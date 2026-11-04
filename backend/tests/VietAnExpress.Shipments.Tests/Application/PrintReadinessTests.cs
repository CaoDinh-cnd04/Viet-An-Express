using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

/// <summary>In &amp; cấp bill từ trang Đơn nháp kiểm tra đủ ô bắt buộc như form tạo đơn.</summary>
public class PrintReadinessTests
{
    private static readonly LegacyCustomerRef Customer = new(201008, "SGB EXPRESS HN", "Đức Anh", "0977714964", null);

    private static OrderPayload Full(string hub = "Fedex - Priority", string country = "Singapore", string countryCode = "", string state = "") => new()
    {
        Shipper = new() { Company = "SGB EXPRESS HN", Contact = "Đức Anh", Tel = "0977714964", Address = "241 Tân Xuân, HN", Branch = "TP.HCM" },
        Service = new() { Carrier = "Fedex", Hub = hub },
        Shipment = new() { Type = "PACK", Pieces = "1", GrossWeight = "2" },
        Receiver = new()
        {
            Company = "Jason Teo", Contact = "Jason", Tel = "6591234567", Country = country, CountryCode = countryCode, State = state, City = "SINGAPORE",
            Addr1 = "8 Bhamo Road", Addr2 = "Unit 01"
        },
        Goods = new() { Description = "Watch strap" },
        Packages = [new() { Qty = "1", Weight = "2", Packaging = "Thùng carton", Category = LegacyOrderFactory.MultiCategory }],
        Invoice = new()
        {
            ExportType = "Sample", Items = [new() { DescEn = "Watch strap", DescVi = "Dây đồng hồ", Qty = "1", Unit = "PCS", Price = "5" }]
        }
    };

    [Fact]
    public void Don_du_thong_tin_thi_in_duoc() => Assert.Empty(PrintReadiness.Missing(Full()));

    [Theory]
    [InlineData("United States", "")]
    [InlineData("Australia", "")]
    [InlineData("Canada", "CA")]
    public void My_Canada_Uc_bat_buoc_tinh_bang(string country, string code)
    {
        Assert.Equal(["Tỉnh / bang (bắt buộc với Mỹ, Canada, Úc)"], PrintReadiness.Missing(Full(country: country, countryCode: code)));
        Assert.Empty(PrintReadiness.Missing(Full(country: country, countryCode: code, state: "NSW")));
        Assert.False(ReceiverStateRule.Required("SG", "Singapore"));
    }

    [Fact]
    public void Chua_chon_hub_thi_khong_in_duoc()
    {
        Assert.Equal(["Hub"], PrintReadiness.Missing(Full(hub: "")));

        // Nháp lưu khi chưa chọn hub: cột Dich_Vu chỉ có tên hãng → không phải "Chờ in".
        var draft = DraftOrders.Build(Full(hub: ""), Customer, new DateTime(2026, 11, 2), null);
        Assert.Equal("Fedex", draft.ServiceName);
        Assert.False(DraftOrders.IsReady(draft));
        Assert.True(DraftOrders.IsReady(DraftOrders.Build(Full(), Customer, new DateTime(2026, 11, 2), null)));
    }

    [Fact]
    public void Bao_ten_cac_o_con_thieu()
    {
        var p = Full();
        var missing = PrintReadiness.Missing(new OrderPayload
        {
            Shipper = p.Shipper, Service = p.Service, Shipment = p.Shipment, Goods = new(), Packages = [],
            Receiver = new() { Company = "Jason Teo", Country = "Singapore", Addr1 = "8 Bhamo Road" },
            Invoice = new() { Items = [new() { DescEn = "Strap", Qty = "1", Unit = "PCS", Price = "" }] }
        });

        Assert.Equal(
            ["Người liên hệ (người nhận)", "Điện thoại người nhận", "Thành phố", "Địa chỉ 2", "Mô tả hàng hóa", "Chi tiết kiện hàng",
             "Hình thức xuất khẩu", "Dòng hàng invoice (tên EN / VN, số lượng, đơn vị, đơn giá)"],
            missing);
    }

    [Fact]
    public void Chung_tu_chi_can_so_kien_va_can()
    {
        var p = Full();
        var doc = new OrderPayload
        {
            Shipper = p.Shipper, Service = p.Service, Receiver = p.Receiver,
            Shipment = new() { Type = "DOC", Pieces = "1", GrossWeight = "0.5" }
        };
        Assert.Empty(PrintReadiness.Missing(doc));
        Assert.Equal(["Cân nặng"], PrintReadiness.Missing(new OrderPayload
        {
            Shipper = p.Shipper, Service = p.Service, Receiver = p.Receiver, Shipment = new() { Type = "DOC", Pieces = "1" }
        }));
    }
}
