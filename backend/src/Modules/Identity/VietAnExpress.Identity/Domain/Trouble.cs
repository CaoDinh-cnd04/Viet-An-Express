namespace VietAnExpress.Identity.Domain;

/// <summary>
/// Trạng thái yêu cầu — khớp frontend (<c>features/troubles/types.ts</c>). Cột TContact.Status lưu số:
/// 0 = mới, 1 = đang xử lý, 2 = chờ khách phản hồi, 3 = đã xử lý.
/// </summary>
internal static class TroubleStatus
{
    public const string New = "new";
    public const string Doing = "doing";
    /// <summary>Chờ khách phản hồi.</summary>
    public const string WaitCustomer = "waitc";
    public const string Done = "done";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { New, Doing, WaitCustomer, Done };

    public static string Label(string status) => status switch
    {
        New => "Mới",
        Doing => "Đang xử lý",
        WaitCustomer => "Chờ khách phản hồi",
        Done => "Đã xử lý",
        _ => status
    };

    public static int ToNumber(string status) => status switch
    {
        Doing => 1,
        WaitCustomer => 2,
        Done => 3,
        _ => 0
    };

    public static string FromNumber(int? value) => value switch
    {
        1 => Doing,
        2 => WaitCustomer,
        3 => Done,
        _ => New
    };
}

/// <summary>
/// Loại yêu cầu — khớp frontend. Cột TContact.FormType lưu nhãn "Portal - …" để phần mềm cũ lọc được yêu cầu gửi từ portal.
/// </summary>
internal static class TroubleKind
{
    /// <summary>Sự cố của 1 vận đơn — bắt buộc mã bill.</summary>
    public const string Incident = "su_co";
    /// <summary>Yêu cầu hỗ trợ chung (tạo đơn, cước, tracking, sửa thông tin người gửi…).</summary>
    public const string Support = "ho_tro";
    /// <summary>Góp ý cho Việt An / phần mềm.</summary>
    public const string Feedback = "gop_y";

    public const string IncidentFormType = "Portal - Sự cố";
    public const string SupportFormType = "Portal - Hỗ trợ";
    public const string FeedbackFormType = "Portal - Góp ý";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Incident, Support, Feedback };

    public static string Label(string kind) => kind switch
    {
        Incident => "Sự cố",
        Support => "Yêu cầu hỗ trợ",
        Feedback => "Góp ý",
        _ => kind
    };

    public static string FormTypeOf(string kind) => kind switch
    {
        Support => SupportFormType,
        Feedback => FeedbackFormType,
        _ => IncidentFormType
    };

    public static string FromFormType(string? formType) => formType switch
    {
        SupportFormType => Support,
        FeedbackFormType => Feedback,
        _ => Incident
    };
}

/// <summary>Mức độ — khớp frontend.</summary>
internal static class TroublePriority
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { "low", "mid", "high" };
}

/// <summary>
/// Yêu cầu khách gửi CS Việt An — 1 dòng bảng hệ thống cũ dbo.TContact (FormType "Portal - …"): sự cố vận đơn, yêu cầu hỗ trợ hoặc góp ý.
/// Không có trao đổi qua lại: CS xử lý ở phần mềm cũ / trang quản trị, đổi trạng thái thì khách nhận thông báo.
/// </summary>
internal sealed class Trouble
{
    public const int BillMaxLength = 50;
    public const int NameMaxLength = 250;
    public const int CountryMaxLength = 100;
    public const int TypeMaxLength = 100;
    public const int SubjectMinLength = 5;
    public const int SubjectMaxLength = 200;
    public const int DescriptionMinLength = 10;
    public const int DescriptionMaxLength = 4000;
    public const int RequesterMaxLength = 150;
    public const int ContactMaxLength = 200;
    public const int FileMaxBytes = 10 * 1024 * 1024;

