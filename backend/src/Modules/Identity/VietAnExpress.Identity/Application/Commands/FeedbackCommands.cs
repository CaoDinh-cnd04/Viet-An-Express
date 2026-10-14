using System.Globalization;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Application;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Domain;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

/// <summary>1 ảnh khách gửi lên (đã đọc vào bộ nhớ ở controller).</summary>
internal sealed record FeedbackUpload(string FileName, string ContentType, byte[] Data);

internal sealed record FeedbackImageDto(string Id, string FileName, int Size);

/// <summary>
/// 1 câu trả lời khảo sát: mã câu hỏi, nội dung câu hỏi lúc gửi, các phương án đã chọn và ghi chú thêm.
/// Lưu kèm nội dung câu hỏi để góp ý cũ vẫn đọc được khi bộ câu hỏi (ở frontend) thay đổi.
/// </summary>
internal sealed record FeedbackSurveyAnswer(string Id, string Question, IReadOnlyList<string> Answers, string? Note);

/// <summary>Góp ý khách đã gửi (trang Góp ý của khách) — không trả dữ liệu ảnh, ảnh tải riêng theo id.</summary>
internal sealed record FeedbackDto(
    string Id, string Message, string? Contact, int? Rating, string CreatedAt, bool Seen, IReadOnlyList<FeedbackImageDto> Images,
    IReadOnlyList<FeedbackSurveyAnswer> Survey);

/// <summary>Góp ý ở trang quản trị: kèm khách / người gửi.</summary>
internal sealed record AdminFeedbackDto(
    string Id, string CustomerCode, string CompanyName, string UserName, bool IsStaff, string Message, string? Contact, int? Rating,
    string CreatedAt, bool Seen, IReadOnlyList<FeedbackImageDto> Images, IReadOnlyList<FeedbackSurveyAnswer> Survey);

/// <summary>Chi tiết 1 góp ý ở trang quản trị: thêm lúc đã xem và thông tin liên hệ của khách (hồ sơ dbo.TCustomer).</summary>
internal sealed record AdminFeedbackDetailDto(
    AdminFeedbackDto Feedback, string? SeenAt,
    string? CustomerContact, string? CustomerPhone, string? CustomerEmail, string? CustomerAddress);

/// <summary>Ảnh trả về trình duyệt.</summary>
internal sealed record FeedbackImageFile(string FileName, string ContentType, byte[] Data);

internal static class FeedbackErrors
{
    public static readonly Error Empty = Error.Validation("FEEDBACK_EMPTY", "Nhập nội dung góp ý hoặc trả lời ít nhất 1 câu khảo sát");
    public static readonly Error Survey = Error.Validation("FEEDBACK_SURVEY", "Câu trả lời khảo sát không hợp lệ");
    public static readonly Error TooLong = Error.Validation("FEEDBACK_TOO_LONG", $"Nội dung góp ý tối đa {Feedback.MessageMaxLength} ký tự");
    public static readonly Error ContactTooLong = Error.Validation("FEEDBACK_CONTACT_TOO_LONG", $"Thông tin liên hệ tối đa {Feedback.ContactMaxLength} ký tự");
    public static readonly Error TooManyImages = Error.Validation("FEEDBACK_TOO_MANY_IMAGES", $"Tối đa {Feedback.MaxImages} ảnh cho mỗi góp ý");
    public static readonly Error ImageTooLarge = Error.Validation("FEEDBACK_IMAGE_TOO_LARGE", "Mỗi ảnh tối đa 5 MB");
    public static readonly Error ImageType = Error.Validation("FEEDBACK_IMAGE_TYPE", "Chỉ nhận ảnh JPG, PNG, WEBP hoặc GIF");
    public static readonly Error NotFound = Error.NotFound("FEEDBACK_NOT_FOUND", "Không tìm thấy góp ý");
    public static readonly Error ImageNotFound = Error.NotFound("FEEDBACK_IMAGE_NOT_FOUND", "Không tìm thấy ảnh");
    public static readonly Error NotLoggedIn = Error.Unauthorized("NOT_LOGGED_IN", "Vui lòng đăng nhập");
    public static readonly Error Rating = Error.Validation("FEEDBACK_RATING", "Đánh giá từ 1 đến 5 sao");
}

