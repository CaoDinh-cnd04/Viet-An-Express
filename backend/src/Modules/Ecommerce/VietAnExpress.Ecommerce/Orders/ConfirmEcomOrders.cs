using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.Ecommerce.Stores;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Orders;

/// <summary>
/// Xác nhận gửi (chuyển sang "Đơn hàng của tôi") hoặc trả về tab Đơn hàng — nhiều đơn một lần.
/// Đơn còn thiếu trường bắt buộc không được xác nhận và được báo lại trong câu kết quả.
/// </summary>
internal sealed record ConfirmEcomOrdersCommand(IReadOnlyList<string>? Ids, bool Confirm) : IRequest<Result<ConfirmOrdersResult>>;

internal sealed class ConfirmEcomOrdersHandler(EcommerceDbContext db, ICurrentUser user, TimeProvider clock) :
    IRequestHandler<ConfirmEcomOrdersCommand, Result<ConfirmOrdersResult>>
{
    private const int MaxPerRequest = 500;

    public async Task<Result<ConfirmOrdersResult>> Handle(ConfirmEcomOrdersCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return StoreErrors.NotLoggedIn;
        var ids = (c.Ids ?? []).Select(s => long.TryParse(s, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return Error.Validation("ECOM_SELECT_EMPTY", "Chọn đơn trước");
        if (ids.Count > MaxPerRequest) return Error.Validation("ECOM_SELECT_TOO_MANY", $"Mỗi lần tối đa {MaxPerRequest} đơn");

        var orders = await db.MarketplaceOrders.VisibleTo(user, customerId).Where(o => o.DeletedAt == null && ids.Contains(o.Id)).ToListAsync(ct);
        if (orders.Count == 0) return Error.NotFound("ECOM_ORDER_NOT_FOUND", "Không tìm thấy đơn");

        var now = VietnamTime.Now(clock);
        if (!c.Confirm)
        {
            var returned = orders.Count(o => o.Unconfirm(now));
            await db.SaveChangesAsync(ct);
            var kept = orders.Count - returned;
            return new ConfirmOrdersResult(
                kept == 0 ? $"Đã trả {returned} đơn về tab Đơn hàng" : $"Đã trả {returned} đơn về tab Đơn hàng; {kept} đơn đã có bill được giữ lại",
                returned);
        }

        var incomplete = orders.Where(o => o.ConfirmedAt is null && OrderData.Issues(o).Count > 0).ToList();
        var confirmed = orders.Except(incomplete).Count(o => o.Confirm(now));
        await db.SaveChangesAsync(ct);

        var message = $"Đã xác nhận gửi {confirmed} đơn";
        if (incomplete.Count > 0)
        {
            var names = string.Join(", ", incomplete.Take(5).Select(o => o.OrderName)) + (incomplete.Count > 5 ? "…" : "");
            message += $"; {incomplete.Count} đơn còn thiếu thông tin ({names}), bổ sung rồi xác nhận lại";
        }
        return new ConfirmOrdersResult(message, confirmed);
    }
}

/// <summary>Frontend đọc thẳng <c>message</c>, <c>count</c>.</summary>
internal sealed record ConfirmOrdersResult(string Message, int Count);
