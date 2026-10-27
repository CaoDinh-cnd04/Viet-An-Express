using Microsoft.EntityFrameworkCore;
using Moq;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Domain;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class PublicContactTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PublicContactCommand Valid(string? email = "lan@shop.vn") =>
        new("Nguyễn Thị Lan", "0909 123 456", email, "Shop cần báo giá gửi hàng đi Úc hằng tuần.");

    [Fact]
    public async Task Loi_nhan_ghi_TContact_khong_gan_khach_va_gui_email_ve_hop_thu()
    {
        await using var f = new IdentityFixture();
        var handler = new PublicContactHandler(f.Db, f.Jobs, f.Company, f.Clock);

        var result = await handler.Handle(Valid(), Ct);

        Assert.True(result.IsSuccess);
        var row = await f.Db.Troubles.SingleAsync(Ct);
        Assert.Equal((TroubleKind.Contact, 0L, "Nguyễn Thị Lan", "0909 123 456", "lan@shop.vn", TroubleStatus.New),
            (row.Kind, row.CustomerId, row.Requester, row.Contact, row.Email, row.Status));
        Assert.Equal("Portal - Liên hệ", TroubleKind.FormTypeOf(row.Kind));
        Assert.Equal(("ContactHome", ""), (row.Type, row.Title)); // như form liên hệ trang chủ website cũ (p_TContactHome_Rows)
        Assert.Contains(row.Code, Assert.Single(f.Jobs.Jobs).Name);

        var mail = PublicContactHandler.ContactEmail(row, f.Company);
        Assert.Equal("lan@shop.vn", mail.ReplyTo); // bấm Trả lời là gửi thẳng cho khách
        Assert.Contains("Nguyễn Thị Lan", mail.Subject);
    }

    [Fact]
    public async Task Khach_dang_nhap_khong_thay_loi_nhan_quan_tri_chi_doi_trang_thai_khong_ghi_chu()
    {
        await using var f = new IdentityFixture();
        await new PublicContactHandler(f.Db, f.Jobs, f.Company, f.Clock).Handle(Valid(email: ""), Ct);
        var id = (await f.Db.Troubles.SingleAsync(Ct)).Code;
        var admin = new AdminTroubleHandlers(f.Db, f.CurrentUser.Object, f.Customers.Object, f.Company, f.Clock);
        f.Customers.Setup(c => c.GetByIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        Assert.Equal("lien_he", Assert.Single(await admin.Handle(new GetAdminTroublesQuery(), Ct)).Ticket.Kind);
        Assert.Equal(TroubleErrors.ContactNote, (await admin.Handle(new AdminReplyTroubleCommand(id, "Đã gọi lại", null), Ct)).Error);
        Assert.True((await admin.Handle(new AdminReplyTroubleCommand(id, null, TroubleStatus.Done), Ct)).IsSuccess);
        Assert.Empty(await f.Db.Notifications.ToListAsync(Ct)); // không tạo thông báo cho CustID 0
    }

    [Theory]
    [InlineData("", "0909123456", "", "Nội dung đủ dài rồi", "name")]
    [InlineData("Lan", "abc", "", "Nội dung đủ dài rồi", "phone")]
    [InlineData("Lan", "0909123456", "khong-phai-email", "Nội dung đủ dài rồi", "email")]
    [InlineData("Lan", "0909123456", "", "ngắn", "message")]
    public void Kiem_tra_du_lieu_nhu_frontend(string name, string phone, string email, string message, string field)
    {
        var errors = new PublicContactCommandValidator().Validate(new PublicContactCommand(name, phone, email, message)).Errors;
        Assert.Equal(field, char.ToLowerInvariant(Assert.Single(errors).PropertyName[0]) + Assert.Single(errors).PropertyName[1..]);
    }
}