/// <summary>Kiểm tra góp ý — hàm thuần, có test.</summary>
internal static class FeedbackRules
{
    private static readonly JsonSerializerOptions SurveyJson = new(JsonSerializerDefaults.Web);

    /// <summary>Có trả lời khảo sát thì nội dung góp ý không bắt buộc.</summary>
    public static Error? Validate(string? message, string? contact, int? rating, IReadOnlyList<FeedbackUpload> images, bool hasSurvey = false)
    {
        if (rating is { } r && (r < Feedback.MinRating || r > Feedback.MaxRating)) return FeedbackErrors.Rating;
        var text = message?.Trim() ?? "";
        if (text.Length == 0 && !hasSurvey) return FeedbackErrors.Empty;
        if (text.Length > Feedback.MessageMaxLength) return FeedbackErrors.TooLong;
        if ((contact?.Trim().Length ?? 0) > Feedback.ContactMaxLength) return FeedbackErrors.ContactTooLong;
        if (images.Count > Feedback.MaxImages) return FeedbackErrors.TooManyImages;
        if (images.Any(i => !Feedback.ImageTypes.Contains(i.ContentType) || !LooksLikeImage(i.Data))) return FeedbackErrors.ImageType;
        if (images.Any(i => i.Data.Length is 0 or > Feedback.ImageMaxBytes)) return FeedbackErrors.ImageTooLarge;
        return null;
    }

    /// <summary>
    /// Đọc câu trả lời khảo sát khách gửi (JSON <c>[{ id, question, answers, note }]</c>): cắt khoảng trắng, bỏ phương án trống / trùng,
    /// bỏ câu không chọn gì và không ghi chú. Trống → danh sách rỗng; sai cấu trúc, trùng mã câu hỏi hoặc quá giới hạn → lỗi.
    /// </summary>
    public static Result<IReadOnlyList<FeedbackSurveyAnswer>> ParseSurvey(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<FeedbackSurveyAnswer>();
        List<FeedbackSurveyAnswer?>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<FeedbackSurveyAnswer?>>(json, SurveyJson);
        }
        catch (JsonException)
        {
            return FeedbackErrors.Survey;
        }
        if (raw is null || raw.Count > Feedback.MaxSurveyQuestions) return FeedbackErrors.Survey;

