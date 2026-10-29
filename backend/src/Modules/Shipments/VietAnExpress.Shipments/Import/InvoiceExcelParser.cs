using System.Globalization;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Import;

/// <summary>1 dòng hàng invoice đọc từ file Excel — cùng tên trường với dòng invoice trên form tạo đơn.</summary>
internal sealed record InvoiceImportItem(
    string DescEn, string DescVi, string Manufacturer, string Origin, string Hs, string Qty, string Unit, string Price);

/// <summary>Kết quả đọc file: dòng hợp lệ + dòng bị bỏ qua kèm lý do ("Dòng 5: …").</summary>
internal sealed record InvoiceImportResult(IReadOnlyList<InvoiceImportItem> Items, IReadOnlyList<string> Skipped);

/// <summary>
/// Đọc file mẫu "MẪU IMPORT INVOICE.xlsx" (Mau_Import_Invoice.xlsx): dòng 1 tên cột STT, Product_en, Product_vn, Description,
/// Country, HS_Code, Quantyti, Unit, Unit_Price; dòng 2 chú thích tiếng Việt (bỏ qua); tối đa 49 dòng hàng. Hàm thuần — có test.
/// </summary>
internal static class InvoiceExcelParser
{
    public const int MaxItems = 49;
    public const int NameMax = 170;
    public const int ManufacturerMax = 300;
    public const int OriginMax = 50;

    /// <summary>ĐVT đếm được — số lượng phải là số nguyên (khớp ExcelOrderParser / frontend lib/units.ts).</summary>
    private static readonly IReadOnlySet<string> CountableUnits = new HashSet<string>(StringComparer.Ordinal) { "PCS", "SET", "BOX", "BAG" };

    /// <summary>Tên cột chấp nhận (đã chuẩn hoá bởi <see cref="ExcelSheetReader.NormalizeHeader"/>), cột đầu tiên có trong file được dùng.</summary>
    private static readonly string[] EnCols = ["product_en", "descriptionen", "description_en"];
    private static readonly string[] ViCols = ["product_vn", "product_vi", "descriptionvn", "description_vn"];
    private static readonly string[] MakerCols = ["description", "manufacturer"];
    private static readonly string[] OriginCols = ["country", "origin"];
    private static readonly string[] HsCols = ["hs_code_1", "hscode", "hs"];
    private static readonly string[] QtyCols = ["quantyti", "quantity", "qty"];
    private static readonly string[] UnitCols = ["unit"];
    private static readonly string[] PriceCols = ["unit_price", "unitprice", "price"];

    /// <summary>File thiếu cột tên hàng tiếng Anh / số lượng / đơn giá → sai mẫu.</summary>
    public static bool HasRequiredColumns(IReadOnlyList<string> headers) =>
        EnCols.Any(headers.Contains) && QtyCols.Any(headers.Contains) && PriceCols.Any(headers.Contains);

    public static InvoiceImportResult Parse(SheetData sheet)
    {
        var items = new List<InvoiceImportItem>();
        var skipped = new List<string>();
        foreach (var row in sheet.Rows)
        {
            // Dòng chú thích dưới tiêu đề: STT không phải số (vd "Số Thứ Tự (Tối Đa 49 dòng mặt hàng)")
            var stt = row["stt"];
            if (stt.Length > 0 && !decimal.TryParse(stt, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) continue;

            string Get(string[] cols) => cols.Select(c => row[c]).FirstOrDefault(v => v.Length > 0)?.Trim() ?? "";
            var en = Get(EnCols);
            var vi = Get(ViCols);
            var maker = Get(MakerCols);
            var origin = Get(OriginCols);
            var hs = Get(HsCols);
            var qty = Get(QtyCols);
            var unit = Get(UnitCols).ToUpperInvariant();
            var price = Get(PriceCols);
            if (new[] { en, vi, maker, hs, qty, price }.All(v => v.Length == 0)) continue;

            if (items.Count >= MaxItems)
            {
                skipped.Add($"Dòng {row.Line}: vượt quá {MaxItems} dòng mặt hàng");
                continue;
            }
            if (Validate(en, vi, maker, origin, qty, unit, price) is { } error)
            {
                skipped.Add($"Dòng {row.Line}: {error}");
                continue;
            }
            items.Add(new InvoiceImportItem(en, vi, maker, origin.Length > 0 ? origin : "VN", hs, Number(qty), unit.Length > 0 ? unit : "PCS", Number(price)));
        }
        return new InvoiceImportResult(items, skipped);
    }

    private static string? Validate(string en, string vi, string maker, string origin, string qty, string unit, string price)
    {
        if (en.Length == 0) return "thiếu tên hàng tiếng Anh (Product_en)";
        if (en.Length > NameMax) return $"tên hàng tiếng Anh quá {NameMax} ký tự";
        if (vi.Length > NameMax) return $"tên hàng tiếng Việt quá {NameMax} ký tự";
        if (maker.Length > ManufacturerMax) return $"thông tin nhà sản xuất quá {ManufacturerMax} ký tự";
        if (origin.Length > OriginMax) return $"nước sản xuất quá {OriginMax} ký tự";
        if (!decimal.TryParse(qty, NumberStyles.Number, CultureInfo.InvariantCulture, out var q) || q <= 0) return "số lượng phải là số > 0";
        if (CountableUnits.Contains(unit.Length > 0 ? unit : "PCS") && q != decimal.Truncate(q)) return "số lượng phải là số nguyên với đơn vị PCS / SET / BOX / BAG";
        if (!decimal.TryParse(price, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) || p < 0) return "đơn giá phải là số";
        if (decimal.Round(p, 2) != p) return "đơn giá tối đa 2 số lẻ";
        return null;
    }

    /// <summary>"7.0" → "7", "15.50" → "15.5" (Excel đọc số dạng thập phân).</summary>
    private static string Number(string value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.InvariantCulture);
}
