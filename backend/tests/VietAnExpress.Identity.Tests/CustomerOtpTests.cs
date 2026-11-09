using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.CustomerOtp;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using Xunit;

namespace VietAnExpress.Identity.Tests;

/// <summary>
/// OTP đăng nhập của khách: công tắc CustomerOtp:Mode; lần đầu mọi tài khoản nhập email nhận mã + đồng ý điều khoản, xác nhận bằng mã gửi tới
/// email đó (ghi đè cột Email); các lần sau mã gửi thẳng tới email đã xác nhận.
/// </summary>
public class CustomerOtpTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Kho mã + sổ email dùng chung cho các bước của 1 test.</summary>
    private static readonly EmailCompany TestCompany = new("Việt An Express", null, null, null, null);

    private sealed class Otp(IdentityFixture f, string mode = CustomerOtpOptions.On)
    {
        public CustomerOtpStore Store { get; } = new(f.Clock);
        public OtpEmailRegistry Registry { get; } = NewRegistry(f);
        private CustomerOtpMailer Mailer => new(Store, f.Email, TestCompany, f.Environment.Object, NullLogger<CustomerOtpMailer>.Instance);

        public StartCustomerOtpHandler Start => new(f.Db, Store, Registry, Mailer, Options.Create(new CustomerOtpOptions { Mode = mode }));
        public SetOtpEmailHandler SetEmail => new(Store, Mailer, f.Clock);
        public VerifyCustomerOtpHandler Verify => new(f.Db, f.Sessions, f.LoginGuard, Store, Registry);

        private static OtpEmailRegistry NewRegistry(IdentityFixture f)
        {
            f.Environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
            return new(f.Environment.Object, NullLogger<OtpEmailRegistry>.Instance);
        }
    }

    private static async Task<AuthSession> Password(IdentityFixture f, string userName = IdentityFixture.UserName, string password = IdentityFixture.Password) =>
        (await f.Login.Handle(new LoginCommand(userName, password, false), Ct)).Value;

    private static string SentCode(IdentityFixture f) => Regex.Match(f.Email.Sent[^1].Body, @"bạn: (\d{6})").Groups[1].Value;

    [Fact]
    public async Task Tat_OTP_thi_dang_nhap_nhu_cu()
    {
        await using var f = new IdentityFixture();
        var otp = new Otp(f, CustomerOtpOptions.Off);
        Assert.Null((await otp.Start.Handle(new StartCustomerOtpCommand(await Password(f), IdentityFixture.UserName, false), Ct)).Value);
        Assert.Empty(f.Email.Sent);
    }

    [Fact]
    public async Task Lan_dau_nhap_email_va_dong_y_xac_nhan_bang_ma_roi_luu_email_lan_sau_gui_ma_thang()
    {
        await using var f = new IdentityFixture();
        var otp = new Otp(f);

        // Lần đầu: chưa gửi mã, bắt nhập email (khách chưa có email hồ sơ).
        var first = (await otp.Start.Handle(new StartCustomerOtpCommand(await Password(f), IdentityFixture.UserName, false), Ct)).Value!;
        Assert.True(first.NeedEmail);
        Assert.Empty(f.Email.Sent);

        // Chưa tick đồng ý / email sai → từ chối.
        Assert.Equal("CONSENT_REQUIRED", (await otp.SetEmail.Handle(new SetOtpEmailCommand(first.ChallengeId, "ketoan@sgb.vn", false), Ct)).Error.Code);
        Assert.Equal("OTP_EMAIL_INVALID", (await otp.SetEmail.Handle(new SetOtpEmailCommand(first.ChallengeId, "kd@vietanexpress.com", true), Ct)).Error.Code);

        var sent = (await otp.SetEmail.Handle(new SetOtpEmailCommand(first.ChallengeId, "ketoan@sgb.vn", true), Ct)).Value;
        Assert.Equal("ke***@sgb.vn", sent.Email);
        Assert.Equal(["ketoan@sgb.vn"], f.Email.Sent[^1].To);
        Assert.Equal("OTP_INVALID", (await otp.Verify.Handle(new VerifyCustomerOtpCommand(sent.ChallengeId, "000000"), Ct)).Error.Code);
        Assert.True((await otp.Verify.Handle(new VerifyCustomerOtpCommand(sent.ChallengeId, SentCode(f)), Ct)).IsSuccess);
        Assert.Equal("ketoan@sgb.vn", (await f.Db.Logins.AsNoTracking().SingleAsync(l => l.CustomerId == IdentityFixture.CustomerId, Ct)).Email);

        // Lần sau: gửi mã thẳng tới email đã xác nhận.
        var next = (await otp.Start.Handle(new StartCustomerOtpCommand(await Password(f), IdentityFixture.UserName, false), Ct)).Value!;
        Assert.False(next.NeedEmail);
        Assert.Equal(["ketoan@sgb.vn"], f.Email.Sent[^1].To);
        var session = await otp.Verify.Handle(new VerifyCustomerOtpCommand(next.ChallengeId, SentCode(f)), Ct);
        Assert.Equal(IdentityFixture.CustomerId, session.Value.User.CustomerId);
        Assert.Equal("OTP_EXPIRED", (await otp.Verify.Handle(new VerifyCustomerOtpCommand(next.ChallengeId, SentCode(f)), Ct)).Error.Code); // dùng 1 lần
    }

    [Fact]
    public async Task Da_co_email_ho_so_van_phai_xac_nhan_lai_lan_dau_email_dien_san()
    {
        await using var f = new IdentityFixture();
        var login = await f.Db.Logins.SingleAsync(l => l.CustomerId == IdentityFixture.CustomerId, Ct);
        login.SetEmail("ops@sgb.vn");
        await f.Db.SaveChangesAsync(Ct);
        var otp = new Otp(f);

        var first = (await otp.Start.Handle(new StartCustomerOtpCommand(await Password(f), IdentityFixture.UserName, false), Ct)).Value!;
        Assert.True(first.NeedEmail);
        Assert.Equal("ops@sgb.vn", first.SuggestedEmail);
    }

    [Fact]
    public async Task Tai_khoan_con_xac_nhan_email_rieng_va_vao_lai_dung_tai_khoan_con()
    {
        await using var f = new IdentityFixture();
        f.Db.StaffAccounts.Add(new StaffAccount(IdentityFixture.CustomerId, "kho.lan", StaffPasswordHasher.Hash("MatKhau1"), "Lan kho", null, null, DateTime.Now));
        await f.Db.SaveChangesAsync(Ct);
        var otp = new Otp(f);

        var first = (await otp.Start.Handle(new StartCustomerOtpCommand(await Password(f, "kho.lan", "MatKhau1"), "kho.lan", true), Ct)).Value!;
        var sent = (await otp.SetEmail.Handle(new SetOtpEmailCommand(first.ChallengeId, "lan@sgb.vn", true), Ct)).Value;
        var session = (await otp.Verify.Handle(new VerifyCustomerOtpCommand(sent.ChallengeId, SentCode(f)), Ct)).Value;

        Assert.Equal("staff", session.User.AccountType);
        Assert.True(session.IsPersistent);
        Assert.Equal("lan@sgb.vn", (await f.Db.StaffAccounts.AsNoTracking().SingleAsync(s => s.UserName == "kho.lan", Ct)).Email);
        Assert.Null((await f.Db.Logins.AsNoTracking().SingleAsync(l => l.CustomerId == IdentityFixture.CustomerId, Ct)).Email); // không đụng email tài khoản chính
    }

    [Theory]
    [InlineData("ketoan@congtykhach.vn", "ketoan@congtykhach.vn")]
    [InlineData(" a@b.vn; c@d.vn", "a@b.vn")]
    [InlineData("sale@vietanexpress.com", null)]
    [InlineData("congtykhach.vn", null)]
    [InlineData(null, null)]
    public void Email_nhan_ma(string? value, string? expected) => Assert.Equal(expected, CustomerOtpRules.Recipient(value));
}
