using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

/// <summary>Thông tin người gửi — tên trường khớp frontend (<c>features/sender-info/types.ts</c>).</summary>
internal sealed record SenderInfoDto(string CompanyName, string? ContactName, string? Phone, string? Email, string? Address, string? TaxCode);

/// <summary>1 yêu cầu sửa: <c>changed</c> = tên các trường khác giá trị cũ (companyName, contactName, phone, email, address, taxCode).</summary>
internal sealed record SenderInfoRequestDto(
    long Id, string Status, SenderInfoDto Old, SenderInfoDto New, IReadOnlyList<string> Changed,
    string? Note, string? Reason, string CreatedAt, string? ReviewedAt);

/// <summary>Trang "Thông tin người gửi": hồ sơ hiện tại + yêu cầu đang chờ duyệt (nếu có) + lịch sử gần đây.</summary>
internal sealed record SenderInfoOverviewDto(SenderInfoDto Current, SenderInfoRequestDto? Pending, IReadOnlyList<SenderInfoRequestDto> History);

/// <summary>
/// Yêu cầu ở trang quản trị. <c>Live</c> = hồ sơ đang lưu trong dbo.TCustomer;
/// <c>Outdated</c> = hồ sơ đã đổi (ở hệ thống cũ) sau khi khách gửi yêu cầu, ở các trường yêu cầu này sửa.
/// </summary>
internal sealed record AdminSenderInfoRequestDto(
    SenderInfoRequestDto Request, string CustomerCode, string CompanyName, string UserName, SenderInfoDto? Live, bool Outdated, string? ReviewedBy);

internal sealed record SenderInfoChangeRequest(
    string? CompanyName, string? ContactName, string? Phone, string? Email, string? Address, string? TaxCode, string? Note);

internal static class SenderInfoErrors
{
    public static readonly Error CompanyName = Error.Validation("SENDER_COMPANY", $"Nhập tên công ty (tối đa {SenderInfo.CompanyNameMaxLength} ký tự)");
    public static readonly Error ContactName = Error.Validation("SENDER_CONTACT", $"Nhập người liên hệ (tối đa {SenderInfo.ContactNameMaxLength} ký tự)");
    public static readonly Error Phone = Error.Validation("SENDER_PHONE", "Số điện thoại không hợp lệ");
    public static readonly Error Email = Error.Validation("SENDER_EMAIL", "Email không hợp lệ");
    public static readonly Error Address = Error.Validation("SENDER_ADDRESS", $"Nhập địa chỉ lấy hàng (5–{SenderInfo.AddressMaxLength} ký tự)");
    public static readonly Error TaxCode = Error.Validation("SENDER_TAX_CODE", "Mã số thuế gồm 10 số, chi nhánh thêm -XXX (vd 0312345678-001)");
    public static readonly Error Note = Error.Validation("SENDER_NOTE", $"Ghi chú tối đa {SenderInfoRequest.NoteMaxLength} ký tự");
    public static readonly Error NoChange = Error.Validation("SENDER_NO_CHANGE", "Thông tin mới giống thông tin hiện tại");
    public static readonly Error Reason = Error.Validation("SENDER_REASON", $"Nhập lý do từ chối (tối đa {SenderInfoRequest.ReasonMaxLength} ký tự)");
    public static readonly Error PendingExists = Error.Conflict("SENDER_PENDING", "Bạn đang có yêu cầu chờ duyệt — hủy yêu cầu đó trước khi gửi yêu cầu mới");
    public static readonly Error Closed = Error.BusinessRule("SENDER_CLOSED", "Yêu cầu đã được xử lý");
    public static readonly Error NotFound = Error.NotFound("SENDER_NOT_FOUND", "Không tìm thấy yêu cầu");
    public static readonly Error CustomerNotFound = Error.NotFound("SENDER_CUSTOMER_NOT_FOUND", "Không tìm thấy hồ sơ khách hàng");
    public static readonly Error Outdated = Error.Conflict("SENDER_OUTDATED", "Hồ sơ đã thay đổi trong lúc chờ duyệt — đề nghị khách gửi lại yêu cầu theo hồ sơ mới");
}

