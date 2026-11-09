using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

/// <summary>
/// Khách nhắn tiếp trong yêu cầu (chat như bình thường, 06/11/2026 — không cần CS chuyển "Chờ khách phản hồi" trước):
/// nối vào TContact.Content, chuyển "Đang xử lý" (yêu cầu mới giữ "Mới") và báo nhân viên phụ trách khách qua email.
/// </summary>
internal sealed record AnswerSupportRequestCommand(string Id, string? Text) : IRequest<Result<SupportRequestDto>>;

internal sealed class AnswerSupportRequestHandler(IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IBackgroundJobQueue jobs, EmailCompany company, TimeProvider clock, SupportLive live)
    : IRequestHandler<AnswerSupportRequestCommand, Result<SupportRequestDto>>
{
    public async Task<Result<SupportRequestDto>> Handle(AnswerSupportRequestCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var text = c.Text?.Trim() ?? "";
        if (text.Length is 0 or > SupportThread.ReplyMaxLength) return SupportRequestErrors.Reply;
        if (SupportRequest.ParseCode(c.Id) is not { } id) return SupportRequestErrors.NotFound;
        var ticket = await db.SupportRequests.OwnedBy(customerId, user.StaffId).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return SupportRequestErrors.NotFound;

        ticket.AddCustomerReply(text, VietnamTime.Now(clock));
        await db.SaveChangesAsync(ct);
        await live.ChangedAsync(ticket, ct);
        var customer = await customers.GetByIdAsync(customerId, ct);
        var cs = SupportRequestRules.CsRecipients(await customers.GetAccountManagerAsync(customerId, ct));
        jobs.EnqueueEmail($"email {ticket.Code} reply", SupportRequestRules.CustomerReplyEmail(ticket, text, customer, user.UserName, company) with { To = cs });
        return SupportRequestRules.ToDto(ticket);
    }
}
