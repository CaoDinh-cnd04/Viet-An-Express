using System.Net;
using System.Text;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Printing;

/// <summary>Loại chứng từ in — khớp PRINT_DOCUMENTS của frontend (web/src/features/orders/constants.ts).</summary>
internal static class PrintDocs
{
    public const string BillA4 = "bill-a4";
    public const string Invoice = "invoice";
    public const string Cvck = "cvck";
    /// <summary>Nhãn A6: chỉ shipping mark (1 nhãn / kiện để dán thùng).</summary>
    public const string LabelA6 = "label-a6";

    public static readonly string[] All = [BillA4, Invoice, Cvck, LabelA6];

    /// <summary>"bill-a4,invoice" → danh sách loại (bỏ trùng, theo thứ tự chuẩn của <see cref="All"/>); loại lạ giữ lại để validator báo lỗi.</summary>
    public static IReadOnlyList<string> Parse(string? doc)
    {
        var picked = (doc ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        return [.. All.Where(picked.Contains), .. picked.Where(d => !All.Contains(d))];
    }
}
