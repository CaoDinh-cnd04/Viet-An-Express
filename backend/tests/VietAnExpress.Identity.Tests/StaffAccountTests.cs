using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.MyTracking;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Authorization;
using Xunit;

namespace VietAnExpress.Identity.Tests;

public class StaffAccountTests
{
    private const string StaffPassword = "NhanVien123";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<long> CreateStaffAsync(IdentityFixture f, string userName = "kho.hn", params string[] permissions)
    {
        var created = await f.CreateStaff.Handle(new CreateStaffCommand(userName, StaffPassword, "Nguyễn Văn Kho", null, null,
            permissions.Length > 0 ? permissions : ["shipments.view"]), Ct);
        Assert.True(created.IsSuccess, created.Error.Message);
        return created.Value.Id;
    }

    [Fact]
    public async Task Admin_tao_tai_khoan_con_nhan_vien_dang_nhap_chi_co_quyen_duoc_cap()
    {
        await using var f = new IdentityFixture();
        var staffId = await CreateStaffAsync(f, "kho.hn", "shipments.view");

        var login = await f.Login.Handle(new LoginCommand("kho.hn", StaffPassword, false), Ct);

        Assert.True(login.IsSuccess);
        var user = login.Value.User;
        Assert.Equal(("staff", false, "Nguyễn Văn Kho", "SaigonbayHN"), (user.AccountType, user.IsAdmin, user.FullName, user.CustomerCode));
        Assert.Equal(["shipments.view", IdentityPermissions.SupportRequests], user.Permissions); // + quyền luôn có

        var jwt = new JsonWebToken(login.Value.AccessToken);
        Assert.Equal(IdentityFixture.CustomerId.ToString(), jwt.GetClaim(VaClaimTypes.CustomerId).Value); // thấy dữ liệu của khách cha
        Assert.Equal(staffId.ToString(), jwt.GetClaim(VaClaimTypes.StaffId).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == VaClaimTypes.Permission && c.Value == "account.staff");
    }

    [Fact]
    public async Task Tai_khoan_con_dien_san_nguoi_gui_la_nhan_vien_con_cong_ty_dia_chi_MST_cua_khach_cha()
    {
        await using var f = new IdentityFixture();
        await f.CreateStaff.Handle(new CreateStaffCommand("kho.hn", StaffPassword, "Trần Thị Lan", "lan@sgb.vn", "0901234567", ["shipments.view"]), Ct);
        await f.CreateStaff.Handle(new CreateStaffCommand("kho.hcm", StaffPassword, "Lê Văn Minh", null, null, ["shipments.view"]), Ct);

        var lan = (await f.Login.Handle(new LoginCommand("kho.hn", StaffPassword, false), Ct)).Value.User;
        Assert.Equal(("SGB EXPRESS HN", "Trần Thị Lan", "0901234567", "lan@sgb.vn"), (lan.CompanyName, lan.ContactName, lan.Phone, lan.Email));

        // Nhân viên chưa có SĐT / email: để trống, không lấy của công ty (người tạo đơn tự nhập).
        var minh = (await f.Login.Handle(new LoginCommand("kho.hcm", StaffPassword, false), Ct)).Value.User;
        Assert.Equal(("Lê Văn Minh", null, null), (minh.ContactName, minh.Phone, minh.Email));

        // Tài khoản chính vẫn dùng người liên hệ / SĐT của công ty.
        var admin = (await f.Login.Handle(new LoginCommand(IdentityFixture.UserName, IdentityFixture.Password, false), Ct)).Value.User;
        Assert.Equal(("Đức Anh", "0977714964", "ops@sgb.vn"), (admin.ContactName, admin.Phone, admin.Email));
    }

