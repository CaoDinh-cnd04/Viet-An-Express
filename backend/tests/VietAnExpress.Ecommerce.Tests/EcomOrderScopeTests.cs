using Microsoft.EntityFrameworkCore;
using Moq;
using VietAnExpress.Ecommerce.Application;
using VietAnExpress.Ecommerce.Contracts;
using VietAnExpress.Ecommerce.Domain;
using VietAnExpress.Ecommerce.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using Xunit;

namespace VietAnExpress.Ecommerce.Tests;

/// <summary>Tài khoản con chỉ thấy / xử lý đơn E-commerce mình tạo, trừ khi có quyền xem toàn bộ đơn của công ty.</summary>
public class EcomOrderScopeTests
{
    private const long CustomerId = 201008;
    private const long StaffA = 7;
    private const long StaffB = 8;
    private static readonly DateTime Now = new(2026, 10, 6, 9, 0, 0);

    private static ICurrentUser User(long? staffId, params string[] permissions)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.CustomerId).Returns(CustomerId);
        user.SetupGet(u => u.StaffId).Returns(staffId);
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns<string>(permissions.Contains);
        return user.Object;
    }

    private static ImportedOrder Imported(string name) =>
        new(name, "#" + name, MarketplaceRecipient.Empty with { }, 1, 1, "USD", 10, null, null, Now);

    private static EcommerceDbContext Seed()
    {
        var db = new EcommerceDbContext(new DbContextOptionsBuilder<EcommerceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.MarketplaceOrders.AddRange(
            MarketplaceOrder.Import(CustomerId, 1, "tiktok", Imported("dong-bo"), Now),                       // đồng bộ từ sàn
            MarketplaceOrder.Import(CustomerId, null, "tiktok", Imported("csv-a"), Now, StaffA),             // nhân viên A nhập CSV
            MarketplaceOrder.CreateManual(CustomerId, "manual", Imported("tay-b"), new ManualShipping(null, null, null, null), Now, StaffB),
            MarketplaceOrder.CreateManual(CustomerId, "manual", Imported("admin"), new ManualShipping(null, null, null, null), Now),
            MarketplaceOrder.Import(999, null, "tiktok", Imported("khach-khac"), Now, StaffA));             // khách khác
        db.SaveChanges();
        return db;
    }

    private static List<string> Visible(EcommerceDbContext db, ICurrentUser user) =>
        db.MarketplaceOrders.VisibleTo(user, CustomerId).Select(o => o.OrderName).OrderBy(n => n).ToList();

    [Fact]
    public void Tai_khoan_chinh_thay_moi_don_cua_cong_ty()
    {
        using var db = Seed();
        Assert.Equal(["#admin", "#csv-a", "#dong-bo", "#tay-b"], Visible(db, User(null)));
    }

    [Fact]
    public void Nhan_vien_chi_thay_don_minh_nhap_tay_hoac_nhap_csv()
    {
        using var db = Seed();
        Assert.Equal(["#csv-a"], Visible(db, User(StaffA, EcommercePermissions.View)));
        Assert.Equal(["#tay-b"], Visible(db, User(StaffB, EcommercePermissions.View, EcommercePermissions.Orders)));
    }

    [Fact]
    public void Nhan_vien_co_quyen_xem_toan_bo_thay_ca_don_dong_bo_tu_san()
    {
        using var db = Seed();
        Assert.Equal(["#admin", "#csv-a", "#dong-bo", "#tay-b"], Visible(db, User(StaffA, EcommercePermissions.View, EcommercePermissions.ViewAll)));
    }

    [Fact]
    public void Don_tao_moi_ghi_nhan_vien_tao_don_dong_bo_thi_khong()
    {
        Assert.Equal(StaffA, MarketplaceOrder.Import(CustomerId, null, "tiktok", Imported("x"), Now, StaffA).CreatedByStaffId);
        Assert.Null(MarketplaceOrder.Import(CustomerId, 1, "tiktok", Imported("y"), Now).CreatedByStaffId);
    }
}
