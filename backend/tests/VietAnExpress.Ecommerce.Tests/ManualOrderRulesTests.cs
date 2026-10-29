using VietAnExpress.Ecommerce.Orders;
using Xunit;

namespace VietAnExpress.Ecommerce.Tests;

public class ManualOrderRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 9, 0, 0);

    private static ManualOrderInput Input(string? source = "manual", IReadOnlyList<ManualProductInput>? products = null, decimal kg = 1.2345m) => new(
        " SHOP-001 ", source, null, "Jane Doe", "0400 000 000", null, "Australia", "au", "2000", "Sydney", "NSW", "1 Main St", "DHL", "SGN", kg,
        products ?? [new("T-shirt", "TS-1", 2, 5, 12.5m, "6109"), new("Hat", null, 1, 3, 7, " ")],
        new() { ["ioss"] = "IM123", ["eori"] = " " });

    [Fact]
    public void Build_chuan_hoa_don_hop_le()
    {
        var r = ManualOrderRules.Build(Input(), Now);
        Assert.True(r.IsSuccess);
        var (source, order, shipping) = r.Value;
        Assert.Equal("manual", source);
        Assert.Equal("SHOP-001", order.OrderName);
        Assert.Equal("AU", order.Recipient.CountryCode);
        Assert.Equal("+61400000000", order.Recipient.Phone);
        Assert.Equal(3, order.ItemCount);
        Assert.Equal(1.235m, order.WeightKg);
        Assert.Equal(32m, order.TotalAmount);
        Assert.Contains("\"hsCode\":\"6109\"", order.ProductsJson);
        Assert.Equal("{\"ioss\":\"IM123\"}", shipping.CustomsJson);
        Assert.Equal(Now, order.PlacedAt);
    }

    [Fact]
    public void Build_khong_can_thi_de_trong_va_nguon_trong_la_nhap_tay()
    {
        var r = ManualOrderRules.Build(Input(source: null, kg: 0), Now);
        Assert.Equal("manual", r.Value.Source);
        Assert.Null(r.Value.Order.WeightKg);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("excel")]
    public void Build_tu_choi_nguon_khong_phai_nhap_tay(string source) =>
        Assert.True(ManualOrderRules.Build(Input(source), Now).IsFailure);

    [Fact]
    public void Build_tu_choi_san_pham_khong_hop_le()
    {
        Assert.True(ManualOrderRules.Build(Input(products: []), Now).IsFailure);
        Assert.True(ManualOrderRules.Build(Input(products: [new("", null, 1, 0, 0, null)]), Now).IsFailure);
        Assert.True(ManualOrderRules.Build(Input(products: [new("A", null, 0, 0, 0, null)]), Now).IsFailure);
        Assert.True(ManualOrderRules.Build(Input(products: [.. Enumerable.Range(0, 6).Select(i => new ManualProductInput($"P{i}", null, 1, 0, 0, null))]), Now).IsFailure);
    }
}
