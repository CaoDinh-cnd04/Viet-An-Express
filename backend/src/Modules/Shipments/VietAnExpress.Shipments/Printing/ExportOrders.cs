using System.Text.Json;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Printing;

/// <summary>
/// Cùng bộ lọc với danh sách đơn. Có Page + PageSize → chỉ xuất đúng trang đó (khớp bảng đang xem);
/// không có → xuất toàn bộ, tối đa <see cref="MaxRows"/> dòng.
/// </summary>
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
        IQueryable<LegacyOrder> sorted = OrderListFilter.Sort(query, f.SortBy, f.SortDir);
        if (f.Page is not null && f.PageSize is not null)
        {
            var (page, size) = Paging.Normalize(f.Page, f.PageSize);
            sorted = sorted.Skip((int)Math.Min((long)(page - 1) * size, int.MaxValue)).Take(size);
        }
        else sorted = sorted.Take(ExportOrdersQuery.MaxRows);
        var orders = await sorted.ToListAsync(ct);

        var customer = orders.Select(o => o.CustomerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return new ExportedFile(
            OrderSheet.Build(orders, today, access.IsCustomer ? customer : null, f.FromDate, f.ToDate, nowVn.DateTime, StatusLabels),
            $"bang-ke-gui-hang-{nowVn:yyyyMMdd-HHmm}.xlsx");
    }
}
