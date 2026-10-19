using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Domain;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Legacy;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class CopyOrderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static ShipmentsDbContext Db() => new(new DbContextOptionsBuilder<ShipmentsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static OrderAccess Access(ShipmentsDbContext db, long customerId = 42, long? staffId = null, bool viewAll = false)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.CustomerId).Returns(customerId);
        user.SetupGet(x => x.StaffId).Returns(staffId);
        user.Setup(x => x.HasPermission(ShipmentsPermissions.ViewAll)).Returns(viewAll);
        return new(user.Object, Mock.Of<ICustomersApi>());
    }

    [Fact]
    public async Task Don_chua_in_khong_hien_o_don_hang_va_khong_nhan_ban_duoc()
    {
        await using var db = Db();
        db.LegacyOrders.Add(new LegacyOrder { Id = 100, CustomerId = 42, OrderNumber = 90000001, Status = LegacyOrder.IssuedStatus });
        db.LegacyOrders.Add(new LegacyOrder { Id = 101, CustomerId = 42, Status = LegacyOrder.DraftStatus, VaBill = "VAHN" });
        db.LegacyOrders.Add(new LegacyOrder { Id = 102, CustomerId = 42, OrderNumber = 6000001 }); // đơn cũ Status NULL
        await db.SaveChangesAsync(Ct);

        Assert.Equal([100L, 102L], await db.LegacyOrders.Select(o => o.Id).OrderBy(x => x).ToListAsync(Ct));
        Assert.Equal([101L], await db.Drafts().Select(o => o.Id).ToListAsync(Ct));
        Assert.True((await new CopyOrderHandler(db, Access(db)).Handle(new("90000001"), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Copy_khong_doc_don_cua_khach_khac_hoac_nhan_vien_khac()
    {
        await using var db = Db();
        db.LegacyOrders.Add(new LegacyOrder { Id = 100, CustomerId = 42, OrderNumber = 90000001, CustomerStaffId = 7 });
        await db.SaveChangesAsync(Ct);
        Assert.True((await new CopyOrderHandler(db, Access(db, 99)).Handle(new("90000001"), Ct)).IsFailure);
        Assert.True((await new CopyOrderHandler(db, Access(db, 42, 8)).Handle(new("90000001"), Ct)).IsFailure);
        Assert.True((await new CopyOrderHandler(db, Access(db, 42, 7)).Handle(new("90000001"), Ct)).IsSuccess);
        Assert.True((await new CopyOrderHandler(db, Access(db, 42, 8, true)).Handle(new("90000001"), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Don_cu_khong_co_form_goc_van_copy_kien_invoice_nguoi_gui_nhan_va_tham_chieu()
    {
        await using var db = Db();
        db.LegacyOrders.Add(new LegacyOrder
        {
            Id = 100, CustomerId = 42, OrderNumber = 90000001, SenderName = "Sender", SenderPhone = "0909000000", ForwarderShipperName = "FWD",
            ConsigneeName = "Receiver", ConsigneeAddress3 = "Address 3", ConsigneePhoneCode = "+1", ServiceName = "UPS|Original hub",
            CustomerBill = "REF", Pieces = 2, WeightKg = 9, GoodsName = "Dresses", Currency = "CAD", ShippingFee = 12.5m, ExportReason = "SAMPLE"
        });
        db.LegacyPackageLines.Add(new LegacyPackageLine { Id = 1, OrderId = 100, Quantity = 2, PackType = "BAG", LengthCm = 20, WidthCm = 30, HeightCm = 40, WeightKg = 6 });
        db.LegacyInvoiceLines.Add(new LegacyInvoiceLine { Id = 1, OrderId = 100, DescriptionEn = "Dress", DescriptionVi = "Váy", Manufacturer = "Maker", Quantity = 2, UnitPrice = 1.2345m, Unit = "PCS", HsCode = "610910", Origin = "CN" });
        await db.SaveChangesAsync(Ct);
        var result = (await new CopyOrderHandler(db, Access(db)).Handle(new("90000001"), Ct)).Value;
        var p = JsonSerializer.Deserialize<OrderPayload>(result.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("FWD", p.Shipper.OriginalShipper);
        Assert.Equal("Address 3", p.Receiver.Addr3);
        Assert.Equal("+1", p.Receiver.PhoneCode);
        Assert.Equal("REF", p.Service.Reference);
        Assert.Equal("UPS - Original hub", p.Service.Hub);
        Assert.Equal("6", p.Shipment.GrossWeight); // actual weight from the row, not billable weight 9
        Assert.Equal("3", Assert.Single(p.Packages).Weight);
        Assert.Equal("Bao / túi", p.Packages[0].Packaging);
        Assert.Equal("Maker", Assert.Single(p.Invoice.Items).Manufacturer);
        Assert.Equal("1.2345", p.Invoice.Items[0].Price);
        Assert.Equal("CAD", p.Invoice.Currency);
        Assert.Equal("12.5", p.Invoice.ShippingFee);
    }
}
