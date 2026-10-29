using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Printing;

internal sealed record ExportedFile(byte[] Content, string FileName);

/// <summary>Dựng file Excel bảng kê — tách riêng để test.</summary>
internal static class OrderSheet
{
    private static readonly string[] Headers =
    [
        "STT", "Số vận đơn", "Mã hãng", "Số tham chiếu", "Ngày tạo", "Ngày gửi", "Người nhận", "Người liên hệ", "Điện thoại",
        "Địa chỉ", "Thành phố", "Nước đến", "Dịch vụ", "Số kiện", "Cân (kg)", "Nội dung hàng", "Giá trị", "Tiền tệ",
        "Trạng thái", "Ngày giao", "Người ký nhận"
    ];

    public static byte[] Build(
        IReadOnlyList<LegacyOrder> orders, DateTime today, string? customerName, DateOnly? from, DateOnly? to,
        DateTime exportedAt, IReadOnlyDictionary<string, string> statusLabels)
    {
        using var book = new XLWorkbook();
        var ws = book.Worksheets.Add("Bảng kê");

        ws.Cell(1, 1).Value = "Bảng kê gửi hàng";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.FromHtml("#1E6B2C"));
        var sub = new List<string>();
        if (!string.IsNullOrWhiteSpace(customerName)) sub.Add($"Khách hàng: {customerName}");
        if (from is not null || to is not null)
            sub.Add($"Từ ngày {from?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "…"} đến ngày {to?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "…"}");
        sub.Add($"Xuất lúc {exportedAt:dd/MM/yyyy HH:mm} · {orders.Count} đơn");
        ws.Cell(2, 1).Value = string.Join("   ·   ", sub);
        ws.Cell(2, 1).Style.Font.SetFontColor(XLColor.FromHtml("#4D6456"));

        const int headerRow = 4;
        for (var c = 0; c < Headers.Length; c++) ws.Cell(headerRow, c + 1).Value = Headers[c];
        var header = ws.Range(headerRow, 1, headerRow, Headers.Length);
        header.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#2E8B3E"))
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true);

        var r = headerRow;
        var index = 0;
        foreach (var o in orders)
        {
            r++;
            var pod = LegacyOrderView.ParsePod(o.Pod);
            var connect = o.BillConnect?.Trim();
            var bill = LegacyOrderView.BillOf(o);
            object?[] values =
            [
                ++index, bill, connect == bill ? null : connect, o.CustomerBill, o.CreateDate, o.SentDate,
                o.ConsigneeName, o.ConsigneeContactName, o.ConsigneePhone,
                string.Join(", ", new[] { o.ConsigneeAddress1, o.ConsigneeAddress2, o.ConsigneeAddress3 }.Where(s => !string.IsNullOrWhiteSpace(s))),
                o.ConsigneeCity, o.ConsigneeCountry, o.ServiceName?.Replace("|", " - "),
                o.Pieces ?? 1, o.WeightKg ?? 0, o.GoodsName, o.GoodsValue, o.Currency,
                statusLabels.GetValueOrDefault(LegacyOrderStatus.Of(o, today)), pod is null ? null : $"{pod.Date} {pod.Time}".Trim(), pod?.Signer
            ];
            for (var c = 0; c < values.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                cell.Value = values[c] switch
                {
                    null => Blank.Value,
                    int i => i,
                    decimal d => d,
                    DateTime dt => dt,
                    var v => v.ToString()?.Trim()
                };
            }
            // Số vận đơn / mã hãng để dạng chữ — Excel không tự đổi thành số khoa học.
            ws.Cell(r, 2).Style.NumberFormat.Format = "@";
            ws.Cell(r, 3).Style.NumberFormat.Format = "@";
        }

        // Dòng tổng
        var total = r + 1;
        ws.Cell(total, 13).Value = "Tổng cộng";
        if (orders.Count > 0)
        {
            ws.Cell(total, 14).FormulaA1 = $"SUM(N{headerRow + 1}:N{r})";
            ws.Cell(total, 15).FormulaA1 = $"SUM(O{headerRow + 1}:O{r})";
        }
        ws.Range(total, 1, total, Headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E6F4EA"));

        ws.Columns(5, 6).Style.NumberFormat.Format = "dd/mm/yyyy";
        ws.Column(15).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(17).Style.NumberFormat.Format = "#,##0.00";
        ws.Range(headerRow, 1, total, Headers.Length).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorderColor(XLColor.FromHtml("#CCDDD3"));
        if (orders.Count > 0) ws.Range(headerRow, 1, r, Headers.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(headerRow);
        try
        {
            ws.Columns().AdjustToContents(headerRow, total, 8, 45);
        }
        catch (Exception)
        {
            // Máy chủ Linux không có font để đo chữ → đặt độ rộng cố định, vẫn xuất được file.
            ws.Columns().Width = 16;
        }

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }
}
