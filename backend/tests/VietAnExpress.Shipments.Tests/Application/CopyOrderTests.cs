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
        return new(user.Object, Mock.Of<ICustomersApi>(), db);
    }

    private const string Form = """
        {"shipper":{"company":"Original sender","originalShipper":"FWD customer"},
         "receiver":{"company":"Original receiver","addr3":"Third address","phoneCode":"+1"},
         "service":{"carrier":"DHL","hub":"DHL - VN","reference":"KEEP-REF"},
         "shipment":{"type":"PACK","pieces":"3","grossWeight":"8"},
         "goods":{"description":"Content","multi":[{"name":"Clothes"}]},
         "packages":[{"qty":"3","weight":"2.666","length":"20.25","category":"Clothes"}],
         "invoice":{"currency":"CAD","dutyTerms":"DDP","shippingFee":"12.50","items":[{"descEn":"Item","manufacturer":"Maker","price":"1.25"}]},
         "addons":["Đóng thuế hộ (DDP)"]}
        """;

    [Fact]
    public async Task Copy_doc_ban_form_goc_da_xoa_mem_giu_tat_ca_truong_va_khong_ghi_DB()
    {
        await using var db = Db();
        db.LegacyOrders.Add(new LegacyOrder { Id = 100, CustomerId = 42, OrderNumber = 90000001, GoodsName = "Old rounded value" });
        var archive = new OrderDraft(42, new("ready", "Receiver", "US", "DHL", "TP.HCM", "REF", "3", "Content"), Form);
        archive.MarkPrinted(90000001);
        db.OrderDrafts.Add(archive);
        db.Entry(archive).Property(x => x.IsDeleted).CurrentValue = true;
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.OrderDrafts.ToListAsync(Ct));
        var result = await new CopyOrderHandler(db, Access(db)).Handle(new("90000001"), Ct);
        Assert.True(result.IsSuccess);
        Assert.Equal("20.25", result.Value.GetProperty("packages")[0].GetProperty("length").GetString());
        Assert.Equal("Maker", result.Value.GetProperty("invoice").GetProperty("items")[0].GetProperty("manufacturer").GetString());
        Assert.Equal("Đóng thuế hộ (DDP)", result.Value.GetProperty("addons")[0].GetString());
        Assert.Equal("KEEP-REF", result.Value.GetProperty("service").GetProperty("reference").GetString());
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Single(await db.LegacyOrders.ToListAsync(Ct));
    }

    [Fact]
    public async Task Copy_khong_doc_don_cua_khach_khac_hoac_nhan_vien_khac()
    {
        await using var db = Db();
        db.LegacyOrders.Add(new LegacyOrder { Id = 100, CustomerId = 42, OrderNumber = 90000001 });
        db.OrderCreators.Add(new OrderCreator(100, 42, 7, DateTime.UtcNow));
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

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{\"shipper\":null}")]
    public void Form_luu_hong_thi_dung_du_lieu_don_cu(string json) => Assert.Null(OrderCopy.TryReadForm(json));
}
