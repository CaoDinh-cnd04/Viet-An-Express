
namespace VietAnExpress.Shipments.Catalog;

/// <summary>
/// Đánh dấu của khách trên danh mục khai hàng (bảng <c>dbo.MatHangKhachHang</c>):
/// mặt hàng trong thư viện (yêu thích / đã xóa khỏi thư viện) và nhóm hàng chung Việt An được khách đánh dấu yêu thích.
/// Thư viện mặt hàng vẫn gom từ dbo.MaVanDon_ChiTietHang — bảng này chỉ giữ lựa chọn của khách, không đụng đơn cũ.
/// </summary>
internal sealed class CatalogMark
{
    /// <summary>Loại đánh dấu: mặt hàng (khóa = <see cref="ProductLibrary.KeyOf"/>).</summary>
    public const string Product = "SP";
    /// <summary>Loại đánh dấu: nhóm hàng chung (khóa = ID trong dbo.NhomHangHoa).</summary>
    public const string Category = "NHOM";
    public const int KindMaxLength = 10;
    public const int KeyMaxLength = 64;

    public long Id { get; private set; }
    /// <summary>dbo.TCustomer.CustomerID.</summary>
    public long CustomerId { get; private set; }
    public string Kind { get; private set; } = "";
    public string Key { get; private set; } = "";
    public bool IsFavorite { get; private set; }
    /// <summary>Mặt hàng khách đã xóa khỏi thư viện (ẩn đi).</summary>
    public bool IsDeleted { get; private set; }
    public DateTime CreateDate { get; private set; }
    public DateTime? ModifyDate { get; private set; }

    private CatalogMark() { }

    public static CatalogMark Create(long customerId, string kind, string key, DateTime now) =>
        new() { CustomerId = customerId, Kind = kind, Key = key, CreateDate = now };

    public void SetFavorite(bool isFavorite, DateTime now)
    {
        IsFavorite = isFavorite;
        if (isFavorite) IsDeleted = false;
        ModifyDate = now;
    }

    public void MarkDeleted(DateTime now)
    {
        IsDeleted = true;
        IsFavorite = false;
        ModifyDate = now;
    }
}