    [Fact]
    public async Task Di_dong_2_va_email_2_luu_vao_cot_Di_Dong2_Email2_va_xoa_duoc_khi_sua()
    {
        await using var f = new IdentityFixture();
        var created = (await f.CreateStaff.Handle(new CreateStaffCommand("kho.hn", StaffPassword, "Trần Thị Lan", null, "0901234567", [],
            Phone2: " 0912345678 ", Email2: "lan2@sgb.vn"), Ct)).Value;
        Assert.Equal(("0912345678", "lan2@sgb.vn"), (created.Phone2, created.Email2));

        var updated = (await f.UpdateStaff.Handle(new UpdateStaffCommand(created.Id, "Trần Thị Lan", null, "0901234567", [], true,
            Phone2: "", Email2: null), Ct)).Value;
        Assert.Equal((null, null), (updated.Phone2, updated.Email2));
    }

    [Fact]
    public async Task Mat_khau_tai_khoan_con_luu_nhu_he_thong_cu_tai_khoan_cu_con_ma_bam_van_dang_nhap_duoc()
    {
        await using var f = new IdentityFixture();
        await CreateStaffAsync(f);

        // Cột Mo_Ta của dbo.TCustomer_NguoiLienLac dùng chung với hệ thống cũ.
        Assert.Equal(StaffPassword, f.Db.StaffAccounts.Single().Password);
        Assert.Equal(AuthErrors.InvalidCredentials, (await f.Login.Handle(new LoginCommand("kho.hn", "sai-mat-khau", false), Ct)).Error);

        // Tài khoản chuyển từ bảng tạm còn lưu mã băm PBKDF2.
        f.Db.StaffAccounts.Add(new StaffAccount(IdentityFixture.CustomerId, "kho.cu", StaffPasswordHasher.Hash("MatKhauCu1"), "Kho cũ", null, null, [], DateTime.Now));
        await f.Db.SaveChangesAsync(Ct);
        Assert.True((await f.Login.Handle(new LoginCommand("kho.cu", "MatKhauCu1", false), Ct)).IsSuccess);
        Assert.False(StaffPasswordHasher.Matches(StaffPasswordHasher.Hash("MatKhauCu1"), "matkhaucu1"));
    }

    [Fact]
    public async Task Nguoi_lien_he_khong_co_ten_dang_nhap_khong_phai_tai_khoan_con()
    {
        await using var f = new IdentityFixture();
        // Dòng người liên hệ thường của hệ thống cũ: không có TinhCach.
        f.Db.StaffAccounts.Add(new StaffAccount(IdentityFixture.CustomerId, "", "", "Chị Lan kế toán", null, "0909", [], DateTime.Now));
        await f.Db.SaveChangesAsync(Ct);

        Assert.Empty((await f.ListStaff.Handle(new ListStaffQuery(), Ct)).Value);
        Assert.Equal(AuthErrors.InvalidCredentials, (await f.Login.Handle(new LoginCommand("", "", false), Ct)).Error);
    }

    [Fact]
    public async Task Dong_nguoi_lien_he_cu_de_trong_Ten_Mo_Ta_Quyen_van_doc_duoc_khong_dang_nhap_duoc()
    {
        await using var f = new IdentityFixture();
        // Phần mềm cũ ghi TinhCach nhưng để NULL các cột còn lại (Quyen là cột portal thêm sau).
        var legacy = new StaffAccount(IdentityFixture.CustomerId, "kho.cu", "x", "x", null, null, [], DateTime.Now);
        foreach (var field in new[] { "_password", "_fullName", "_permissions" })
            typeof(StaffAccount).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(legacy, null);
        f.Db.StaffAccounts.Add(legacy);
        await f.Db.SaveChangesAsync(Ct);
        f.Db.ChangeTracker.Clear();

        var list = (await f.ListStaff.Handle(new ListStaffQuery(), Ct)).Value;
        Assert.Equal(("kho.cu", "", 0), (list[0].UserName, list[0].FullName, list[0].Permissions.Count));
        // Không có mật khẩu → không đăng nhập được bằng mật khẩu rỗng.
        Assert.Equal(AuthErrors.InvalidCredentials, (await f.Login.Handle(new LoginCommand("kho.cu", "", false), Ct)).Error);
    }

