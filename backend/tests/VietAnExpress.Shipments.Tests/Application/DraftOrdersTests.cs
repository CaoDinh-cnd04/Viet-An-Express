using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>Đơn chưa in lưu thẳng trong dbo.MaVanDon (Status 0).</summary>
public class DraftOrdersTests
{
    private static readonly LegacyCustomerRef Customer = new(201008, "SGB EXPRESS HN", "Đức Anh", "0977714964", null);
    private static readonly DateTime Today = new(2026, 10, 15);

    private static OrderPayload Payload(string branch = "Hà Nội") => new()
    {
        Shipper = new() { Company = "SGB EXPRESS HN", Address = "241 Tân Xuân, HN", Branch = branch },
        Service = new() { Carrier = "DHL", Hub = "DHL - Singapore", Reference = "PO-1001" },
        Shipment = new() { Type = "PACK" },
        Receiver = new() { Company = "Jason Teo", Country = "Singapore", City = "SINGAPORE", Addr1 = "8 Bhamo Road" },
        Packages = [new() { Qty = "1", Weight = "2", Category = LegacyOrderFactory.MultiCategory, Description = "Watch strap" }]
    };

    [Fact]
    public void Nhap_ghi_Status_0_chua_co_so_bill_va_VA_Bill_de_NULL_nhu_he_thong_cu()
    {
        var o = DraftOrders.Build(Payload(), Customer, Today, consigneeCountryId: 191);

        Assert.Equal(LegacyOrder.DraftStatus, o.Status);
        Assert.Null(o.OrderNumber);
        Assert.Null(o.VaBill); // 23/10/2026: không ghi VA_Bill (cả tiền tố chi nhánh) — như hệ thống cũ
        Assert.Equal(("DHL|Singapore", "Jason Teo", 2m, "Watch strap"), (o.ServiceName, o.ConsigneeName, o.WeightKg, o.GoodsName));
        Assert.True(DraftOrders.IsReady(o));
    }

    [Fact]
    public void Nhap_chua_khai_du_la_nhap_khai_du_la_cho_in()
    {
        var empty = DraftOrders.Build(new OrderPayload(), Customer, Today, null);
        Assert.False(DraftOrders.IsReady(empty));
        Assert.Null(empty.GoodsName); // không ghi chữ mặc định "Goods" cho nháp trống

        var noAddress = DraftOrders.Build(Payload(), Customer, Today, null);
        noAddress.ConsigneeAddress1 = null;
        Assert.False(DraftOrders.IsReady(noAddress));
    }

    [Fact]
    public void Chi_nhanh_chua_co_tien_to_thi_khong_ghi_VA_Bill()
    {
        Assert.Null(DraftOrders.Build(Payload("Đà Nẵng"), Customer, Today, null).VaBill);
        Assert.Null(VaBillCode.BranchOfPrefix("VAHCM6003585US"));
    }

    [Fact]
    public void Danh_sach_nhap_doc_lai_form_tu_MaVanDon()
    {
        var o = DraftOrders.Build(Payload(), Customer, Today, null);
        o.Id = 850993;
        var lines = LegacyOrderFactory.PackageLines(Payload(), 850993);

        var dto = DraftOrders.ToDto(o, lines, []);

        // Không lưu chi nhánh (VA_Bill NULL) → danh sách để trống, mở lại form mặc định TP.HCM.
        Assert.Equal(("850993", "ready", "Jason Teo", "DHL - Singapore", "", "1 kiện · 2 kg", "15/10/2026"),
            (dto.Id, dto.Stt, dto.Cnee, dto.Service, dto.Branch, dto.Pcs, dto.Date));
        var form = dto.Payload!.Value;
        Assert.Equal("TP.HCM", form.GetProperty("shipper").GetProperty("branch").GetString());
        Assert.Equal("1", form.GetProperty("packages")[0].GetProperty("qty").GetString());
    }
}
