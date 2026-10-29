using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

/// <summary>
/// Khách trả lời yêu cầu đang "Chờ khách phản hồi": nối vào TContact.Content, chuyển lại "Đang xử lý" và báo hộp thư Việt An qua email.
/// </summary>
internal sealed record AnswerSupportRequestCommand(string Id, string? Text) : IRequest<Result<SupportRequestDto>>;

internal sealed class AnswerSupportRequestHandler(IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IBackgroundJobQueue jobs, EmailCompany company, TimeProvider clock)
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
        if (ticket.Status != SupportRequestStatus.WaitCustomer) return SupportRequestErrors.NotWaiting;

        ticket.AddCustomerReply(text, VietnamTime.Now(clock));
        await db.SaveChangesAsync(ct);
        var customer = await customers.GetByIdAsync(customerId, ct);
        jobs.EnqueueEmail($"email {ticket.Code} reply", SupportRequestRules.CustomerReplyEmail(ticket, text, customer, user.UserName, company));
        return SupportRequestRules.ToDto(ticket);
    }
}
