namespace VietAnExpress.Identity.Domain;

/// <summary>Trạng thái gửi email của 1 thông báo.</summary>
internal static class NotificationEmailStatus
{
    /// <summary>Không chọn gửi email.</summary>
    public const string None = "none";
    /// <summary>Chờ gửi: nháp có chọn gửi email, hoặc thông báo hẹn giờ chưa tới giờ đăng.</summary>
    public const string Scheduled = "scheduled";
    /// <summary>Đã xếp hàng, đang gửi ở nền.</summary>
    public const string Sending = "sending";
    public const string Sent = "sent";
    /// <summary>Gửi được một phần (có lô lỗi hoặc vượt giới hạn người nhận).</summary>
    public const string Partial = "partial";
    public const string Failed = "failed";
    /// <summary>Không có khách nào có email hợp lệ.</summary>
    public const string NoRecipients = "no_recipients";

    public const int MaxLength = 20;

    /// <summary>Email đã (hoặc đang) gửi — sửa thông báo không gửi lại.</summary>
    public static bool Started(string status) => status is not (None or Scheduled);
}

/// <summary>Trạng thái đăng (dbo.TThongBao.Trang_Thai).</summary>
internal static class NotificationStatus
{
    public const int Draft = 0;
    /// <summary>Đã đăng — khách thấy từ <see cref="Notification.PublishAt"/> tới <see cref="Notification.ExpiresAt"/>.</summary>
    public const int Published = 1;
    /// <summary>Đã gỡ khỏi portal của khách (vẫn xem lại được ở quản trị).</summary>
    public const int Removed = 2;
}

/// <summary>Đối tượng nhận thông báo quản trị đăng (dbo.TThongBao.Doi_Tuong).</summary>
internal static class NotificationAudience
{
    /// <summary>Mọi khách có tài khoản portal.</summary>
    public const string All = "all";
    /// <summary>Khách có <c>TCustomer.CustomerTypeID</c> thuộc <see cref="Notification.CustomerTypes"/>.</summary>
    public const string Types = "type";
    /// <summary>Khách chỉ định ở cột dbo.TThongBao.Khach_Nhan.</summary>
    public const string Customers = "pick";

    public const int MaxLength = 10;

    public static bool IsValid(string? value) => value is All or Types or Customers;
}

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

/// <summary>
/// 1 tin trong thông báo (1 phần tử của cột JSON dbo.TThongBao.Tin): tiêu đề, nội dung (chữ thường hoặc HTML có định dạng), ảnh và nút "Xem chi tiết"
/// mở <see cref="Link"/> hoặc mở <see cref="Detail"/> (nội dung chi tiết admin soạn) — chỉ 1 trong 2.
/// </summary>
internal sealed class NotificationItem
{
    public const int MaxImages = 5;
    /// <summary>Số ký tự chữ (bỏ thẻ định dạng) tối đa của nội dung tin / nội dung chi tiết.</summary>
    public const int BodyTextMaxLength = 4000;
    public const int DetailTextMaxLength = 20000;
    /// <summary>Độ dài HTML tối đa (chữ + thẻ định dạng).</summary>
    public const int BodyHtmlMaxLength = 30000;
    public const int DetailHtmlMaxLength = 150000;

    private NotificationItem() { } // EF Core

    public NotificationItem(string? title, string body, IReadOnlyList<string> images, string? link, string? detail = null)
    {
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Body = body.Trim();
        Images = images.Count == 0 ? null : string.Join('\n', images);
        Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
        Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
    }

    public int SortOrder { get; private set; }
    public string? Title { get; private set; }
    public string Body { get; private set; } = "";
    /// <summary>Đường dẫn ảnh "/Upload/ThongBao/{tên}" theo thứ tự, mỗi dòng 1 ảnh.</summary>
    public string? Images { get; private set; }
    public string? Link { get; private set; }
    /// <summary>Nội dung chi tiết (HTML có định dạng) mở bằng nút "Xem chi tiết"; null = nút mở <see cref="Link"/> hoặc không có nút.</summary>
    public string? Detail { get; private set; }

    public IReadOnlyList<string> ImagePaths => (Images ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal NotificationItem WithOrder(int order)
    {
        SortOrder = order;
        return this;
    }
}

/// <summary>1 tài khoản đã đọc 1 thông báo (dbo.TThongBao_DaDoc). <see cref="StaffId"/> = 0: tài khoản chính của khách.</summary>
internal sealed class NotificationRead
{
    private NotificationRead() { } // EF Core

    public NotificationRead(long notificationId, long customerId, long staffId, DateTime now)
    {
        NotificationId = notificationId;
        CustomerId = customerId;
        StaffId = staffId;
        ReadAt = now;
    }

    public long Id { get; private set; }
    public long NotificationId { get; private set; }
    public long CustomerId { get; private set; }
    public long StaffId { get; private set; }
    public DateTime ReadAt { get; private set; }
}
