
namespace VietAnExpress.Identity.Notifications;

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
