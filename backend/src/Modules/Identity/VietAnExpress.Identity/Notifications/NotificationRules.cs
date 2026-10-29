using System.Globalization;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Quy tắc thông báo — hàm thuần, có test.</summary>
internal static class NotificationRules
{
    public const int MaxItems = 10;

    public const int MaxCustomers = 500;

    public static Error? Validate(string? title, string? body, string? link = null)
    {
        var t = title?.Trim() ?? "";
        var b = body?.Trim() ?? "";
        if (t.Length is 0 or > Notification.TitleMaxLength) return NotificationErrors.TitleRequired;
        if (b.Length is 0 or > Notification.BodyMaxLength) return NotificationErrors.BodyRequired;
        if (!string.IsNullOrWhiteSpace(link) && !IsWebLink(link)) return NotificationErrors.Link;
        return null;
    }

    /// <summary>
    /// Tiêu đề thông báo + các tin: bỏ tin trống hoàn toàn; mỗi tin cần nội dung; có từ 2 tin thì mỗi tin cần tiêu đề;
    /// tối đa <see cref="MaxItems"/> tin, <see cref="NotificationItem.MaxImages"/> ảnh / tin; nút "Xem chi tiết" mở link http / https
    /// hoặc nội dung chi tiết (không cả hai). Nội dung có định dạng được lọc HTML; độ dài tính theo chữ (bỏ thẻ).
    /// Trả các tin đã chuẩn hóa (HTML đã lọc).
    /// </summary>
    public static Result<IReadOnlyList<NotificationItemInput>> ValidateContent(string? title, IReadOnlyList<NotificationItemInput>? items)
    {
        var t = title?.Trim() ?? "";
        if (t.Length is 0 or > Notification.TitleMaxLength) return NotificationErrors.TitleRequired;
        var list = (items ?? [])
            .Select(i => i with { Body = Clean(i.Body), Detail = Clean(i.Detail) })
            .Where(i => !string.IsNullOrWhiteSpace(i.Title) || RichText.ToPlainText(i.Body).Length > 0 || i.Images is { Count: > 0 }
                || !string.IsNullOrWhiteSpace(i.Link) || RichText.ToPlainText(i.Detail).Length > 0)
            .ToList();
        if (list.Count is 0 or > MaxItems) return NotificationErrors.ItemsRequired;
        for (var n = 0; n < list.Count; n++)
        {
            var item = list[n];
            var itemTitle = item.Title?.Trim() ?? "";
            var body = RichText.ToPlainText(item.Body);
            var detail = RichText.ToPlainText(item.Detail);
            if (itemTitle.Length > Notification.TitleMaxLength) return NotificationErrors.Item(n, $"tiêu đề tối đa {Notification.TitleMaxLength} ký tự");
            if (list.Count > 1 && itemTitle.Length == 0) return NotificationErrors.Item(n, "nhập tiêu đề tin (thông báo có nhiều tin)");
            if (body.Length is 0 or > NotificationItem.BodyTextMaxLength || (item.Body?.Length ?? 0) > NotificationItem.BodyHtmlMaxLength)
                return NotificationErrors.Item(n, $"nhập nội dung (tối đa {NotificationItem.BodyTextMaxLength} ký tự)");
            if ((item.Images?.Count ?? 0) > NotificationItem.MaxImages) return NotificationErrors.Item(n, $"tối đa {NotificationItem.MaxImages} ảnh");
            if (!string.IsNullOrWhiteSpace(item.Link) && !IsWebLink(item.Link))
                return NotificationErrors.Item(n, $"đường link phải bắt đầu bằng http:// hoặc https:// (tối đa {Notification.LinkMaxLength} ký tự)");
            if (detail.Length > NotificationItem.DetailTextMaxLength || (item.Detail?.Length ?? 0) > NotificationItem.DetailHtmlMaxLength)
                return NotificationErrors.Item(n, $"nội dung chi tiết tối đa {NotificationItem.DetailTextMaxLength} ký tự");
            if (!string.IsNullOrWhiteSpace(item.Link) && detail.Length > 0)
                return NotificationErrors.Item(n, "nút Xem chi tiết chỉ mở link hoặc nội dung chi tiết, không cả hai");
        }
        return list.Select(i => i with { Detail = RichText.ToPlainText(i.Detail).Length > 0 ? i.Detail : null }).ToList();
    }

    /// <summary>Có thẻ định dạng → lọc HTML; chữ thường giữ nguyên.</summary>
    private static string? Clean(string? value) => value is null ? null : RichText.IsRich(value) ? RichText.Sanitize(value).Trim() : value.Trim();

    /// <summary>Link tuyệt đối http / https (chặn javascript:, data:… vì khách bấm mở trực tiếp).</summary>
    public static bool IsWebLink(string value)
    {
        var v = value.Trim();
        return v.Length <= Notification.LinkMaxLength
            && Uri.TryCreate(v, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && !string.IsNullOrEmpty(uri.Host);
    }

    /// <summary>Mã khách nhập tay ("KH1, kh2\nKH3") → danh sách không trùng (không phân biệt hoa thường), giữ thứ tự.</summary>
    public static IReadOnlyList<string> CustomerCodes(IEnumerable<string>? codes) =>
        (codes ?? []).SelectMany(c => c.Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Trạng thái hiển thị ở trang quản trị.</summary>
    public static string StatusOf(Notification n, DateTime now) => n.Status switch
    {
        NotificationStatus.Draft => "draft",
        NotificationStatus.Removed => "removed",
        _ when n.PublishAt > now => "scheduled",
        _ when n.ExpiresAt is { } end && end < now => "expired",
        _ => "live"
    };

    /// <summary>"Hiện đến ngày" → hết ngày đó (23:59:59).</summary>
    public static DateTime? EndOfDay(DateOnly? date) => date?.ToDateTime(new TimeOnly(23, 59, 59));

    public static string Time(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Ảnh "/Upload/ThongBao/{tên}" → tên tệp (đường dẫn khác bị bỏ).</summary>
    public static IReadOnlyList<string> ImageNames(NotificationItem item) =>
        item.ImagePaths.Select(NotificationImageStore.NameOf).OfType<string>().ToList();

    public static IReadOnlyList<NotificationItemDto> Items(IEnumerable<NotificationItem> items) =>
        items.OrderBy(i => i.SortOrder).Select(i => new NotificationItemDto(i.Title, i.Body, ImageNames(i), i.Link, i.Detail)).ToList();
}
