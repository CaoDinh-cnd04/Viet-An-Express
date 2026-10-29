using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Printing;

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
