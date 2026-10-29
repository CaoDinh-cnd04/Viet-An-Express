using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

/// <summary>
/// Việt An trả lời và / hoặc đổi trạng thái. Câu trả lời nối vào TContact.Content (<see cref="SupportThread"/>), khách xem ở trang Hỗ trợ &amp; góp ý —
/// không gửi thông báo. Chuyển "Chờ khách phản hồi" thì khách trả lời lại được.
/// </summary>
internal sealed record ReplySupportRequestCommand(string Id, string? Reply, string? Status) : IRequest<Result<AdminSupportRequestDto>>;

internal sealed class ReplySupportRequestHandler(IdentityDbContext db, ICustomersApi customers, TimeProvider clock)
    : IRequestHandler<ReplySupportRequestCommand, Result<AdminSupportRequestDto>>
{
    public async Task<Result<AdminSupportRequestDto>> Handle(ReplySupportRequestCommand c, CancellationToken ct)
    {
        if (c.Status is { } status && !SupportRequestStatus.All.Contains(status)) return SupportRequestErrors.Status;
        var reply = c.Reply?.Trim() ?? "";
        if (reply.Length > SupportThread.ReplyMaxLength) return SupportRequestErrors.Reply;
        if (SupportRequest.ParseCode(c.Id) is not { } id) return SupportRequestErrors.NotFound;
        var ticket = await db.SupportRequests.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return SupportRequestErrors.NotFound;
        // Lời nhắn từ trang chủ: khách chưa có tài khoản để xem trả lời → chỉ đổi trạng thái.
        if (ticket.Kind == SupportRequestKind.Contact && reply.Length > 0) return SupportRequestErrors.ContactReply;

        var now = VietnamTime.Now(clock);
        var changed = c.Status is { } next && ticket.ChangeStatus(next, now);
        if (!changed && reply.Length == 0) return SupportRequestErrors.NothingToSend;
        if (reply.Length > 0) ticket.AddCsReply(reply, now);
        await db.SaveChangesAsync(ct);
        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct);
        return SupportRequestRules.ToAdminDto(ticket, customer?.Code);
    }
}
