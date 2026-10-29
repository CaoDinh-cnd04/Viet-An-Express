using System.Globalization;
using System.Net;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Printing;

/// <summary>Tiện ích định dạng dùng chung cho invoice / CVCK — mọi dữ liệu do khách nhập đều HtmlEncode.</summary>
internal static class Doc
{
    public static string H(string? value) => WebUtility.HtmlEncode(value?.Trim() ?? "");

    public static string Join(string separator, params string?[] parts) =>
        string.Join(separator, parts.Select(p => p?.Trim()).Where(p => !string.IsNullOrEmpty(p)));

    public static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";

    public static string Qty(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Money(decimal? value) => (value ?? 0).ToString("#,##0.00", CultureInfo.InvariantCulture);
}
