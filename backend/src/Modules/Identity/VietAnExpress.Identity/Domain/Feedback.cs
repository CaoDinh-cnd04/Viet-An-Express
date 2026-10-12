namespace VietAnExpress.Identity.Domain;

/// <summary>Góp ý khách gửi từ portal (dbo.GopY) — admin Việt An xem ở trang /admin.</summary>
internal sealed class Feedback
{
    public const int MessageMaxLength = 4000;
    public const int ContactMaxLength = 200;
    public const int MaxImages = 5;
    public const int ImageMaxBytes = 5 * 1024 * 1024;
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxSurveyQuestions = 30;
    public const int SurveyIdMaxLength = 50;
    public const int SurveyQuestionMaxLength = 300;
    public const int MaxSurveyAnswers = 10;
    public const int SurveyAnswerMaxLength = 200;
    public const int SurveyNoteMaxLength = 500;

    /// <summary>Định dạng ảnh nhận (trình duyệt hiển thị được).</summary>
    public static readonly IReadOnlySet<string> ImageTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "image/gif" };

    private Feedback() { } // EF Core

    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public string CustomerCode { get; private set; } = "";
    public string CompanyName { get; private set; } = "";
    /// <summary>Tài khoản con gửi (null = tài khoản chính).</summary>
    public long? StaffId { get; private set; }
    /// <summary>Tên đăng nhập của người gửi.</summary>
    public string SenderUserName { get; private set; } = "";
    public string Message { get; private set; } = "";
    public string? Contact { get; private set; }
    /// <summary>Khách chấm 1–5 sao (không bắt buộc).</summary>
    public int? Rating { get; private set; }
    /// <summary>Câu trả lời khảo sát dạng JSON (<c>FeedbackSurveyAnswer[]</c>, đã kiểm tra) — null nếu khách không trả lời.</summary>
    public string? SurveyJson { get; private set; }
    public bool IsSeen { get; private set; }
    public bool IsHiddenByCustomer { get; private set; }
    public DateTime CreateDate { get; private set; }
    public DateTime? SeenAt { get; private set; }
    public List<FeedbackImage> Images { get; private set; } = [];

    public static Feedback Create(long customerId, string customerCode, string companyName, long? staffId, string userName,
        string message, string? contact, int? rating, IEnumerable<FeedbackImage> images, DateTime now, string? surveyJson = null) => new()
    {
        CustomerId = customerId,
        CustomerCode = customerCode,
        CompanyName = companyName,
        StaffId = staffId,
        SenderUserName = userName,
        Message = message,
        Contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim(),
        Rating = rating,
        SurveyJson = surveyJson,
        CreateDate = now,
        Images = [.. images]
    };

    /// <summary>Ẩn ở trang khách; giữ nội dung và ảnh cho quản trị.</summary>
    public void HideFromCustomer() => IsHiddenByCustomer = true;

    /// <summary>Admin đánh dấu đã xem (lần đầu).</summary>
    public bool MarkSeen(DateTime now)
    {
        if (IsSeen) return false;
        IsSeen = true;
        SeenAt = now;
        return true;
    }
}

/// <summary>1 ảnh đính kèm góp ý (dbo.GopY_HinhAnh) — lưu thẳng trong database (Render không giữ file trên ổ đĩa).</summary>
internal sealed class FeedbackImage
{
    private FeedbackImage() { } // EF Core

    public FeedbackImage(string fileName, string contentType, byte[] data)
    {
        FileName = fileName;
        ContentType = contentType;
        Size = data.Length;
        Data = data;
    }

    public long Id { get; private set; }
    public long FeedbackId { get; private set; }
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public int Size { get; private set; }
    public byte[] Data { get; private set; } = [];
}
