using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Infrastructure.Geo;
using VietAnExpress.Shipments.Infrastructure.Legacy;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class RemoteAreaColumnsTests
{
    [Fact]
    public void Ghi_dung_dinh_dang_he_thong_cu()
    {
        var o = new LegacyOrder();
        RemoteAreaColumns.Apply(o, [new RemoteAreaHit("Fedex", "Tier B"), new RemoteAreaHit("UPS", null), new RemoteAreaHit("Au-Post", null)]);
        Assert.Equal(("Fedex (Tier B)", "UPS", "Au-Post"), (o.RemoteAreaFedEx, o.RemoteAreaUps, o.RemoteArea));
    }

    [Fact]
    public void Chi_UPS_thi_cot_khac_de_trong()
    {
        var o = new LegacyOrder();
        RemoteAreaColumns.Apply(o, [new RemoteAreaHit("UPS", null)]); // vd Brazil 12946 (khoảng UPS 12940–12955)
        Assert.Equal(((string?)null, "UPS", (string?)null), (o.RemoteAreaFedEx, o.RemoteAreaUps, o.RemoteArea));
    }

    [Fact]
    public void Ma_nuoc_lay_tu_form_thieu_thi_doi_tu_ten()
    {
        CountryInfo[] countries = [new("BR", "Brazil", "+55"), new("US", "United States", "+1")];
        Assert.Equal("BR", RemoteAreaColumns.CountryCode(new OrderPayload { Receiver = new() { Country = "brazil" } }, countries));
        Assert.Equal("US", RemoteAreaColumns.CountryCode(new OrderPayload { Receiver = new() { CountryCode = "us", Country = "Brazil" } }, countries));
        Assert.Equal("", RemoteAreaColumns.CountryCode(new OrderPayload { Receiver = new() { Country = "Atlantis" } }, countries));
    }

    [Fact]
    public void Chi_tiet_don_hien_VSVX_moi_hang()
    {
        var o = new LegacyOrder { RemoteAreaFedEx = "Fedex (Tier B)", RemoteAreaUps = "UPS", RemoteArea = "Au-Post" };
        Assert.Equal("Fedex (Tier B), UPS, Au-Post", LegacyOrderView.RemoteAreaText(o));
        Assert.Equal("UPS", LegacyOrderView.RemoteAreaText(new LegacyOrder { RemoteAreaUps = " UPS " }));
        Assert.Null(LegacyOrderView.RemoteAreaText(new LegacyOrder()));
    }

    [Fact]
    public void Ma_buu_chinh_dinh_nhieu_hang_tra_ve_du_cac_hang()
    {
        var rows = new[]
        {
            new RemoteAreaRow("UPS", null, "5505", "5505", null),
            new RemoteAreaRow("Fedex", null, "5505", "5505", "Tier B"),
            new RemoteAreaRow("Au-Post", null, "9999", "9999", null),
        };
        var hits = RemoteAreaMatcher.Match(rows, "AT", "5505", null);
        Assert.Equal([new RemoteAreaHit("Fedex", "Tier B"), new RemoteAreaHit("UPS", null)], hits);
    }
}
