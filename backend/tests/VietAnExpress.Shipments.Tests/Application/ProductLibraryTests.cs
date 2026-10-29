using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.OrderData;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class ProductLibraryTests
{
    private static LegacyInvoiceLine Line(long id, int orderId, string en, decimal price, string? vi = "Đầm", string? hs = "62044220", string? unit = "PCS") => new()
    {
        Id = id, OrderId = orderId, DescriptionEn = en, DescriptionVi = vi, HsCode = hs, Unit = unit, Origin = "VN",
        Manufacturer = "LE GA CO., LTD", Quantity = 2, UnitPrice = price, Currency = "USD"
    };

    [Fact]
    public void Moi_mat_hang_1_dong_gia_lan_khai_gan_nhat_moi_dung_truoc()
    {
        // mới nhất trước (Id giảm dần)
        var lines = new[]
        {
            Line(9, 3, "Dress", 12.5m),
            Line(8, 3, "Scarf", 5m, vi: null, hs: null, unit: null),
            Line(5, 2, "DRESS ", 10m),        // cùng mặt hàng (khác hoa thường, khoảng trắng) → bỏ
            Line(4, 2, "Dress", 10m, hs: "62044290"), // khác HS → mặt hàng khác
            new() { Id = 1, OrderId = 1 }     // dòng không tên → bỏ
        };

        var library = ProductLibrary.Build(lines, max: 10);

        Assert.Equal(["Dress", "Scarf", "Dress"], library.Select(p => p.DescEn));
        Assert.Equal("12.5", library[0].Price);
        Assert.Equal(("PCS", "", ""), (library[1].Unit, library[1].Hs, library[1].DescVi));
        Assert.Equal("62044290", library[2].Hs);
        Assert.Single(ProductLibrary.Build(lines, max: 1));
    }

    [Fact]
    public void Khoa_mat_hang_on_dinh_khong_phan_biet_hoa_thuong()
    {
        var a = ProductLibrary.ToDto(Line(9, 3, "Dress", 12.5m));
        var b = ProductLibrary.ToDto(Line(5, 2, " DRESS", 10m));

        Assert.Equal(a.Id, b.Id);
        Assert.True(ProductLibrary.IsValidKey(a.Id));
        Assert.NotEqual(a.Id, ProductLibrary.ToDto(Line(4, 2, "Dress", 10m, hs: "62044290")).Id);
        Assert.False(ProductLibrary.IsValidKey("9"));
        Assert.False(ProductLibrary.IsValidKey(a.Id.ToUpperInvariant()));
    }

    [Fact]
    public void Mat_hang_yeu_thich_len_dau_mat_hang_da_xoa_bi_an()
    {
        var lines = new[] { Line(9, 3, "Dress", 12.5m), Line(8, 3, "Scarf", 5m), Line(7, 3, "Shirt", 8m) };
        var key = (string en) => ProductLibrary.ToDto(lines.First(l => l.DescriptionEn == en)).Id;
        var marks = new Dictionary<string, ProductMark>
        {
            [key("Shirt")] = new(IsFavorite: true, IsDeleted: false),
            [key("Scarf")] = new(IsFavorite: false, IsDeleted: true)
        };

        var library = ProductLibrary.Build(lines, max: 10, marks);

        Assert.Equal(["Shirt", "Dress"], library.Select(p => p.DescEn));
        Assert.Equal([true, false], library.Select(p => p.IsFavorite));
    }

    [Fact]
    public void Invoice_cu_gom_theo_don_moi_nhat_truoc()
    {
        var newer = new LegacyOrder { Id = 3, OrderNumber = 90000003, ConsigneeName = "LINEX", CreateDate = new DateTime(2026, 9, 29), Currency = "SGD" };
        var older = new LegacyOrder { Id = 2, OrderNumber = 90000002, ConsigneeName = "ACME", CreateDate = new DateTime(2026, 9, 1) };
        var lines = new[]
        {
            new CustomerInvoiceLine(Line(9, 3, "Scarf", 5m), newer),
            new CustomerInvoiceLine(Line(7, 3, "Dress", 12.5m), newer),
            new CustomerInvoiceLine(Line(5, 2, "Shirt", 8m), older)
        };

        var recent = ProductLibrary.Recent(lines, max: 10);

        Assert.Equal(["90000003", "90000002"], recent.Select(r => r.Bill));
        Assert.Equal(("LINEX", "29/09/2026", "USD"), (recent[0].Cnee, recent[0].Date, recent[0].Currency));
        Assert.Equal(["Dress", "Scarf"], recent[0].Items.Select(i => i.DescEn)); // giữ thứ tự khai trong đơn
        Assert.Single(ProductLibrary.Recent(lines, max: 1));
    }
}
