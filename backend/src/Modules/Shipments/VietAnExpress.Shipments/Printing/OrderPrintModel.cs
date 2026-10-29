using System.Net;
using System.Text;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Printing;

/// <summary>1 dòng hàng trên invoice / CVCK. <paramref name="Description"/> là tên tiếng Anh.</summary>
/// <paramref name="Package"/>: kiện số mấy khi đơn khai invoice riêng từng kiện; null = invoice chung.
internal sealed record PrintItem(string Description, decimal Quantity, string Unit, decimal UnitPrice, string? HsCode, string? Origin,
    string? DescriptionVi = null, string? Manufacturer = null, int? Package = null)
{
    public decimal Amount => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    /// <summary>"Tên tiếng Anh / Tên tiếng Việt" như mẫu hệ thống cũ; thiếu tên Việt thì chỉ tên Anh.</summary>
    public string Name(string separator = " / ") =>
        string.IsNullOrWhiteSpace(DescriptionVi) ? Description.Trim() : $"{Description.Trim()}{separator}{DescriptionVi.Trim()}";
}

/// <summary>
/// Dữ liệu 1 đơn để in: dòng dbo.MaVanDon + dòng hàng và dòng kiện (nếu đơn tạo trên portal còn giữ form).
/// </summary>
internal sealed record OrderPrintModel(LegacyOrder Order, IReadOnlyList<PrintItem> Items, IReadOnlyList<PrintPackage>? PackageLines = null)
{
    public IReadOnlyList<PrintPackage> Packages => PackageLines ?? [];

    public string Bill => LegacyOrderView.BillOf(Order);

    /// <summary>Tổng số kiện / cân thực: theo dòng kiện nếu có, không thì theo dbo.MaVanDon.</summary>
    public int TotalPieces => Packages.Count > 0 ? Packages.Sum(p => p.Qty) : Order.Pieces ?? 1;
    public decimal TotalGrossKg => Packages.Count > 0 ? Packages.Sum(p => p.GrossKg) : Order.WeightKg ?? 0;

    /// <summary>Nước người gửi in trên chứng từ — đơn nội địa (chưa khai hoặc mã nước 231) là "Viet Nam".</summary>
    public string SenderCountryText => Order.SenderCountryId is null or 231 ? "Viet Nam" : "";

    /// <summary>Đơn khai invoice riêng từng kiện (có dòng hàng mang số kiện) → in mỗi kiện 1 invoice.</summary>
    public bool InvoicePerPackage => Items.Any(i => i.Package is not null);

    /// <summary>Dòng kiện tách thành từng kiện (SL 3 → 3 kiện SL 1), theo thứ tự: kiện số n là phần tử thứ n - 1.</summary>
    public IReadOnlyList<PrintPackage> SinglePackages =>
        [.. Packages.SelectMany(p => Enumerable.Repeat(p with { Qty = 1, StoredVolumeKg = p.Qty > 0 && p.StoredVolumeKg is { } v ? v / p.Qty : null },
            Math.Max(0, p.Qty)))];

    /// <summary>Không có chi tiết hàng (đơn hệ thống cũ) → 1 dòng từ tên hàng, số kiện, giá trị tổng.</summary>
    public IReadOnlyList<PrintItem> InvoiceLines
    {
        get
        {
            if (Items.Count > 0) return Items;
            var qty = Math.Max(1, Order.Pieces ?? 1);
            var total = Order.GoodsValue ?? 0;
            return [new PrintItem(Order.GoodsName ?? "Goods", qty, "PCS", decimal.Round(total / qty, 2), null, "Vietnam")];
        }
    }
}

/// <summary>1 dòng kiện (từ form đơn portal): số kiện cùng kích thước, D×R×C (cm), cân 1 kiện (kg).</summary>
internal sealed record PrintPackage(int Qty, decimal Length, decimal Width, decimal Height, decimal WeightKg, decimal? StoredVolumeKg = null)
{
    public decimal GrossKg => Qty * WeightKg;
    /// <summary>Cân quy đổi = D×R×C / 5000 cho mỗi kiện.</summary>
    public decimal VolumeKg => StoredVolumeKg ?? Qty * Length * Width * Height / BillA4.VolumeDivisor;
}