/// <summary>Quy tắc sửa thông tin người gửi — hàm thuần, có test.</summary>
internal static partial class SenderInfoRules
{
    public const string CompanyNameField = "companyName";
    public const string ContactNameField = "contactName";
    public const string PhoneField = "phone";
    public const string EmailField = "email";
    public const string AddressField = "address";
    public const string TaxCodeField = "taxCode";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [CompanyNameField] = "Tên công ty",
        [ContactNameField] = "Người liên hệ",
        [PhoneField] = "Điện thoại",
        [EmailField] = "Email",
        [AddressField] = "Địa chỉ lấy hàng",
        [TaxCodeField] = "Mã số thuế"
    };

    [GeneratedRegex(@"^\+?[0-9 ().-]{6,50}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^[0-9]{10}(-[0-9]{3})?$")]
    private static partial Regex TaxCodePattern();

    public static SenderInfo From(CustomerSummary c) => new(c.CompanyName, c.ContactName, c.Phone, c.Email, c.Address, c.TaxCode);

    public static SenderInfo From(SenderInfoChangeRequest r) =>
        new(Clean(r.CompanyName) ?? "", Clean(r.ContactName), Clean(r.Phone), Clean(r.Email), Clean(r.Address), Clean(r.TaxCode));

    /// <summary>
    /// Tên công ty, người liên hệ, điện thoại, địa chỉ bắt buộc. Định dạng điện thoại / email / mã số thuế chỉ kiểm tra khi trường đó đổi
    /// (dữ liệu cũ trong dbo.TCustomer có thể không đúng chuẩn — khách không sửa thì không chặn).
    /// </summary>
    public static Error? Validate(SenderInfo current, SenderInfo next, string? note)
    {
        if (next.CompanyName.Length is 0 or > SenderInfo.CompanyNameMaxLength) return SenderInfoErrors.CompanyName;
        if ((next.ContactName?.Length ?? 0) is 0 or > SenderInfo.ContactNameMaxLength) return SenderInfoErrors.ContactName;
        if (next.Phone is null || (next.Phone != current.Phone && !IsPhone(next.Phone))) return SenderInfoErrors.Phone;
        if (next.Email is not null && next.Email != current.Email && (next.Email.Length > SenderInfo.EmailMaxLength || !SupportRules.IsEmail(next.Email)))
            return SenderInfoErrors.Email;
        if ((next.Address?.Length ?? 0) is < 5 or > SenderInfo.AddressMaxLength) return SenderInfoErrors.Address;
        if (next.TaxCode is not null && next.TaxCode != current.TaxCode && !TaxCodePattern().IsMatch(next.TaxCode)) return SenderInfoErrors.TaxCode;
        if ((note?.Trim().Length ?? 0) > SenderInfoRequest.NoteMaxLength) return SenderInfoErrors.Note;
        if (Changed(current, next).Count == 0) return SenderInfoErrors.NoChange;
        return null;
    }

    private static bool IsPhone(string value) => PhonePattern().IsMatch(value) && value.Count(char.IsAsciiDigit) >= 6;

    /// <summary>Các trường khác nhau giữa 2 bộ thông tin (theo thứ tự hiển thị).</summary>
    public static IReadOnlyList<string> Changed(SenderInfo a, SenderInfo b)
    {
        var fields = new List<string>(6);
        if (a.CompanyName != b.CompanyName) fields.Add(CompanyNameField);
        if (a.ContactName != b.ContactName) fields.Add(ContactNameField);
        if (a.Phone != b.Phone) fields.Add(PhoneField);
        if (a.Email != b.Email) fields.Add(EmailField);
        if (a.Address != b.Address) fields.Add(AddressField);
        if (a.TaxCode != b.TaxCode) fields.Add(TaxCodeField);
        return fields;
    }

    /// <summary>Chỉ ghi các trường khách sửa — trường khác giữ nguyên trong dbo.TCustomer (có thể đã đổi ở hệ thống cũ); xóa trắng → "".</summary>
    public static SenderInfoUpdate ToUpdate(SenderInfoRequest r)
    {
        var changed = Changed(r.Old, r.New);
        string? Pick(string field, string? value) => changed.Contains(field) ? value ?? "" : null;
        return new SenderInfoUpdate(
            Pick(CompanyNameField, r.NewCompanyName),
            Pick(ContactNameField, r.NewContactName),
            Pick(PhoneField, r.NewPhone),
            Pick(EmailField, r.NewEmail),
            Pick(AddressField, r.NewAddress),
            Pick(TaxCodeField, r.NewTaxCode));
    }

    public static SenderInfoUpdate Expected(SenderInfoRequest r)
    {
        var changed = Changed(r.Old, r.New);
        string? Pick(string field, string? value) => changed.Contains(field) ? value ?? "" : null;
        return new(Pick(CompanyNameField, r.OldCompanyName), Pick(ContactNameField, r.OldContactName), Pick(PhoneField, r.OldPhone),
            Pick(EmailField, r.OldEmail), Pick(AddressField, r.OldAddress), Pick(TaxCodeField, r.OldTaxCode));
    }

    /// <summary>Hồ sơ hiện tại khác giá trị cũ của yêu cầu ở trường yêu cầu sửa → có người đã đổi trong lúc chờ duyệt.</summary>
    public static bool Outdated(SenderInfoRequest r, SenderInfo live)
    {
        var requested = Changed(r.Old, r.New);
        return Changed(r.Old, live).Any(requested.Contains);
    }

    public static SenderInfoDto ToDto(SenderInfo s) => new(s.CompanyName, s.ContactName, s.Phone, s.Email, s.Address, s.TaxCode);

    public static SenderInfoRequestDto ToDto(SenderInfoRequest r) => new(
        r.Id, r.Status, ToDto(r.Old), ToDto(r.New), Changed(r.Old, r.New), r.Note, r.RejectReason,
        TroubleRules.Time(r.CreatedAt), r.ReviewedAt is { } at ? TroubleRules.Time(at) : null);

    /// <summary>Bảng "cũ → mới" của các trường đã sửa (dùng cho email và thông báo).</summary>
    public static IReadOnlyList<EmailField> ChangeFields(SenderInfoRequest r)
    {
        var old = Values(r.Old);
        var next = Values(r.New);
        return Changed(r.Old, r.New).Select(f => new EmailField(Labels[f], $"{old[f] ?? "(trống)"} → {next[f] ?? "(trống)"}")).ToList();
    }

    private static Dictionary<string, string?> Values(SenderInfo s) => new()
    {
        [CompanyNameField] = s.CompanyName,
        [ContactNameField] = s.ContactName,
        [PhoneField] = s.Phone,
        [EmailField] = s.Email,
        [AddressField] = s.Address,
        [TaxCodeField] = s.TaxCode
    };

    /// <summary>Thông báo riêng cho khách khi admin duyệt / từ chối.</summary>
    public static Notification CustomerNotice(SenderInfoRequest r, string author, string? link, DateTime now)
    {
        var approved = r.Status == SenderInfoRequestStatus.Approved;
        var changes = string.Join("\n", ChangeFields(r).Select(f => $"• {f.Label}: {f.Value}"));
        var body = approved
            ? $"Việt An đã cập nhật thông tin người gửi theo yêu cầu của bạn:\n{changes}"
            : $"Yêu cầu sửa thông tin người gửi chưa được duyệt.\nLý do: {r.RejectReason}\n\nThông tin đề nghị:\n{changes}";
        return Notification.Create(
            approved ? "Đã duyệt sửa thông tin người gửi" : "Yêu cầu sửa thông tin người gửi bị từ chối",
            body.Length <= Notification.BodyMaxLength ? body : body[..Notification.BodyMaxLength],
            important: !approved, sendEmail: false, author, now, link, r.CustomerId);
    }

    /// <summary>Email báo yêu cầu mới về hộp thư Việt An.</summary>
    public static EmailMessage InboxEmail(SenderInfoRequest r, CustomerSummary? customer, EmailCompany company)
    {
        var content = new EmailContent(
            Title: "Yêu cầu sửa thông tin người gửi",
            Badge: "Chờ duyệt",
            Meta: $"{r.OldCompanyName} ({r.CustomerCode}) · {TroubleRules.Time(r.CreatedAt)}",
            Sections:
            [
                new EmailSection("Thay đổi", Fields: ChangeFields(r)),
                .. r.Note is { } note ? [new EmailSection("Ghi chú của khách", note)] : Array.Empty<EmailSection>(),
                new EmailSection("Thông tin khách", Fields: SupportRules.CustomerFields(customer, r.SenderUserName, false, null))
            ],
            Button: company.Portal("/admin") is { } admin ? new EmailLink("Duyệt trong trang quản trị", admin) : null,
            FooterNote: "Thông tin chỉ được ghi vào hồ sơ khách sau khi quản trị duyệt.");
        return new EmailMessage(
            $"[Sửa người gửi] {r.OldCompanyName} ({r.CustomerCode})",
            EmailTemplate.PlainText(content, company),
            ReplyTo: SupportRules.IsEmail(r.OldEmail) ? r.OldEmail : null,
            HtmlBody: EmailTemplate.Html(content, company));
    }

    /// <summary>Email báo kết quả cho khách — gửi tới email hồ sơ (cả email cũ lẫn mới nếu khách đổi email); không có email hợp lệ → null.</summary>
    public static EmailMessage? CustomerEmail(SenderInfoRequest r, EmailCompany company)
    {
        var to = new[] { r.OldEmail, r.Status == SenderInfoRequestStatus.Approved ? r.NewEmail : null }
            .Where(SupportRules.IsEmail).Select(e => e!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (to.Count == 0) return null;

        var approved = r.Status == SenderInfoRequestStatus.Approved;
        var title = approved ? "Đã cập nhật thông tin người gửi" : "Yêu cầu sửa thông tin người gửi chưa được duyệt";
        var content = new EmailContent(
            Title: title,
            Badge: approved ? "Đã duyệt" : "Từ chối",
            Meta: $"{r.NewCompanyName} ({r.CustomerCode})",
            Sections:
            [
                approved
                    ? new EmailSection(null, "Thông tin người gửi trên portal đã được cập nhật. Các đơn tạo mới sẽ dùng thông tin này.")
                    : new EmailSection("Lý do", r.RejectReason),
                new EmailSection(approved ? "Thay đổi" : "Thông tin đề nghị", Fields: ChangeFields(r))
            ],
            Button: company.Portal("/account/sender-info") is { } page ? new EmailLink("Xem thông tin người gửi", page) : null,
            FooterNote: approved ? null : "Bạn có thể gửi lại yêu cầu với thông tin đã điều chỉnh.");
        return new EmailMessage(title, EmailTemplate.PlainText(content, company), To: to, HtmlBody: EmailTemplate.Html(content, company));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

// ---------------- Khách (chỉ tài khoản chính) ----------------

internal sealed record GetSenderInfoQuery : IRequest<Result<SenderInfoOverviewDto>>;

internal sealed record RequestSenderInfoChangeCommand(SenderInfoChangeRequest Request) : IRequest<Result<SenderInfoRequestDto>>;

internal sealed record CancelSenderInfoRequestCommand(long Id) : IRequest<Result<SenderInfoRequestDto>>;

internal sealed class CustomerSenderInfoHandlers(
    IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IBackgroundJobQueue jobs, EmailCompany company, TimeProvider clock) :
    IRequestHandler<GetSenderInfoQuery, Result<SenderInfoOverviewDto>>,
    IRequestHandler<RequestSenderInfoChangeCommand, Result<SenderInfoRequestDto>>,
    IRequestHandler<CancelSenderInfoRequestCommand, Result<SenderInfoRequestDto>>
{
    private const int HistoryLimit = 10;

    public async Task<Result<SenderInfoOverviewDto>> Handle(GetSenderInfoQuery q, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var customer = await customers.GetByIdAsync(customerId, ct);
        if (customer is null) return SenderInfoErrors.CustomerNotFound;
        var rows = await db.SenderInfoRequests.AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderBy(r => r.Status == SenderInfoRequestStatus.Pending ? 0 : 1).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Take(HistoryLimit + 1)
            .ToListAsync(ct);
        var pending = rows.FirstOrDefault(r => r.IsPending);
        return new SenderInfoOverviewDto(
            SenderInfoRules.ToDto(SenderInfoRules.From(customer)),
            pending is null ? null : SenderInfoRules.ToDto(pending),
            rows.Where(r => !r.IsPending).Take(HistoryLimit).Select(SenderInfoRules.ToDto).ToList());
    }

    public async Task<Result<SenderInfoRequestDto>> Handle(RequestSenderInfoChangeCommand c, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var customer = await customers.GetByIdAsync(customerId, ct);
        if (customer is null) return SenderInfoErrors.CustomerNotFound;

        var current = SenderInfoRules.From(customer);
        var next = SenderInfoRules.From(c.Request);
        if (SenderInfoRules.Validate(current, next, c.Request.Note) is { } error) return error;
        if (await db.SenderInfoRequests.AnyAsync(r => r.CustomerId == customerId && r.Status == SenderInfoRequestStatus.Pending, ct))
            return SenderInfoErrors.PendingExists;

        var request = SenderInfoRequest.Create(customerId, customer.Code, user.UserName ?? customer.Code, current, next, c.Request.Note,
            VietnamTime.Now(clock));
        db.SenderInfoRequests.Add(request);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(request).State = EntityState.Detached;
            return SenderInfoErrors.PendingExists;
        }
        jobs.EnqueueEmail($"email yêu cầu sửa người gửi {request.Id}", SenderInfoRules.InboxEmail(request, customer, company));
        return SenderInfoRules.ToDto(request);
    }

    public async Task<Result<SenderInfoRequestDto>> Handle(CancelSenderInfoRequestCommand c, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var request = await db.SenderInfoRequests.FirstOrDefaultAsync(r => r.Id == c.Id && r.CustomerId == customerId, ct);
        if (request is null) return SenderInfoErrors.NotFound;
        if (!request.IsPending) return SenderInfoErrors.Closed;
        request.Cancel(VietnamTime.Now(clock));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return SenderInfoErrors.Closed; }
        return SenderInfoRules.ToDto(request);
    }
}