    [Fact]
    public async Task Xoa_tai_khoan_con_chi_go_dang_nhap_giu_dong_nguoi_lien_he()
    {
        await using var f = new IdentityFixture();
        var staffId = await CreateStaffAsync(f);

        Assert.True((await f.DeleteStaff.Handle(new DeleteStaffCommand(staffId), Ct)).IsSuccess);

        Assert.Empty(f.Db.StaffAccounts);
        var row = f.Db.StaffAccounts.IgnoreQueryFilters().Single();
        Assert.Equal((staffId, "Nguyễn Văn Kho", "", ""), (row.Id, row.FullName, row.UserName, row.Password));
        Assert.Equal(AuthErrors.InvalidCredentials, (await f.Login.Handle(new LoginCommand("kho.hn", StaffPassword, false), Ct)).Error);
    }

    [Theory]
    [InlineData("account.staff")]
    [InlineData("account.mytracking")]
    [InlineData("internal.only")]
    [InlineData("khong.ton.tai")]
    public async Task Khong_cap_duoc_quyen_admin_hoac_quyen_la_cho_tai_khoan_con(string permission)
    {
        await using var f = new IdentityFixture();
        var result = await f.CreateStaff.Handle(new CreateStaffCommand("kho.hn", StaffPassword, "Kho", null, null, [permission]), Ct);
        Assert.Equal(StaffErrors.UnknownPermission, result.Error);
    }

    [Theory]
    [InlineData("kho.hn")]
    [InlineData(IdentityFixture.UserName)] // trùng tài khoản chính ở dbo.TCustomer
    public async Task Ten_dang_nhap_khong_duoc_trung(string userName)
    {
        await using var f = new IdentityFixture();
        await CreateStaffAsync(f, "kho.hn");

        var result = await f.CreateStaff.Handle(new CreateStaffCommand(userName, StaffPassword, "Trùng", null, null, []), Ct);
        Assert.Equal(StaffErrors.UserNameTaken, result.Error);
    }

    [Fact]
    public async Task Tai_khoan_con_khong_quan_ly_duoc_nhan_vien()
    {
        await using var f = new IdentityFixture();
        var staffId = await CreateStaffAsync(f);
        f.SignInAs(staffId);

        Assert.Equal(IdentityErrors.AdminOnly, (await f.ListStaff.Handle(new ListStaffQuery(), Ct)).Error);
        Assert.Equal(IdentityErrors.AdminOnly, (await f.CreateStaff.Handle(new CreateStaffCommand("x.y", StaffPassword, "X", null, null, []), Ct)).Error);
        Assert.Equal(IdentityErrors.AdminOnly, (await f.DeleteStaff.Handle(new DeleteStaffCommand(staffId), Ct)).Error);
    }

    [Fact]
    public async Task Admin_khong_sua_duoc_nhan_vien_cua_khach_khac()
    {
        await using var f = new IdentityFixture();
        f.Db.StaffAccounts.Add(new StaffAccount(999, "khach.khac", StaffPasswordHasher.Hash(StaffPassword), "Khác", null, null, [], DateTime.Now));
        await f.Db.SaveChangesAsync(Ct);
        var otherId = f.Db.StaffAccounts.Single().Id;

        Assert.Empty((await f.ListStaff.Handle(new ListStaffQuery(), Ct)).Value);
        Assert.Equal(StaffErrors.StaffNotFound, (await f.DeleteStaff.Handle(new DeleteStaffCommand(otherId), Ct)).Error);
    }

