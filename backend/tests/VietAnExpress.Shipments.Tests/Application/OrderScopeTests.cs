using Microsoft.EntityFrameworkCore;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Infrastructure;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Tài khoản con chỉ thấy vận đơn / nháp mình tạo, trừ khi có quyền xem toàn bộ đơn của công ty.</summary>
public class OrderScopeTests
{
    private const long CustomerId = 201008;
    private const long StaffId = 7;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ShipmentsDbContext CreateDb() => new(new DbContextOptionsBuilder<ShipmentsDbContext>()
        .UseSqlServer("Server=localhost;Database=TranslationOnly;Integrated Security=True;Encrypt=True")
        .Options);

    private static ICurrentUser User(long? staffId, params string[] permissions)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.CustomerId).Returns(CustomerId);
        user.SetupGet(u => u.StaffId).Returns(staffId);
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns<string>(permissions.Contains);
        return user.Object;
    }

    private static async Task<(OrderScope Scope, string Sql)> ScopeSqlAsync(ICurrentUser user)
    {
        using var db = CreateDb();
        var access = new OrderAccess(user, Mock.Of<ICustomersApi>());
        var scope = await access.ScopeAsync(Ct);
        return (scope, access.Apply(db.LegacyOrders, scope).ToQueryString());
    }

    [Fact]
    public async Task Tai_khoan_chinh_thay_moi_don_cua_khach()
    {
        var (scope, sql) = await ScopeSqlAsync(User(null));
        Assert.Equal(new OrderScope(CustomerId, null), scope);
        Assert.Contains("[CustomerID]", sql);
        Assert.DoesNotContain("[Customer_Staff_ID] =", sql);
    }

    [Fact]
    public async Task Nhan_vien_chi_thay_don_minh_tao()
    {
        var (scope, sql) = await ScopeSqlAsync(User(StaffId, ShipmentsPermissions.View));
        Assert.Equal(new OrderScope(CustomerId, StaffId), scope);
        Assert.Contains("[CustomerID]", sql);
        Assert.Contains("[Customer_Staff_ID] = @staffId", sql);
    }

    [Fact]
    public async Task Nhan_vien_co_quyen_xem_toan_bo_thay_moi_don_cua_cong_ty()
    {
        var (scope, sql) = await ScopeSqlAsync(User(StaffId, ShipmentsPermissions.View, ShipmentsPermissions.ViewAll));
        Assert.Equal(new OrderScope(CustomerId, null), scope);
        Assert.DoesNotContain("[Customer_Staff_ID] =", sql);
    }

    [Fact]
    public async Task Nhan_vien_chi_thay_nhap_minh_tao_nhap_nam_trong_MaVanDon_Status_0()
    {
        using var db = CreateDb();
        async Task<string> Where(ICurrentUser user)
        {
            var access = new OrderAccess(user, Mock.Of<ICustomersApi>());
            return access.Apply(db.Drafts(), await access.ScopeAsync(Ct)).ToQueryString();
        }
        Assert.Contains("[Customer_Staff_ID] =", await Where(User(StaffId)));
        Assert.DoesNotContain("[Customer_Staff_ID] =", await Where(User(StaffId, ShipmentsPermissions.ViewAll)));
        Assert.Contains("[Status] = 0", await Where(User(null)));
    }
}
