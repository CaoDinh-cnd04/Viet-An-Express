
namespace VietAnExpress.Shipments.Catalog;

/// <summary>
/// Nhóm hàng hóa khi khai nội dung hàng (bảng <c>dbo.NhomHangHoa</c>).
/// <see cref="CustomerId"/> null = nhóm chung Việt An cho mọi khách (chỉ đọc); có giá trị = nhóm khách tự tạo, được sửa / xóa.
/// </summary>
internal sealed class GoodsCategory
{
    public const int NameMaxLength = 150;

    public long Id { get; private set; }
    /// <summary>dbo.TCustomer.CustomerID của khách tạo nhóm; null = nhóm chung.</summary>
    public long? CustomerId { get; private set; }
    public string Name { get; private set; } = "";
    public bool IsFavorite { get; private set; }
    /// <summary>Thứ tự hiển thị của nhóm chung.</summary>
    public int SortOrder { get; private set; }
    public DateTime CreateDate { get; private set; }
    public DateTime? ModifyDate { get; private set; }

    private GoodsCategory() { }

    public static GoodsCategory Create(long customerId, string name, bool isFavorite, DateTime now) =>
        new() { CustomerId = customerId, Name = name, IsFavorite = isFavorite, CreateDate = now };

    public void Update(string name, bool isFavorite, DateTime now)
    {
        Name = name;
        IsFavorite = isFavorite;
        ModifyDate = now;
    }

    /// <summary>Chuẩn hóa tên: bỏ khoảng trắng thừa ở giữa và hai đầu.</summary>
    public static string NormalizeName(string? name) =>
        string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