    [Fact]
    public async Task Khoa_tai_khoan_thi_khong_dang_nhap_va_khong_lam_moi_phien_duoc()
    {
        await using var f = new IdentityFixture();
        var staffId = await CreateStaffAsync(f);
        var session = (await f.Login.Handle(new LoginCommand("kho.hn", StaffPassword, true), Ct)).Value;

        await f.UpdateStaff.Handle(new UpdateStaffCommand(staffId, "Kho", null, null, ["shipments.view"], Active: false), Ct);

        Assert.Equal(AuthErrors.StaffDisabled, (await f.Login.Handle(new LoginCommand("kho.hn", StaffPassword, false), Ct)).Error);
        Assert.Equal(AuthErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(session.RefreshToken), Ct)).Error);
    }

    [Fact]
    public async Task Doi_quyen_ap_dung_khi_lam_moi_phien()
    {
        await using var f = new IdentityFixture();
        var staffId = await CreateStaffAsync(f, "kho.hn", "shipments.view");
        var session = (await f.Login.Handle(new LoginCommand("kho.hn", StaffPassword, false), Ct)).Value;

        await f.UpdateStaff.Handle(new UpdateStaffCommand(staffId, "Kho", null, null, ["shipments.view", "shipments.create"], Active: true), Ct);

        var refreshed = await f.Refresh.Handle(new RefreshSessionCommand(session.RefreshToken), Ct);
        Assert.Equal(["shipments.create", "shipments.view", IdentityPermissions.SupportRequests], refreshed.Value.User.Permissions);
    }

    [Fact]
    public async Task Sua_quyen_gui_lai_quyen_luon_co_van_luu_duoc_va_danh_sach_chi_hien_quyen_da_chon()
    {
        await using var f = new IdentityFixture();
        var staffId = await CreateStaffAsync(f, "kho.hn", "shipments.view", IdentityPermissions.SupportRequests);

        // Trang quản lý chỉ thấy quyền tài khoản chính đã chọn (không có quyền luôn có như hỗ trợ & sự cố).
        Assert.Equal(["shipments.view"], Assert.Single((await f.ListStaff.Handle(new ListStaffQuery(), Ct)).Value).Permissions);

        // Client cũ gửi kèm quyền luôn có khi sửa / khóa → vẫn lưu được, không báo quyền không hợp lệ.
        var updated = await f.UpdateStaff.Handle(new UpdateStaffCommand(staffId, "Kho", null, null,
            ["shipments.view", "shipments.create", IdentityPermissions.SupportRequests], Active: true), Ct);
        Assert.True(updated.IsSuccess, updated.Error.Message);
        Assert.Equal(["shipments.create", "shipments.view"], updated.Value.Permissions);

        // Nhân viên đăng nhập vẫn có quyền hỗ trợ & sự cố.
        var login = await f.Login.Handle(new LoginCommand("kho.hn", StaffPassword, false), Ct);
        Assert.Contains(IdentityPermissions.SupportRequests, login.Value.User.Permissions);
    }

    [Fact]
    public async Task Admin_dat_lai_mat_khau_thi_phien_cu_cua_nhan_vien_het_hieu_luc()
    {
        await using var f = new IdentityFixture();
        var staffId = await CreateStaffAsync(f);
        var session = (await f.Login.Handle(new LoginCommand("kho.hn", StaffPassword, false), Ct)).Value;

        Assert.True((await f.ResetStaffPassword.Handle(new ResetStaffPasswordCommand(staffId, "MatKhauMoi9"), Ct)).IsSuccess);

        Assert.Equal(AuthErrors.SessionExpired, (await f.Refresh.Handle(new RefreshSessionCommand(session.RefreshToken), Ct)).Error);
        Assert.True((await f.Login.Handle(new LoginCommand("kho.hn", "MatKhauMoi9", false), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Nhan_vien_khong_tu_doi_mat_khau_nhung_xem_duoc_ho_so_cua_minh()
    {
        await using var f = new IdentityFixture();
        var staffId = await CreateStaffAsync(f);
        f.SignInAs(staffId);

        var changed = await f.ChangePassword.Handle(new ChangePasswordCommand(StaffPassword, "MatKhauMoi9", null), Ct);
        Assert.Equal(AuthErrors.StaffPasswordManagedByAdmin, changed.Error);
        Assert.Equal(StaffPassword, f.Db.StaffAccounts.Single().Password); // mật khẩu không đổi
        Assert.Equal(IdentityFixture.Password, f.StoredPassword()); // không đụng mật khẩu tài khoản chính

        var me = await f.Me.Handle(new GetSessionQuery(), Ct);
        Assert.Equal(("kho.hn", "staff"), (me.Value.UserName, me.Value.AccountType));
    }
}

public class MyTrackingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement Config(string title) => JsonDocument.Parse($$"""{"title":"{{title}}","images":[]}""").RootElement.Clone();

    [Fact]
    public async Task Chua_cau_hinh_thi_tra_ma_khach_va_cau_hinh_trong()
    {
        await using var f = new IdentityFixture();
        var page = await f.GetMyTracking.Handle(new GetMyTrackingQuery(), Ct);
        Assert.Equal("SaigonbayHN", page.Value.CustomerCode);
        Assert.Null(page.Value.Config);
    }

    [Fact]
    public async Task Luu_xong_thi_trang_cong_khai_theo_ma_khach_xem_duoc()
    {
        await using var f = new IdentityFixture();
        // Chưa có dòng cấu hình → không có trang.
        Assert.Equal(MyTrackingErrors.MyTrackingNotFound, (await f.GetCustomerMyTracking.Handle(new GetCustomerMyTrackingQuery("saigonbayhn"), Ct)).Error);

        await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(Config("Trang cua toi")), Ct);
        var page = await f.GetCustomerMyTracking.Handle(new GetCustomerMyTrackingQuery("saigonbayhn"), Ct);
        Assert.Equal("Trang cua toi", JsonDocument.Parse(page.Value.Config.Json).RootElement.GetProperty("title").GetString());
        Assert.Equal("SaigonbayHN", page.Value.CustomerCode);
        Assert.Equal(MyTrackingErrors.MyTrackingNotFound, (await f.GetCustomerMyTracking.Handle(new GetCustomerMyTrackingQuery("khong-co"), Ct)).Error);
    }

    [Fact]
    public async Task Tai_khoan_con_khong_cau_hinh_duoc_MyTracking()
    {
        await using var f = new IdentityFixture();
        f.SignInAs(5);
        Assert.Equal(IdentityErrors.AdminOnly, (await f.GetMyTracking.Handle(new GetMyTrackingQuery(), Ct)).Error);
        Assert.Equal(IdentityErrors.AdminOnly, (await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(Config("x")), Ct)).Error);
    }

    [Fact]
    public async Task Cau_hinh_khong_phai_object_bi_tu_choi()
    {
        await using var f = new IdentityFixture();
        var array = JsonDocument.Parse("[]").RootElement.Clone();
        Assert.Equal(MyTrackingErrors.InvalidTrackingConfig, (await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(array), Ct)).Error);
    }

    [Fact]
    public async Task Gia_tri_dai_hon_cot_bang_cu_bi_tu_choi()
    {
        await using var f = new IdentityFixture();
        var config = JsonDocument.Parse("{\"brand\":{\"website\":\"https://" + new string('a', 260) + ".vn\"}}").RootElement.Clone();
        Assert.Equal(MyTrackingErrors.InvalidTrackingConfig, (await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(config), Ct)).Error);
    }

    [Fact]
    public async Task Ten_cong_ty_dai_hon_cot_bang_cu_thi_cat_con_250_van_luu_duoc()
    {
        await using var f = new IdentityFixture();
        f.Customers.Setup(c => c.GetByIdAsync(IdentityFixture.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerSummary(IdentityFixture.CustomerId, "SaigonbayHN", "SGB", null, null, null, null, null, new string('A', 300)));
        var saved = await f.SaveMyTracking.Handle(new SaveMyTrackingCommand(Config("x")), Ct);
        Assert.True(saved.IsSuccess);
        Assert.Equal(250, (await f.Db.MyTrackingPages.SingleAsync(Ct)).CompanyName!.Length);
    }
}
