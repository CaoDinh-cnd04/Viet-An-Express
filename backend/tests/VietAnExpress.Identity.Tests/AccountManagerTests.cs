using VietAnExpress.Customers.Application;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.Infrastructure;
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

    [Theory]
    [InlineData("0909 123 456", "0909 123 456")]
    [InlineData("+84 28 3948 3949", "+84 28 3948 3949")]
    [InlineData("01/01/1987", null)]
    [InlineData("123", null)]
    [InlineData(" ", null)]
    public void Chi_nhan_gia_tri_giong_so_dien_thoai(string value, string? expected) => Assert.Equal(expected, CustomersApi.Phone(value));
}
