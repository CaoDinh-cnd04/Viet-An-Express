using System.Globalization;
using ClosedXML.Excel;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Import;

/// <summary>1 dòng dữ liệu trong file: số dòng Excel (để khách dò lại) + ô theo tên cột đã chuẩn hoá.</summary>
internal sealed record SheetRow(int Line, IReadOnlyDictionary<string, string> Cells)
{
    public string this[string column] => Cells.TryGetValue(column, out var v) ? v : "";
}

internal sealed record SheetData(IReadOnlyList<string> Headers, IReadOnlyList<SheetRow> Rows);

/// <summary>
/// Đọc file .xlsx "Tạo đơn từ Excel" (Mau_Excel_Tao_Don.xlsx): sheet "DATA" (không có thì sheet đầu),
/// dòng 1 là tên cột, bỏ dòng chú thích (có chữ "bắt buộc") và dòng trống.
/// </summary>
internal static class ExcelSheetReader
{
    public const string DataSheet = "DATA";

    /// <exception cref="InvalidDataException">File không phải .xlsx hợp lệ.</exception>
    public static SheetData Read(Stream xlsx)
    {
        XLWorkbook book;
        try { book = new XLWorkbook(xlsx); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new InvalidDataException("File không đọc được — vui lòng dùng file Excel (.xlsx) theo mẫu.", e);
        }

        using (book)
        {
            var ws = book.Worksheets.FirstOrDefault(w => string.Equals(w.Name.Trim(), DataSheet, StringComparison.OrdinalIgnoreCase))
                     ?? book.Worksheets.First();
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastCol == 0 || lastRow == 0) return new SheetData([], []);

            // Cột trùng tên → lấy cột đầu tiên.
            var columns = new List<(int Index, string Name)>();
            var seen = new HashSet<string>();
            for (var c = 1; c <= lastCol; c++)
            {
                var name = NormalizeHeader(CellText(ws.Cell(1, c)));
                if (name.Length > 0 && seen.Add(name)) columns.Add((c, name));
            }

            var rows = new List<SheetRow>();
            for (var r = 2; r <= lastRow; r++)
            {
                var cells = new Dictionary<string, string>();
                foreach (var (index, name) in columns)
                {
                    var text = CellText(ws.Cell(r, index));
                    if (text.Length > 0) cells[name] = text;
                }
                if (IsBlankOrGuide(cells)) continue;
                rows.Add(new SheetRow(r, cells));
            }
            return new SheetData(columns.Select(c => c.Name).ToList(), rows);
        }
    }

    /// <summary>"Cnee_country_Code " → "cnee_country_code"; "HS_Code" (mẫu cũ, không số) → "hs_code_1".</summary>
    public static string NormalizeHeader(string header)
    {
        var h = string.Concat(header.Trim().ToLowerInvariant().Where(ch => !char.IsWhiteSpace(ch)));
        return h == "hs_code" ? "hs_code_1" : h;
    }

    /// <summary>Dòng trống (bỏ qua STT) hoặc dòng chú thích dưới tiêu đề.</summary>
    private static bool IsBlankOrGuide(Dictionary<string, string> cells) =>
        cells.Where(kv => kv.Key != "stt").All(kv => kv.Value.Length == 0)
        || cells.Values.Any(v => v.Contains("bắt buộc", StringComparison.OrdinalIgnoreCase));

    /// <summary>Số đọc theo dạng bất biến (2.5, 915514144) — không phụ thuộc định dạng hiển thị của Excel.</summary>
    private static string CellText(IXLCell cell)
    {
        var v = cell.Value;
        if (v.IsBlank || v.IsError) return "";
        if (v.IsNumber)
        {
            var n = v.GetNumber();
            return Math.Abs(n) < 1e15 ? ((decimal)n).ToString(CultureInfo.InvariantCulture) : n.ToString("R", CultureInfo.InvariantCulture);
        }
        if (v.IsDateTime) return v.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (v.IsBoolean) return v.GetBoolean() ? "TRUE" : "FALSE";
        return v.GetText().Trim();
    }
}