    /// <summary>Đuôi tệp đính kèm nhận (khớp <c>accept</c> ở frontend) — tệp chỉ gửi kèm email, không lưu database.</summary>
    public static readonly IReadOnlySet<string> FileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".pdf", ".xlsx", ".xls", ".csv", ".doc", ".docx" };

    private Trouble() { } // EF Core

    public static Trouble Create(string kind, long customerId, string companyName, long? staffId,
        string? bill, string? consignee, string? country, string type, string? subject, string priority, string description, string requester,
        string contact, string? email, DateTime now) => new()
    {
        Kind = kind,
        CustomerId = customerId,
        CompanyName = companyName,
        StaffId = staffId,
        Bill = bill,
        ConsigneeName = consignee,
        Country = country,
        Type = type,
        // Phần mềm cũ đọc cột Title: yêu cầu hỗ trợ là tiêu đề khách nhập, loại khác là loại sự cố / chủ đề.
        Title = string.IsNullOrWhiteSpace(subject) ? type : subject,
        Priority = priority,
        Description = description,
        Requester = requester,
        Contact = contact,
        Email = email,
        Status = TroubleStatus.New,
        CreatedAt = now,
        UpdatedAt = now
    };

    public int Id { get; private set; }
    /// <summary>su_co / ho_tro / gop_y — lưu ở FormType dạng "Portal - …".</summary>
    public string Kind { get; private set; } = TroubleKind.Incident;
    public long CustomerId { get; private set; }
    public string CompanyName { get; private set; } = "";
    /// <summary>Tài khoản con gửi (null = tài khoản chính).</summary>
    public long? StaffId { get; private set; }
    /// <summary>Mã vận đơn — chỉ sự cố có.</summary>
    public string? Bill { get; private set; }
    public string? ConsigneeName { get; private set; }
    public string? Country { get; private set; }
    /// <summary>Loại sự cố, hoặc chủ đề của yêu cầu hỗ trợ / góp ý.</summary>
    public string Type { get; private set; } = "";
    /// <summary>Cột Title: tiêu đề yêu cầu hỗ trợ, hoặc bằng <see cref="Type"/>.</summary>
    public string Title { get; private set; } = "";
    public string Priority { get; private set; } = "mid";
    public string Description { get; private set; } = "";
    public string Requester { get; private set; } = "";
    /// <summary>SĐT / email khách nhập để CS liên hệ lại.</summary>
    public string Contact { get; private set; } = "";
    /// <summary>Email liên hệ (khi khách nhập email) — phần mềm cũ hiện ở cột Email.</summary>
    public string? Email { get; private set; }
    public string Status { get; private set; } = TroubleStatus.New;
    public DateTime CreatedAt { get; private set; }
    /// <summary>Lần đổi trạng thái gần nhất — sắp xếp danh sách.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Tiêu đề yêu cầu hỗ trợ (null với loại khác).</summary>
    public string? Subject => Kind == TroubleKind.Support ? Title : null;

    /// <summary>Mã hiển thị (vd TRB-1024).</summary>
    public string Code => FormatCode(Id);

    public static string FormatCode(long id) => $"TRB-{id}";

    /// <summary>"TRB-1024" hoặc "1024" → 1024.</summary>
    public static int? ParseCode(string? code)
    {
        var v = code?.Trim() ?? "";
        if (v.StartsWith("TRB-", StringComparison.OrdinalIgnoreCase)) v = v[4..];
        return int.TryParse(v, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }

    /// <summary>Việt An đổi trạng thái. Trả về true nếu trạng thái thay đổi.</summary>
    public bool ChangeStatus(string status, DateTime now)
    {
        if (status == Status) return false;
        Status = status;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Ghi tên tệp khách đính kèm vào cuối nội dung (tệp chỉ gửi qua email).</summary>
    public void NoteAttachment(string fileName) => Description += $"\n\nĐính kèm (đã gửi qua email): {fileName}";
}

/// <summary>Tệp đính kèm khách gửi (đã đọc vào bộ nhớ) — chỉ gửi kèm email về hộp thư Việt An.</summary>
internal sealed record TroubleFile(string Name, string ContentType, byte[] Data);