// ---------------- Quản trị Việt An ----------------

/// <param name="Status">Lọc theo trạng thái; null = tất cả (chờ duyệt lên đầu).</param>
internal sealed record GetAdminSenderInfoRequestsQuery(string? Status = null) : IRequest<Result<IReadOnlyList<AdminSenderInfoRequestDto>>>;

/// <summary>Duyệt → ghi vào dbo.TCustomer, báo khách (thông báo + email).</summary>
internal sealed record ApproveSenderInfoRequestCommand(long Id) : IRequest<Result<AdminSenderInfoRequestDto>>;

internal sealed record RejectSenderInfoRequestCommand(long Id, string? Reason) : IRequest<Result<AdminSenderInfoRequestDto>>;

internal sealed class AdminSenderInfoHandlers(
    IdentityDbContext db, ICurrentUser user, ICustomersApi customers, IBackgroundJobQueue jobs, EmailCompany company, TimeProvider clock) :
    IRequestHandler<GetAdminSenderInfoRequestsQuery, Result<IReadOnlyList<AdminSenderInfoRequestDto>>>,
    IRequestHandler<ApproveSenderInfoRequestCommand, Result<AdminSenderInfoRequestDto>>,
    IRequestHandler<RejectSenderInfoRequestCommand, Result<AdminSenderInfoRequestDto>>
{
    private const int ListLimit = 300;
    private const string Author = "CS Việt An";

    public async Task<Result<IReadOnlyList<AdminSenderInfoRequestDto>>> Handle(GetAdminSenderInfoRequestsQuery q, CancellationToken ct)
    {
        if (q.Status is { } status && !SenderInfoRequestStatus.All.Contains(status)) return SenderInfoErrors.NotFound;
        var query = db.SenderInfoRequests.AsNoTracking();
        if (q.Status is { } s) query = query.Where(r => r.Status == s);
        var rows = await query
            .OrderBy(r => r.Status == SenderInfoRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Take(ListLimit)
            .ToListAsync(ct);
        var live = (await customers.GetByIdsAsync(rows.Select(r => r.CustomerId).Distinct().ToList(), ct)).ToDictionary(c => c.Id);
        return rows.Select(r => ToDto(r, live.GetValueOrDefault(r.CustomerId))).ToList();
    }

    public async Task<Result<AdminSenderInfoRequestDto>> Handle(ApproveSenderInfoRequestCommand c, CancellationToken ct)
    {
        SenderInfoRequest? reviewed = null;
        async Task<Result<AdminSenderInfoRequestDto>> Apply(DbTransaction? transaction)
        {
            var query = transaction is null ? db.SenderInfoRequests : db.SenderInfoRequests
                .FromSqlInterpolated($"SELECT * FROM dbo.YeuCauSuaNguoiGui WITH (UPDLOCK, HOLDLOCK) WHERE ID = {c.Id}");
            var request = await query.FirstOrDefaultAsync(r => r.Id == c.Id, ct);
            if (request is null) return SenderInfoErrors.NotFound;
            if (!request.IsPending) return SenderInfoErrors.Closed;
            CustomerSummary? updated;
            if (transaction is not null)
            {
                var write = await customers.UpdateSenderInfoInTransactionAsync(request.CustomerId, SenderInfoRules.ToUpdate(request), SenderInfoRules.Expected(request), transaction, ct);
                if (write.Outdated) return SenderInfoErrors.Outdated;
                updated = write.Current;
            }
            else
            {
                var live = await customers.GetByIdAsync(request.CustomerId, ct);
                if (live is null) return SenderInfoErrors.CustomerNotFound;
                if (SenderInfoRules.Outdated(request, SenderInfoRules.From(live))) return SenderInfoErrors.Outdated;
                updated = await customers.UpdateSenderInfoAsync(request.CustomerId, SenderInfoRules.ToUpdate(request), ct);
            }
            if (updated is null) return SenderInfoErrors.CustomerNotFound;
            var now = VietnamTime.Now(clock);
            request.Approve(user.UserName ?? Author, now);
            await NotifyAsync(request, now, ct);
            reviewed = request;
            return ToDto(request, updated);
        }

        Result<AdminSenderInfoRequestDto> result;
        try
        {
            if (!db.Database.IsRelational()) result = await Apply(null);
            else result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var applied = await Apply(transaction.GetDbTransaction());
                if (applied.IsSuccess) await transaction.CommitAsync(ct);
                return applied;
            });
        }
        catch (DbUpdateConcurrencyException) { return SenderInfoErrors.Closed; }
        if (result.IsSuccess && reviewed is not null) QueueEmail(reviewed);
        return result;
    }

    public async Task<Result<AdminSenderInfoRequestDto>> Handle(RejectSenderInfoRequestCommand c, CancellationToken ct)
    {
        var reason = c.Reason?.Trim() ?? "";
        if (reason.Length is 0 or > SenderInfoRequest.ReasonMaxLength) return SenderInfoErrors.Reason;
        var request = await db.SenderInfoRequests.FirstOrDefaultAsync(r => r.Id == c.Id, ct);
        if (request is null) return SenderInfoErrors.NotFound;
        if (!request.IsPending) return SenderInfoErrors.Closed;

        var now = VietnamTime.Now(clock);
        request.Reject(user.UserName ?? Author, reason, now);
        try { await NotifyAsync(request, now, ct); }
        catch (DbUpdateConcurrencyException) { return SenderInfoErrors.Closed; }
        QueueEmail(request);
        return ToDto(request, await customers.GetByIdAsync(request.CustomerId, ct));
    }

    private async Task NotifyAsync(SenderInfoRequest request, DateTime now, CancellationToken ct)
    {
        db.Notifications.Add(SenderInfoRules.CustomerNotice(request, Author, company.Portal("/account/sender-info"), now));
        await db.SaveChangesAsync(ct);
    }

    private void QueueEmail(SenderInfoRequest request)
    {
        if (SenderInfoRules.CustomerEmail(request, company) is { } email)
            jobs.EnqueueEmail($"email kết quả sửa người gửi {request.Id}", email);
    }

    private static AdminSenderInfoRequestDto ToDto(SenderInfoRequest r, CustomerSummary? live)
    {
        var liveInfo = live is null ? null : SenderInfoRules.From(live);
        return new AdminSenderInfoRequestDto(
            SenderInfoRules.ToDto(r), r.CustomerCode, live?.CompanyName ?? r.OldCompanyName, r.SenderUserName,
            liveInfo is null ? null : SenderInfoRules.ToDto(liveInfo),
            r.IsPending && liveInfo is not null && SenderInfoRules.Outdated(r, liveInfo),
            r.ReviewedBy);
    }
}
