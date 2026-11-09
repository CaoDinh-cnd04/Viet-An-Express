using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.NewCustomers;
using VietAnExpress.Customers.Shared;
using VietAnExpress.Customers.VietAnUsers;
using Xunit;

namespace VietAnExpress.Identity.Tests;

/// <summary>Nhân viên Việt An phụ trách khách (TCustomer.Staff_ID → USERS) — chuẩn bị cho CMS.</summary>
public class AccountManagerTests
{
    [Fact]
    public void Ho_ten_ghep_ten_va_ho_sdt_uu_tien_di_dong()
    {
        var m = CustomersApi.ToAccountManager(new VietAnUser
        {
            Id = 9, UserName = " vo.hanh ", FirstName = "Hạnh Võ", LastName = "M.Hạnh", Email = "hanh@vietan.vn",
            MobilePhone = " ", WorkPhone = "028 3948 3949", Status = "ACTIVE", BranchId = 2
        });

        Assert.Equal(new AccountManager(9, "vo.hanh", "Hạnh Võ M.Hạnh", "hanh@vietan.vn", "028 3948 3949", 2, true), m);
    }

    [Fact]
    public void Thieu_ten_thi_dung_ten_dang_nhap_nhan_vien_da_xoa_coi_nhu_chua_phan_cong()
    {
        var noName = CustomersApi.ToAccountManager(new VietAnUser { Id = 7, UserName = "son.hieu", Status = "INACTIVE" })!;
        Assert.Equal(("son.hieu", false), (noName.FullName, noName.Active));

        Assert.Null(CustomersApi.ToAccountManager(new VietAnUser { Id = 3, UserName = "cu", Deleted = true }));
    }

    [Fact]
    public void Nhan_vien_dang_nhap_portal_khi_ACTIVE_chua_xoa_co_mat_khau_IS_ADMIN_VIEW_CUSTOMER_ALL_khong_mo_rong_pham_vi()
    {
        var u = new VietAnUser { Id = 7, UserName = "vo.hanh", Status = "ACTIVE", Password = "246810" };
        Assert.Equal(new VietAnUserLogin(7, "vo.hanh", "vo.hanh", false, "246810"), CustomersApi.ToLogin(u));
        // Dữ liệu cũ bật IS_ADMIN / VIEW_CUSTOMER_ALL cho cả sale → không dùng; phạm vi chỉ theo vai trò (06/11/2026).
        Assert.False(CustomersApi.ToLogin(new VietAnUser { Id = 7, UserName = "a", Status = "ACTIVE", Password = "1", IsAdmin = true }, ["Sale", "ALL"])!.ViewAllCustomers);
        Assert.False(CustomersApi.ToLogin(new VietAnUser { Id = 7, UserName = "a", Status = "ACTIVE", Password = "1", ViewCustomerAll = 1 }, ["Sale"])!.ViewAllCustomers);
        Assert.Null(CustomersApi.ToLogin(new VietAnUser { Id = 7, UserName = "a", Status = "INACTIVE", Password = "1" }));
        Assert.Null(CustomersApi.ToLogin(new VietAnUser { Id = 7, UserName = "a", Status = "ACTIVE", Password = "1", Deleted = true }));
        Assert.Null(CustomersApi.ToLogin(new VietAnUser { Id = 7, UserName = "a", Status = "ACTIVE", Password = "" }));
    }

    [Theory]
    [InlineData(1157, null, 1157, false, true)]             // sale phụ trách (Staff_ID)
    [InlineData(20, "1157|", 1157, true, true)]             // CS được gán ở SubTer (như CMS)
    [InlineData(20, "1157|", 1157, false, false)]           // sale không tính SubTer
    [InlineData(20, "1158|1157|1159|", 1157, true, true)]
    [InlineData(20, "1158| 1157 |1159", 1157, true, true)]  // khoảng trắng, thiếu "|" cuối
    [InlineData(20, "11570|1158|", 1157, true, false)]      // không khớp 1 phần mã
    [InlineData(20, "", 1157, true, false)]
    [InlineData(20, null, 1157, true, false)]
    public void Pham_vi_khach_Staff_ID_chi_CS_tinh_them_SubTer(int staffId, string? subTer, int agent, bool cs, bool expected) =>
        Assert.Equal(expected, VietAnExpress.Customers.Profiles.CustomerScope.Matches(staffId, subTer, agent, cs));

    [Fact]
    public void CS_co_vai_tro_ALL_xem_moi_khach_sale_co_ALL_thi_khong()
    {
        Assert.True(VietAnRoles.SeesAllCustomers(["CS", " all "]));
        Assert.False(VietAnRoles.SeesAllCustomers(["CS"]));
        Assert.False(VietAnRoles.SeesAllCustomers(["Sale", "ALL"]));
        Assert.True(VietAnRoles.UsesSubTer(["cs"]));
        Assert.False(VietAnRoles.UsesSubTer(["Sale", "ALL"]));
    }

    [Fact]
    public void Vai_tro_Administrator_o_ACL_ROLES_xem_moi_khach()
    {
        Assert.True(VietAnRoles.SeesAllCustomers(["Sale", " administrator "]));
        Assert.True(VietAnRoles.SeesAllCustomers(["Sale", "CS", "ALL", null]));
        Assert.False(VietAnRoles.SeesAllCustomers([]));

        var sale = new VietAnUser { Id = 7, UserName = "a", Status = "ACTIVE", Password = "1" };
        Assert.False(CustomersApi.ToLogin(sale)!.ViewAllCustomers);
        Assert.True(CustomersApi.ToLogin(sale, ["Administrator"])!.ViewAllCustomers);
    }

    [Fact]
    public void Admin_va_CS_gan_nguoi_phu_trach_khach_moi()
    {
        var u = new VietAnUser { Id = 7, UserName = "a", Status = "ACTIVE", Password = "1" };
        Assert.False(CustomersApi.ToLogin(u, ["Sale"])!.CanAssignManager);
        Assert.True(CustomersApi.ToLogin(u, [" cs "])!.CanAssignManager);
        Assert.True(CustomersApi.ToLogin(u, ["Administrator"])!.CanAssignManager);
        Assert.False(CustomersApi.ToLogin(new VietAnUser { Id = 7, UserName = "a", Status = "ACTIVE", Password = "1", IsAdmin = true }, ["Sale"])!.CanAssignManager);

        Assert.True(NewCustomerRules.IsSupportStaff(["Accounting", "Sale"]));
        Assert.True(NewCustomerRules.IsSupportStaff(["CS"]));
        Assert.False(NewCustomerRules.IsSupportStaff(["Accounting", null]));
        Assert.True(NewCustomerRules.IsNew(" portal - khách mới "));
        Assert.False(NewCustomerRules.IsNew("Tự tìm"));

        var clean = NewCustomerRules.Clean(new NewCustomerInfo("  ABC  ", "", null, new string('x', 300), " 0312 ", "a@b.vn"));
        Assert.Equal("ABC", clean.CompanyName);
        Assert.Null(clean.ContactName);
        Assert.Equal(250, clean.Address!.Length);
        Assert.Equal("0312", clean.TaxCode);
    }

    [Theory]
    [InlineData("0909 123 456", "0909 123 456")]
    [InlineData("+84 28 3948 3949", "+84 28 3948 3949")]
    [InlineData("01/01/1987", null)]
    [InlineData("123", null)]
    [InlineData(" ", null)]
    public void Chi_nhan_gia_tri_giong_so_dien_thoai(string value, string? expected) => Assert.Equal(expected, CustomersApi.Phone(value));
}