        var survey = new List<FeedbackSurveyAnswer>();
        foreach (var item in raw)
        {
            var id = item?.Id?.Trim() ?? "";
            var question = item?.Question?.Trim() ?? "";
            var answers = (item?.Answers ?? []).Select(a => a?.Trim() ?? "").Where(a => a.Length > 0).Distinct().ToList();
            var note = string.IsNullOrWhiteSpace(item?.Note) ? null : item.Note.Trim();
            if (id.Length is 0 or > Feedback.SurveyIdMaxLength || question.Length is 0 or > Feedback.SurveyQuestionMaxLength
                || answers.Count > Feedback.MaxSurveyAnswers || answers.Any(a => a.Length > Feedback.SurveyAnswerMaxLength)
                || note?.Length > Feedback.SurveyNoteMaxLength || survey.Any(s => s.Id == id))
                return FeedbackErrors.Survey;
            if (answers.Count > 0 || note is not null) survey.Add(new FeedbackSurveyAnswer(id, question, answers, note));
        }
        return survey;
    }

    public static string? WriteSurvey(IReadOnlyList<FeedbackSurveyAnswer> survey) =>
        survey.Count == 0 ? null : JsonSerializer.Serialize(survey, SurveyJson);

    /// <summary>Đọc cột dbo.GopY.Khao_Sat — trống hoặc hỏng thì trả danh sách rỗng.</summary>
    public static IReadOnlyList<FeedbackSurveyAnswer> ReadSurvey(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<FeedbackSurveyAnswer>>(json, SurveyJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Tổng dung lượng ảnh đính kèm email (Gmail nhận tối đa 25 MB / email, mã hóa base64 tăng ~33%).</summary>
    public const int EmailAttachmentMaxBytes = 15 * 1024 * 1024;

    /// <summary>
    /// Email báo góp ý mới về hộp thư Việt An: số sao, nội dung, câu trả lời khảo sát, thông tin khách; đính kèm ảnh
    /// trong giới hạn <see cref="EmailAttachmentMaxBytes"/> (phần vượt chỉ ghi tên — xem ở trang /admin).
    /// </summary>
    public static EmailMessage Email(Feedback f, IReadOnlyList<FeedbackSurveyAnswer> survey, CustomerSummary? customer, EmailCompany company)
    {
        var attachments = new List<EmailAttachment>();
        var tooLarge = new List<string>();
        var total = 0;
        foreach (var image in f.Images)
        {
            if (total + image.Data.Length > EmailAttachmentMaxBytes)
            {
                tooLarge.Add(image.FileName);
                continue;
            }
            total += image.Data.Length;
            attachments.Add(new EmailAttachment(image.FileName, image.ContentType, image.Data));
        }

        var companyName = customer?.CompanyName ?? f.CompanyName;
        var code = customer?.Code ?? f.CustomerCode;
        var replyTo = SupportRules.ReplyTo(f.Contact, customer?.Email);
        var sections = new List<EmailSection>
        {
            new("Nội dung góp ý", f.Message.Length > 0 ? f.Message : "Khách không viết nội dung, chỉ trả lời khảo sát.")
        };
        if (survey.Count > 0)
            sections.Add(new EmailSection("Khảo sát trải nghiệm", Fields: survey.Select(a => new EmailField(a.Question,
                (a.Answers.Count > 0 ? string.Join(" · ", a.Answers) : "—") + (a.Note is null ? "" : $"\nGhi chú: {a.Note}"))).ToList()));
        sections.Add(new EmailSection("Thông tin khách", Fields:
        [
            .. SupportRules.CustomerFields(customer, f.SenderUserName, f.StaffId is not null, f.Contact),
            .. f.Images.Count > 0
                ? [new EmailField("Ảnh đính kèm", $"{attachments.Count} ảnh gửi kèm email" + (tooLarge.Count > 0 ? $"\n{tooLarge.Count} ảnh quá lớn, xem ở trang quản trị: {string.Join(", ", tooLarge)}" : ""))]
                : Array.Empty<EmailField>()
        ]));

        var content = new EmailContent(
            Title: $"Góp ý mới từ {companyName}",
            Badge: f.Rating is { } r ? $"{new string('★', r)}{new string('☆', Feedback.MaxRating - r)}  {r}/5" : null,
            Meta: $"Mã khách {code} · gửi lúc {Time(f.CreateDate)}",
            Sections: sections,
            Button: company.Portal("/admin") is { } admin ? new EmailLink("Xem trong trang quản trị", admin) : null,
            FooterNote: SupportRules.ReplyNote(replyTo, f.Contact));

        var stars = f.Rating is { } s ? $" — {s}★" : "";
        return new EmailMessage(
            $"[Góp ý portal] {companyName} ({code}){stars}",
            EmailTemplate.PlainText(content, company),
            ReplyTo: replyTo,
            Attachments: attachments,
            HtmlBody: EmailTemplate.Html(content, company));
    }

    /// <summary>Đầu file đúng ảnh JPG / PNG / GIF / WEBP (không tin loại file trình duyệt khai).</summary>
    public static bool LooksLikeImage(byte[] d) =>
        d.Length >= 12 && (
            (d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF)                                        // JPEG
            || (d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47)                     // PNG
            || (d[0] == 0x47 && d[1] == 0x49 && d[2] == 0x46 && d[3] == 0x38)                     // GIF
            || (d[0] == 0x52 && d[1] == 0x49 && d[2] == 0x46 && d[3] == 0x46
                && d[8] == 0x57 && d[9] == 0x45 && d[10] == 0x42 && d[11] == 0x50));              // WEBP

    /// <summary>Tên file an toàn để lưu / trả về: bỏ đường dẫn, tối đa 255 ký tự.</summary>
    public static string SafeFileName(string? name)
    {
        // Tên upload có thể chứa đường dẫn Windows dù server chạy Linux.
        var file = Path.GetFileName((name ?? "").Replace('\\', '/')).Trim();
        if (file.Length == 0) file = "anh";
        return file.Length <= 255 ? file : file[^255..];
    }

    public static string Time(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}

// ---------------- Khách ----------------

internal sealed record SendFeedbackCommand(
    string? Message, string? Contact, int? Rating, IReadOnlyList<FeedbackUpload> Images, string? Survey = null) : IRequest<Result<FeedbackDto>>;

/// <summary>Khách ẩn góp ý khỏi trang của mình; giữ nội dung và ảnh cho admin. Tài khoản con chỉ ẩn góp ý của chính mình.</summary>
internal sealed record DeleteFeedbackCommand(long Id) : IRequest<Result<bool>>;

internal sealed record GetMyFeedbackQuery : IRequest<Result<IReadOnlyList<FeedbackDto>>>;

/// <summary>Ảnh của góp ý: khách chỉ xem ảnh góp ý của chính mình; quản trị xem mọi ảnh.</summary>
internal sealed record GetFeedbackImageQuery(long ImageId) : IRequest<Result<FeedbackImageFile>>;

internal sealed class CustomerFeedbackHandlers(
    IdentityDbContext db, ICurrentUser user, ICustomersApi customers, TimeProvider clock, IBackgroundJobQueue jobs, EmailCompany company) :
    IRequestHandler<SendFeedbackCommand, Result<FeedbackDto>>,
    IRequestHandler<GetMyFeedbackQuery, Result<IReadOnlyList<FeedbackDto>>>,
    IRequestHandler<GetFeedbackImageQuery, Result<FeedbackImageFile>>,
    IRequestHandler<DeleteFeedbackCommand, Result<bool>>
{
    /// <summary>Số góp ý gần nhất hiện ở trang của khách.</summary>
    private const int MyListLimit = 50;

    public async Task<Result<FeedbackDto>> Handle(SendFeedbackCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return FeedbackErrors.NotLoggedIn;
        var survey = FeedbackRules.ParseSurvey(c.Survey);
        if (survey.IsFailure) return survey.Error;
        if (FeedbackRules.Validate(c.Message, c.Contact, c.Rating, c.Images, survey.Value.Count > 0) is { } error) return error;

        var customer = await customers.GetByIdAsync(customerId, ct);
        var feedback = Feedback.Create(customerId, customer?.Code ?? "", customer?.CompanyName ?? "", user.StaffId, user.UserName ?? "",
            c.Message?.Trim() ?? "", c.Contact, c.Rating,
            c.Images.Select(i => new FeedbackImage(FeedbackRules.SafeFileName(i.FileName), i.ContentType.ToLowerInvariant(), i.Data)),
            VietnamTime.Now(clock), FeedbackRules.WriteSurvey(survey.Value));
        db.Feedbacks.Add(feedback);
        await db.SaveChangesAsync(ct);
        // Báo về hộp thư Việt An ở nền — lỗi SMTP không làm hỏng việc gửi góp ý (đã lưu, xem được ở /admin).
        jobs.EnqueueEmail($"email góp ý #{feedback.Id}", FeedbackRules.Email(feedback, survey.Value, customer, company));
        return ToDto(feedback);
    }

    public async Task<Result<IReadOnlyList<FeedbackDto>>> Handle(GetMyFeedbackQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return FeedbackErrors.NotLoggedIn;
        // Tài khoản con chỉ thấy góp ý mình gửi; tài khoản chính thấy mọi góp ý của công ty.
        var query = db.Feedbacks.AsNoTracking().Where(f => f.CustomerId == customerId && !f.IsHiddenByCustomer);
        if (user.StaffId is { } staffId) query = query.Where(f => f.StaffId == staffId);
        var rows = await query
            .OrderByDescending(f => f.Id)
            .Take(MyListLimit)
            .Select(f => new
            {
                f.Id, f.Message, f.Contact, f.Rating, f.CreateDate, f.IsSeen, f.SurveyJson,
                Images = f.Images.OrderBy(i => i.Id).Select(i => new FeedbackImageDto(i.Id.ToString(), i.FileName, i.Size)).ToList()
            })
            .ToListAsync(ct);
        return rows.Select(r => new FeedbackDto(r.Id.ToString(CultureInfo.InvariantCulture), r.Message, r.Contact, r.Rating, FeedbackRules.Time(r.CreateDate), r.IsSeen, r.Images,
                FeedbackRules.ReadSurvey(r.SurveyJson)))
            .ToList();
    }

    public async Task<Result<bool>> Handle(DeleteFeedbackCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return FeedbackErrors.NotLoggedIn;
        var query = db.Feedbacks.Where(f => f.Id == c.Id && f.CustomerId == customerId);
        if (user.StaffId is { } staffId) query = query.Where(f => f.StaffId == staffId);
        var feedback = await query.FirstOrDefaultAsync(ct);
        if (feedback is null) return FeedbackErrors.NotFound;
        feedback.HideFromCustomer();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Result<FeedbackImageFile>> Handle(GetFeedbackImageQuery q, CancellationToken ct)
    {
        var isAdmin = user.HasPermission(IdentityPermissions.AdminFeedback);
        if (!isAdmin && user.CustomerId is null) return FeedbackErrors.NotLoggedIn;

        var image = await db.FeedbackImages.AsNoTracking()
            .Where(i => i.Id == q.ImageId)
            .Join(db.Feedbacks, i => i.FeedbackId, f => f.Id, (i, f) => new { i.FileName, i.ContentType, i.Data, f.CustomerId, f.StaffId, f.IsHiddenByCustomer })
            .FirstOrDefaultAsync(ct);
        if (image is null) return FeedbackErrors.ImageNotFound;
        var allowed = isAdmin || (!image.IsHiddenByCustomer && image.CustomerId == user.CustomerId && (user.StaffId is null || image.StaffId == user.StaffId));
        return allowed ? new FeedbackImageFile(image.FileName, image.ContentType, image.Data) : FeedbackErrors.ImageNotFound;
    }

    private static FeedbackDto ToDto(Feedback f) => new(
        f.Id.ToString(CultureInfo.InvariantCulture), f.Message, f.Contact, f.Rating, FeedbackRules.Time(f.CreateDate), f.IsSeen,
        f.Images.Select(i => new FeedbackImageDto(i.Id.ToString(CultureInfo.InvariantCulture), i.FileName, i.Size)).ToList(),
        FeedbackRules.ReadSurvey(f.SurveyJson));
}

// ---------------- Quản trị Việt An ----------------

/// <summary>
/// Tài khoản quản trị cố định trong code (người dùng chọn): chỉ lưu mã băm PBKDF2 của mật khẩu, không lưu mật khẩu gốc.
/// Đổi mật khẩu: tạo mã băm mới (StaffPasswordHasher.Hash) rồi thay <see cref="PasswordHash"/>.
/// </summary>
internal static class VietAnAdminAccount
{
    public const string UserName = "vietan.admin";
    public const string PasswordHash = "pbkdf2$100000$sNhiFgiUmRCckGhZ9PWSTQ==$IHgm2D99iAeymFyKwqAQDJayQYKFGN5lPJTpNeCMA3U=";

    public static bool Verify(string? userName, string? password) =>
        string.Equals(userName?.Trim(), UserName, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(password)
        && StaffPasswordHasher.Verify(PasswordHash, password);
}

internal sealed record AdminLoginCommand(string? UserName, string? Password) : IRequest<Result<AdminSessionDto>>;

/// <summary>Token quản trị: trang /admin giữ trong sessionStorage và gửi header Authorization (không đụng cookie phiên của khách).</summary>
internal sealed record AdminSessionDto(string Token, DateTimeOffset ExpiresAt, string UserName);

internal sealed record GetAdminFeedbackQuery : IRequest<IReadOnlyList<AdminFeedbackDto>>;

internal sealed record MarkFeedbackSeenCommand(long Id) : IRequest<Result<bool>>;

/// <summary>Quản trị xóa hẳn góp ý: xóa dòng dbo.GopY và ảnh dbo.GopY_HinhAnh — không khôi phục được.</summary>
internal sealed record AdminDeleteFeedbackCommand(long Id) : IRequest<Result<bool>>;

/// <summary>Mở chi tiết góp ý → tự đánh dấu đã xem.</summary>
internal sealed record GetAdminFeedbackDetailQuery(long Id) : IRequest<Result<AdminFeedbackDetailDto>>;

internal sealed class AdminFeedbackHandlers(IdentityDbContext db, ITokenService tokens, ICustomersApi customers, TimeProvider clock) :
    IRequestHandler<AdminLoginCommand, Result<AdminSessionDto>>,
    IRequestHandler<GetAdminFeedbackQuery, IReadOnlyList<AdminFeedbackDto>>,
    IRequestHandler<MarkFeedbackSeenCommand, Result<bool>>,
    IRequestHandler<AdminDeleteFeedbackCommand, Result<bool>>,
    IRequestHandler<GetAdminFeedbackDetailQuery, Result<AdminFeedbackDetailDto>>
{
    /// <summary>Số góp ý mới nhất hiện ở trang quản trị.</summary>
    private const int AdminListLimit = 500;

    public Task<Result<AdminSessionDto>> Handle(AdminLoginCommand c, CancellationToken ct)
    {
        if (!VietAnAdminAccount.Verify(c.UserName, c.Password)) return Task.FromResult<Result<AdminSessionDto>>(IdentityErrors.InvalidCredentials);
        // CustomerID 0: không phải khách nào — chỉ có quyền xem góp ý và soạn thông báo, mọi API của khách đều bị chặn.
        var token = tokens.CreateAccessToken(0, null, VietAnAdminAccount.UserName, [SystemRoles.VietAnAdmin], [IdentityPermissions.AdminFeedback, IdentityPermissions.AdminNotifications, IdentityPermissions.AdminTroubles, IdentityPermissions.AdminAnalytics, IdentityPermissions.AdminSenderInfo]);
        return Task.FromResult<Result<AdminSessionDto>>(new AdminSessionDto(token.Token, token.ExpiresAt, VietAnAdminAccount.UserName));
    }

    public async Task<IReadOnlyList<AdminFeedbackDto>> Handle(GetAdminFeedbackQuery q, CancellationToken ct)
    {
        var rows = await db.Feedbacks.AsNoTracking()
            .OrderByDescending(f => f.Id)
            .Take(AdminListLimit)
            .Select(f => new
            {
                f.Id, f.CustomerCode, f.CompanyName, f.SenderUserName, f.StaffId, f.Message, f.Contact, f.Rating, f.CreateDate, f.IsSeen, f.SurveyJson,
                Images = f.Images.OrderBy(i => i.Id).Select(i => new FeedbackImageDto(i.Id.ToString(), i.FileName, i.Size)).ToList()
            })
            .ToListAsync(ct);
        return rows.Select(r => new AdminFeedbackDto(
            r.Id.ToString(CultureInfo.InvariantCulture), r.CustomerCode, r.CompanyName, r.SenderUserName, r.StaffId is not null,
            r.Message, r.Contact, r.Rating, FeedbackRules.Time(r.CreateDate), r.IsSeen, r.Images, FeedbackRules.ReadSurvey(r.SurveyJson))).ToList();
    }

    public async Task<Result<AdminFeedbackDetailDto>> Handle(GetAdminFeedbackDetailQuery q, CancellationToken ct)
    {
        var f = await db.Feedbacks.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (f is null) return FeedbackErrors.NotFound;
        if (f.MarkSeen(VietnamTime.Now(clock))) await db.SaveChangesAsync(ct);

        var customer = await customers.GetByIdAsync(f.CustomerId, ct);
        var dto = new AdminFeedbackDto(
            f.Id.ToString(CultureInfo.InvariantCulture), f.CustomerCode, f.CompanyName, f.SenderUserName, f.StaffId is not null,
            f.Message, f.Contact, f.Rating, FeedbackRules.Time(f.CreateDate), f.IsSeen,
            f.Images.OrderBy(i => i.Id).Select(i => new FeedbackImageDto(i.Id.ToString(CultureInfo.InvariantCulture), i.FileName, i.Size)).ToList(),
            FeedbackRules.ReadSurvey(f.SurveyJson));
        return new AdminFeedbackDetailDto(dto, f.SeenAt is { } seen ? FeedbackRules.Time(seen) : null,
            customer?.ContactName, customer?.Phone, customer?.Email, customer?.Address);
    }

    public async Task<Result<bool>> Handle(MarkFeedbackSeenCommand c, CancellationToken ct)
    {
        var feedback = await db.Feedbacks.FirstOrDefaultAsync(f => f.Id == c.Id, ct);
        if (feedback is null) return FeedbackErrors.NotFound;
        if (feedback.MarkSeen(VietnamTime.Now(clock))) await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Result<bool>> Handle(AdminDeleteFeedbackCommand c, CancellationToken ct)
    {
        // Nạp kèm ảnh để EF xóa cả dbo.GopY_HinhAnh, không phụ thuộc cascade của khóa ngoại trong database.
        var feedback = await db.Feedbacks.Include(f => f.Images).FirstOrDefaultAsync(f => f.Id == c.Id, ct);
        if (feedback is null) return FeedbackErrors.NotFound;
        db.Feedbacks.Remove(feedback);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
