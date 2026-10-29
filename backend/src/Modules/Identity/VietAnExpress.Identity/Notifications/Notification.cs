
namespace VietAnExpress.Identity.Notifications;

/// <summary>
/// Thông báo trong Trung tâm thông báo (dbo.TThongBao): quản trị Việt An soạn gửi khách (theo đối tượng, có thể kèm email),
/// hoặc hệ thống gửi riêng 1 khách (<see cref="CustomerId"/>, vd cập nhật hỗ trợ &amp; sự cố). Nội dung quản trị soạn nằm ở
/// các tin <see cref="Items"/> (cột JSON Tin); <see cref="Body"/> giữ bản chữ tóm tắt (thông báo cũ / thông báo hệ thống chỉ có Body + Link).
/// </summary>
internal sealed class Notification
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 4000;
    public const int ErrorMaxLength = 500;
    public const int LinkMaxLength = 500;
    public const int CustomerTypesMaxLength = 200;

    private Notification() { } // EF Core

    /// <summary>Thông báo hệ thống gửi riêng 1 khách (đăng ngay, không có tin con).</summary>
    public static Notification Create(string title, string body, bool important, bool sendEmail, string createdBy, DateTime now, string? link = null,
        long? customerId = null) => new()
    {
        CustomerId = customerId,
        Title = title.Trim(),
        Body = body.Trim(),
        Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
        Important = important,
        CreatedBy = createdBy,
        CreatedAt = now,
        PublishAt = now,
        Status = NotificationStatus.Published,
        EmailStatus = sendEmail ? NotificationEmailStatus.Sending : NotificationEmailStatus.None
    };

    /// <summary>Thông báo quản trị soạn — nội dung đặt bằng <see cref="SetContent"/>.</summary>
    public static Notification Draft(string createdBy, DateTime now) => new()
    {
        CreatedBy = createdBy,
        CreatedAt = now,
        PublishAt = now,
        Status = NotificationStatus.Draft
    };

    public long Id { get; private set; }
    /// <summary>Gửi riêng 1 khách (vd phản hồi sự cố); null = thông báo quản trị đăng theo <see cref="Audience"/>.</summary>
    public long? CustomerId { get; private set; }
    public string Title { get; private set; } = "";
    /// <summary>Bản chữ tóm tắt / nội dung của thông báo không có tin con.</summary>
    public string Body { get; private set; } = "";
    /// <summary>Đường link "Xem tại đây" của thông báo không có tin con (http / https, không bắt buộc).</summary>
    public string? Link { get; private set; }
    /// <summary>Quan trọng → hiện popup khi khách mở portal.</summary>
    public bool Important { get; private set; }
    /// <summary>Ghim lên đầu danh sách của khách.</summary>
    public bool Pinned { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = "";
    /// <summary>Giờ khách bắt đầu thấy (hẹn giờ đăng); thông báo hệ thống = giờ tạo.</summary>
    public DateTime PublishAt { get; private set; }
    /// <summary>Khách thấy tới hết thời điểm này; null = không hết hạn.</summary>
    public DateTime? ExpiresAt { get; private set; }
    /// <summary><see cref="NotificationStatus"/>.</summary>
    public int Status { get; private set; }
    /// <summary><see cref="NotificationAudience"/>.</summary>
    public string Audience { get; private set; } = NotificationAudience.All;
    /// <summary>Loại khách nhận (CustomerTypeID) dạng ",3,4," để lọc bằng LIKE; chỉ dùng khi <see cref="Audience"/> = type.</summary>
    public string? CustomerTypes { get; private set; }
    public string EmailStatus { get; private set; } = NotificationEmailStatus.None;
    /// <summary>Số địa chỉ email định gửi.</summary>
    public int EmailRecipients { get; private set; }
    /// <summary>Số địa chỉ đã gửi thành công.</summary>
    public int EmailSent { get; private set; }
    public string? EmailError { get; private set; }

    /// <summary>Khách chỉ định (CustomerID) dạng ",201008,200877," để lọc bằng LIKE; chỉ dùng khi <see cref="Audience"/> = pick.</summary>
    public string? Recipients { get; private set; }

    public List<NotificationItem> Items { get; private set; } = [];

    public IReadOnlyList<int> CustomerTypeIds => ParseTypes(CustomerTypes);
    public IReadOnlyList<long> RecipientIds => ParseIds(Recipients);

    /// <summary>Ghi nội dung + cài đặt hiển thị (thay toàn bộ tin con và khách nhận).</summary>
    public void SetContent(string title, IEnumerable<NotificationItem> items, bool important, bool pinned, DateTime? expiresAt,
        string audience, IReadOnlyCollection<int> customerTypes, IReadOnlyCollection<long> customerIds)
    {
        Title = title.Trim();
        Items = items.Select((item, index) => item.WithOrder(index)).ToList();
        Body = Summary(Items);
        Link = null;
        Important = important;
        Pinned = pinned;
        ExpiresAt = expiresAt;
        Audience = audience;
        CustomerTypes = audience == NotificationAudience.Types ? FormatTypes(customerTypes) : null;
        Recipients = audience == NotificationAudience.Customers ? FormatIds(customerIds) : null;
    }

    /// <summary>Lưu nháp: <paramref name="sendEmail"/> được nhớ để gửi khi đăng (chưa gửi lần nào).</summary>
    public void SaveAsDraft(bool sendEmail)
    {
        Status = NotificationStatus.Draft;
        if (!NotificationEmailStatus.Started(EmailStatus))
            EmailStatus = sendEmail ? NotificationEmailStatus.Scheduled : NotificationEmailStatus.None;
    }

    /// <summary>
    /// Đăng (hoặc đăng lại sau khi sửa / gỡ) vào <paramref name="publishAt"/> (trống hoặc đã qua = ngay).
    /// Trả true khi cần xếp việc gửi email ngay; email đã gửi thì không gửi lại.
    /// </summary>
    public bool Publish(DateTime? publishAt, bool sendEmail, DateTime now)
    {
        var wasPublished = Status == NotificationStatus.Published && PublishAt <= now;
        Status = NotificationStatus.Published;
        // Hẹn giờ tương lai → đăng lúc đó; thông báo đang hiện giữ giờ đăng cũ khi sửa (không nhảy lên đầu danh sách của khách).
        PublishAt = publishAt is { } at && at > now ? at : wasPublished ? PublishAt : now;
        if (NotificationEmailStatus.Started(EmailStatus)) return false;
        if (!sendEmail)
        {
            EmailStatus = NotificationEmailStatus.None;
            return false;
        }
        EmailStatus = PublishAt <= now ? NotificationEmailStatus.Sending : NotificationEmailStatus.Scheduled;
        return EmailStatus == NotificationEmailStatus.Sending;
    }

    /// <summary>Gỡ khỏi portal của khách; email đang chờ gửi bị hủy.</summary>
    public void Remove()
    {
        Status = NotificationStatus.Removed;
        if (EmailStatus == NotificationEmailStatus.Scheduled) EmailStatus = NotificationEmailStatus.None;
    }

    /// <summary>Tới giờ đăng của thông báo hẹn giờ có email → chuyển sang đang gửi.</summary>
    public bool StartScheduledEmail(DateTime now)
    {
        if (Status != NotificationStatus.Published || EmailStatus != NotificationEmailStatus.Scheduled || PublishAt > now) return false;
        EmailStatus = NotificationEmailStatus.Sending;
        return true;
    }

    /// <summary>Ghi kết quả gửi email ở nền.</summary>
    public void RecordEmail(int recipients, int sent, string? error)
    {
        EmailRecipients = recipients;
        EmailSent = sent;
        EmailError = error is null ? null : error.Length <= ErrorMaxLength ? error : error[..ErrorMaxLength];
        EmailStatus = recipients == 0 ? NotificationEmailStatus.NoRecipients
            : sent == 0 ? NotificationEmailStatus.Failed
            : sent < recipients || error is not null ? NotificationEmailStatus.Partial
            : NotificationEmailStatus.Sent;
    }

    /// <summary>Bản chữ thuần tóm tắt ghi vào Noi_Dung (CMS / tìm kiếm): 1 tin → nội dung tin; nhiều tin → tiêu đề + nội dung từng tin.</summary>
    public static string Summary(IReadOnlyList<NotificationItem> items)
    {
        var text = items.Count == 1
            ? RichText.ToPlainText(items[0].Body)
            : string.Join("\n\n", items.Select((i, n) => $"{n + 1}. {i.Title}\n{RichText.ToPlainText(i.Body)}"));
        return text.Length <= BodyMaxLength ? text : text[..(BodyMaxLength - 1)] + "…";
    }

    public static string? FormatTypes(IEnumerable<int> types) =>
        types.Distinct().Order().ToList() is { Count: > 0 } list ? "," + string.Join(',', list) + "," : null;

    public static IReadOnlyList<int> ParseTypes(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var v) ? v : (int?)null).OfType<int>().ToList();

    /// <summary>Khách chỉ định → ",201008,200877," (giữ thứ tự, bỏ trùng); trống → null.</summary>
    public static string? FormatIds(IEnumerable<long> ids) =>
        ids.Distinct().ToList() is { Count: > 0 } list ? "," + string.Join(',', list) + "," : null;

    public static IReadOnlyList<long> ParseIds(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => long.TryParse(s, out var v) ? v : (long?)null).OfType<long>().ToList();

    /// <summary>Mẫu tìm 1 khách trong <see cref="Recipients"/>: ",id,".</summary>
    public static string Token(long id) => "," + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",";
}
