
namespace VietAnExpress.Identity.Support;

/// <summary>
/// Loại yêu cầu — khớp frontend. Cột TContact.FormType lưu nhãn "Portal - …" để phần mềm cũ lọc được yêu cầu gửi từ portal.
/// </summary>
internal static class SupportRequestKind
{
    /// <summary>Sự cố vận đơn — loại cũ (FormType "Portal - Sự cố"), chỉ còn để đọc yêu cầu đã gửi; từ 27/10/2026 gộp vào <see cref="Support"/>.</summary>
    public const string Incident = "su_co";
    /// <summary>Hỗ trợ &amp; sự cố (gộp 1): vận đơn có / không, tạo đơn, cước, tracking, sửa thông tin người gửi…</summary>
    public const string Support = "ho_tro";
    /// <summary>Góp ý cho Việt An / phần mềm.</summary>
    public const string Feedback = "gop_y";
    /// <summary>Khách vãng lai gửi lời nhắn ở trang chủ (chưa đăng nhập, CustID = 0) — không nằm trong <see cref="All"/> (khách không tạo được qua /troubles).</summary>
    public const string Contact = "lien_he";

    public const string IncidentFormType = "Portal - Sự cố";
    public const string SupportFormType = "Portal - Hỗ trợ";
    public const string FeedbackFormType = "Portal - Góp ý";
    public const string ContactFormType = "Portal - Liên hệ";

    /// <summary>
    /// TContact.KeyWord của form hệ thống cũ (xem dữ liệu thật 27/10/2026): form "Yêu cầu" Bill Online ghi "Feedback" (loại góp ý ở Title),
    /// form liên hệ trang chủ website ghi "ContactHome" (Title NULL). Portal ghi giống hệt để CS thấy chung chỗ ở phần mềm cũ.
    /// </summary>
    public const string FeedbackKeyWord = "Feedback";
    public const string ContactKeyWord = "ContactHome";

    /// <summary>Tên hiển thị của lời nhắn trang chủ (KeyWord là mã "ContactHome").</summary>
    public const string ContactTopic = "Liên hệ từ trang chủ";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Incident, Support, Feedback };

    public static string Label(string kind) => kind switch
    {
        Incident => "Sự cố",
        Support => "Hỗ trợ & sự cố",
        Feedback => "Góp ý",
        Contact => "Liên hệ",
        _ => kind
    };

    public static string FormTypeOf(string kind) => kind switch
    {
        Support => SupportFormType,
        Feedback => FeedbackFormType,
        Contact => ContactFormType,
        _ => IncidentFormType
    };

    public static string FromFormType(string? formType) => formType switch
    {
        SupportFormType => Support,
        FeedbackFormType => Feedback,
        ContactFormType => Contact,
        _ => Incident
    };
}
