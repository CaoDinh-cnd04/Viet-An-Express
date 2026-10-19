using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Application.Orders.Documents;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Legacy;
using VietAnExpress.Shipments.Domain;

namespace VietAnExpress.Shipments.Application.Orders;

// ---------- In chứng từ (bill A4, invoice, CVCK, nhãn A6) ----------

/// <summary>Trả trang HTML sẵn để in cho 1 hoặc nhiều đơn (≤ <see cref="MaxBills"/>).</summary>
internal sealed record PrintOrdersQuery(IReadOnlyList<string> Bills, string Doc) : IRequest<Result<string>>
{
    public const int MaxBills = 100;
}

internal sealed class PrintOrdersHandler(
    ShipmentsDbContext db, OrderAccess access, IOptions<CompanyInfo> company, TimeProvider clock)
    : IRequestHandler<PrintOrdersQuery, Result<string>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<string>> Handle(PrintOrdersQuery q, CancellationToken ct)
    {
        var bills = q.Bills.Select(b => b.Trim()).Where(b => b.Length > 0).Distinct().ToList();
        var numbers = bills
            .Select(VaBillCode.Number)
            .OfType<long>()
            .ToList();

        var orders = await access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct))
            .Where(o => (o.OrderNumber != null && numbers.Contains(o.OrderNumber.Value)) || (o.BillConnect != null && bills.Contains(o.BillConnect)))
            .ToListAsync(ct);
        if (orders.Count == 0) return OrderErrors.NotFound(string.Join(", ", bills));

        // Kiện / dòng hàng từ 2 bảng chi tiết dùng chung với hệ thống cũ.
        var lines = await LegacyOrderLinesReader.LoadAsync(db, orders, ct);
        // Giữ đúng thứ tự khách chọn.
        var models = bills
            .Select(b => orders.FirstOrDefault(o => LegacyOrderView.BillOf(o) == b || (o.OrderNumber is { } n && VaBillCode.Number(b) == n) || o.BillConnect == b))
            .OfType<LegacyOrder>()
            .DistinctBy(o => o.Id)
            .Select(o => lines.TryGetValue(o.Id, out var l) ? new OrderPrintModel(o, l.Items, l.Packages) : new OrderPrintModel(o, []))
            .ToList();

        var now = VietnamTime.Now(clock);
        return OrderDocumentRenderer.RenderMany(PrintDocs.Parse(q.Doc), models, company.Value, now);
    }

}

// ---------- Xuất bảng kê gửi hàng (Excel) ----------

internal sealed record ExportedFile(byte[] Content, string FileName);

/// <summary>Cùng bộ lọc với danh sách đơn; tối đa <see cref="MaxRows"/> dòng.</summary>
internal sealed record ExportOrdersQuery(GetOrdersQuery Filters) : IRequest<ExportedFile>
{
    public const int MaxRows = 10_000;
}

internal sealed class ExportOrdersHandler(ShipmentsDbContext db, OrderAccess access, TimeProvider clock)
    : IRequestHandler<ExportOrdersQuery, ExportedFile>
{
    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        [LegacyOrderStatus.Waiting] = "Chưa đi",
        [LegacyOrderStatus.InTransit] = "Đã đi",
        [LegacyOrderStatus.NotDelivered] = "Chưa phát",
        [LegacyOrderStatus.Delivered] = "Đã phát"
    };

    public async Task<ExportedFile> Handle(ExportOrdersQuery q, CancellationToken ct)
    {
        var f = q.Filters;
        var nowVn = VietnamTime.ToVietnam(clock.GetUtcNow());
        var today = nowVn.Date;

        var query = OrderListFilter.Apply(access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct)), f);
        query = OrderListFilter.ApplyStatus(query, f.Status, today);
        var orders = await OrderListFilter.Sort(query, f.SortBy, f.SortDir).Take(ExportOrdersQuery.MaxRows).ToListAsync(ct);

        var customer = orders.Select(o => o.CustomerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return new ExportedFile(
            OrderSheet.Build(orders, today, access.IsCustomer ? customer : null, f.FromDate, f.ToDate, nowVn.DateTime, StatusLabels),
            $"bang-ke-gui-hang-{nowVn:yyyyMMdd-HHmm}.xlsx");
    }
}

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
