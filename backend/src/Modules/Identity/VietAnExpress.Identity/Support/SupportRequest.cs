
namespace VietAnExpress.Identity.Support;

/// <summary>
/// Yêu cầu khách gửi CS Việt An — 1 dòng bảng hệ thống cũ dbo.TContact (FormType "Portal - …"): sự cố vận đơn, yêu cầu hỗ trợ hoặc góp ý.
/// Trao đổi CS ↔ khách nối vào cuối Content (<see cref="SupportThread"/>) — không có bảng riêng, không gửi thông báo.
/// </summary>
internal sealed class SupportRequest
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

    private SupportRequest() { } // EF Core

    public static SupportRequest Create(string kind, long customerId, string companyName, long? staffId,
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
        // Phần mềm cũ đọc cột Title: tiêu đề khách nhập, không có thì vấn đề; góp ý = loại góp ý (KeyWord "Feedback");
        // lời nhắn trang chủ để trống như form website cũ.
        Title = kind == SupportRequestKind.Contact ? "" : string.IsNullOrWhiteSpace(subject) ? type : subject,
        // Góp ý / lời nhắn trang chủ không có mức độ — Des02 để NULL như form "Yêu cầu" / form liên hệ website cũ.
        Priority = kind is SupportRequestKind.Feedback or SupportRequestKind.Contact ? null : priority,
        Description = SupportThread.Clean(description),
        Requester = requester,
        Contact = contact,
        Email = email,
        Status = SupportRequestStatus.New,
        CreatedAt = now,
        UpdatedAt = now
    };

    public int Id { get; private set; }
    /// <summary>su_co / ho_tro / gop_y — lưu ở FormType dạng "Portal - …".</summary>
    public string Kind { get; private set; } = SupportRequestKind.Incident;
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
    /// <summary>Mức độ (Des02) — chỉ Hỗ trợ &amp; sự cố; góp ý / lời nhắn trang chủ để NULL.</summary>
    public string? Priority { get; private set; }

    /// <summary>Cột Gender của TContact — form cũ (Bill Online, website) luôn ghi 1; portal ghi giống.</summary>
    public int? Gender { get; private set; } = 1;
    /// <summary>Cột Content: nội dung khách gửi + các lần trả lời qua lại (<see cref="SupportThread"/>).</summary>
    public string Description { get; private set; } = "";
    public string Requester { get; private set; } = "";
    /// <summary>SĐT / email khách nhập để CS liên hệ lại.</summary>
    public string Contact { get; private set; } = "";
    /// <summary>Email liên hệ (khi khách nhập email) — phần mềm cũ hiện ở cột Email.</summary>
    public string? Email { get; private set; }
    public string Status { get; private set; } = SupportRequestStatus.New;
    public DateTime CreatedAt { get; private set; }
    /// <summary>Lần đổi trạng thái / trả lời gần nhất — sắp xếp danh sách.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Tiêu đề yêu cầu hỗ trợ (null với loại khác).</summary>
    public string? Subject => Kind == SupportRequestKind.Support ? Title : null;

    /// <summary>
    /// Vấn đề / loại hiển thị: góp ý → Title (loại góp ý, vì KeyWord là "Feedback"), lời nhắn trang chủ → "Liên hệ từ trang chủ",
    /// còn lại → KeyWord. Dòng portal ghi trước 27/10/2026 (KeyWord = loại góp ý, Title = KeyWord) vẫn ra đúng.
    /// </summary>
    public string Topic => Kind switch
    {
        SupportRequestKind.Feedback => string.IsNullOrWhiteSpace(Title) ? Type : Title,
        SupportRequestKind.Contact => SupportRequestKind.ContactTopic,
        _ => Type
    };

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

    /// <summary>CS Việt An trả lời: nối vào cuối nội dung.</summary>
    public void AddCsReply(string text, DateTime now)
    {
        Description += SupportThread.Block(SupportThread.FromCs, text, now);
        UpdatedAt = now;
    }

    /// <summary>Khách trả lời khi CS đang chờ: nối vào cuối nội dung, chuyển lại "Đang xử lý" để CS xem tiếp.</summary>
    public void AddCustomerReply(string text, DateTime now)
    {
        Description += SupportThread.Block(SupportThread.FromCustomer, text, now);
        Status = SupportRequestStatus.Doing;
        UpdatedAt = now;
    }

    /// <summary>Ghi tên tệp khách đính kèm vào cuối nội dung (tệp chỉ gửi qua email).</summary>
    public void NoteAttachment(string fileName) => Description += $"\n\nĐính kèm (đã gửi qua email): {fileName}";
}
