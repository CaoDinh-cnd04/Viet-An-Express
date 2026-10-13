using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Domain;
using VietAnExpress.Shipments.Application.Orders.Documents;
using VietAnExpress.Shipments.Infrastructure.Legacy;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class ShippingWeightTests
{
    private static OrderPayload.PackagePart Package(string weight, string length = "20", string width = "30", string height = "40", string qty = "1") =>
        new() { Qty = qty, Weight = weight, Length = length, Width = width, Height = height };

    [Theory]
    [InlineData("")]
    [InlineData("DHL")]
    [InlineData("Fedex")]
    [InlineData("UPS")]
    [InlineData("Aramex")]
    [InlineData("SEA")]
    [InlineData("Chuyên tuyến")]
    public void Lay_max_tung_kien_roi_cong_giu_he_so_5000(string carrier)
    {
        Assert.Equal(5000m, ShippingRules.VolumetricDivisor);
        var p = new OrderPayload { Service = new() { Carrier = carrier }, Packages = [Package("6"), Package("2", "30", "50", "10")] };
        var order = LegacyOrderFactory.FromPayload(p, new(1, "Test", null, null, null), 100, new DateTime(2026, 10, 13));
        Assert.Equal(9m, order.WeightKg);
        var lines = LegacyOrderFactory.PackageLines(p, 100);
        Assert.Equal(8m, lines.Sum(x => x.WeightKg));
        Assert.Equal(7.8m, lines.Sum(x => x.VolumetricKg));
        Assert.Equal(9m, lines.Sum(x => x.ChargeableKg));
    }

    [Theory]
    [InlineData("UPS - VN Saver", "1.1", "3", 4.5)]
    [InlineData("UPS|VN Expedited", "1.1", "3", 6)]
    [InlineData("DHL - VN", "1.1", "3", 4.5)]
    [InlineData("Fedex - VN", "1.1", "3", 3.5)]
    [InlineData("Chuyên tuyến - Singapore", "1.1", "3", 3.3)]
    [InlineData("UPS - VN Saver", "1.5001", "1", 2)]
    [InlineData("UPS - VN Saver", "1.5", "1", 1.5)]
    public void Lam_tron_dung_tung_kien_hay_tong_lo(string service, string weight, string qty, double expected) =>
        Assert.Equal((decimal)expected, LegacyOrderFactory.ChargeableWeight([Package(weight, "", "", "", qty)], 0, service));

    [Theory]
    [InlineData("DHL", 30, 30)]
    [InlineData("DHL", 30.1, 31)]
    [InlineData("DHL", 31.5, 32)]
    [InlineData("Fedex - VN", 20.1, 20.5)]
    [InlineData("Fedex - VN", 20.51, 21)]
    [InlineData("Fedex - SIN 3 (FICP)", 31.1, 31.5)]
    [InlineData("Fedex - SIN 3 (FICP)", 32.51, 33)]
    public void Lam_tron_tong_dung_nguong(string service, double total, double expected) =>
        Assert.Equal((decimal)expected, ShippingRules.ShipmentWeight((decimal)total, ShippingRules.Rule(service)));

    [Fact]
    public void Kich_thuoc_lam_tron_dung_hang_va_so_quy_doi_da_luu_duoc_giu_khi_xem_in()
    {
        Assert.Equal(20m * 21 * 21 / 5000, ShippingRules.Volume(20.1m, 20.5m, 20.9m, ShippingRules.Rule("UPS")));
        Assert.Equal(21m * 21 * 21 / 5000, ShippingRules.Volume(20.1m, 20.5m, 20.9m, ShippingRules.Rule("Fedex")));
        var stored = new LegacyPackageLine { Quantity = 2, LengthCm = 21, WidthCm = 21, HeightCm = 21, WeightKg = 2, VolumetricKg = 3.45m, ChargeableKg = 4 };
        Assert.Equal(3.45m, LegacyOrderView.ToPackageDto(stored).VolumetricWeight);
        Assert.Equal(4m, LegacyOrderView.ToPackageDto(stored).ChargeableWeight);
        var print = LegacyOrderLinesReader.ToPrint(stored);
        Assert.Equal(3.45m, print.VolumeKg);
        Assert.All(BillA4.PieceList([print], 2), piece => Assert.Equal(1.725m, piece!.VolumeKg));
    }
}
