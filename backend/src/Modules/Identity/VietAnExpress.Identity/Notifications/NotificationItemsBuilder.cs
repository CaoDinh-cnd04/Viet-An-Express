namespace VietAnExpress.Identity.Notifications;

/// <summary>Dựng các tin của thông báo từ nội dung đã kiểm tra (dùng chung cho Lưu và Gửi thử email).</summary>
internal static class NotificationItemsBuilder
{
    /// <summary>Tin đã kiểm tra → NotificationItem (ảnh lưu dạng /Upload/ThongBao/…); null nếu có ảnh không còn trên máy chủ.</summary>
    public static List<NotificationItem>? Build(IEnumerable<NotificationItemInput> inputs, NotificationImageStore images)
    {
        var items = new List<NotificationItem>();
        foreach (var input in inputs)
        {
            var paths = (input.Images ?? []).Select(NotificationImageStore.StoredPath).ToList();
            if (paths.Any(p => !images.Exists(p))) return null;
            items.Add(new NotificationItem(input.Title, input.Body!, paths, input.Link, input.Detail));
        }
        return items;
    }
}
