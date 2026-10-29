using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Contracts;

namespace VietAnExpress.Identity.Support;

internal sealed record CreateSupportRequestCommand(NewSupportRequest Request, SupportRequestFile? File = null) : IRequest<Result<SupportRequestDto>>;

internal sealed class CreateSupportRequestHandler(IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IShipmentsApi shipments, IBackgroundJobQueue jobs, EmailCompany company, TimeProvider clock)
    : IRequestHandler<CreateSupportRequestCommand, Result<SupportRequestDto>>
{
    public async Task<Result<SupportRequestDto>> Handle(CreateSupportRequestCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var r = c.Request;
        if (SupportRequestRules.Validate(r, c.File) is { } error) return error;
        var kind = SupportRequestRules.KindOf(r);

        // Có mã vận đơn → phải là vận đơn của khách; lấy người nhận / nước từ vận đơn nếu khách không nhập.
        string? bill = null, consignee = null, country = null;
        if (kind == SupportRequestKind.Support && r.Bill?.Trim() is { Length: > 0 } typed)
        {
            var found = await shipments.FindBillAsync(customerId, typed, ct);
            if (found is null) return SupportRequestErrors.BillNotFound;
            bill = found.Bill;
            consignee = SupportRequestRules.Cut(string.IsNullOrWhiteSpace(r.Cnee) ? found.ConsigneeName : r.Cnee, SupportRequest.NameMaxLength);
            country = SupportRequestRules.Cut(string.IsNullOrWhiteSpace(r.Ct) ? found.ConsigneeCountry : r.Ct, SupportRequest.CountryMaxLength);
        }

        var customer = await customers.GetByIdAsync(customerId, ct);
        var userName = user.UserName ?? "";
        var contact = r.Contact?.Trim() ?? "";
        var ticket = SupportRequest.Create(kind, customerId, customer?.CompanyName ?? "", user.StaffId,
            bill, consignee, country,
            kind == SupportRequestKind.Feedback ? SupportRequestKind.FeedbackKeyWord : r.Type!.Trim(),
            kind == SupportRequestKind.Feedback ? (string.IsNullOrWhiteSpace(r.Type) ? SupportRequestRules.FeedbackType : r.Type.Trim())
                : string.IsNullOrWhiteSpace(r.Subject) ? null : r.Subject.Trim(),
            r.Lv ?? "low", r.Desc!.Trim(),
            string.IsNullOrWhiteSpace(r.Req) ? userName : r.Req.Trim(),
            contact, SupportRules.IsEmail(contact) ? contact : null, VietnamTime.Now(clock));
        var file = c.File is { } f ? f with { Name = SupportRules.SafeFileName(f.Name) } : null;
        if (file is not null) ticket.NoteAttachment(file.Name);
        db.SupportRequests.Add(ticket);
        await db.SaveChangesAsync(ct);
        // Báo về hộp thư Việt An ở nền (kèm tệp) — yêu cầu đã lưu ở dbo.TContact, xử lý ở phần mềm cũ / trang /admin.
        jobs.EnqueueEmail($"email {ticket.Code}", SupportRequestRules.InboxEmail(ticket, customer, userName, file, company));
        return SupportRequestRules.ToDto(ticket);
    }
}
