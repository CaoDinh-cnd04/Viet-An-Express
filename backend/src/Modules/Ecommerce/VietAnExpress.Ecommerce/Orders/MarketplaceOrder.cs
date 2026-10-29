namespace VietAnExpress.Ecommerce.Orders;

/// <summary>
/// Đơn E-commerce của khách (bảng <c>dbo.DonTMDT</c>) — khớp <c>EcomOrder</c> của frontend.
/// Nguồn: kênh bán đã kết nối (TikTok Shop…), sau này cả nhập tay / Excel / API. Chống trùng theo (khách, kênh, mã đơn sàn).
/// </summary>
internal sealed class MarketplaceOrder
{
    public const int PlatformOrderIdMaxLength = 100;
    public const int OrderNameMaxLength = 100;
    public const int BillMaxLength = 50;
    public const int StatusMaxLength = 20;
    public const int NameMaxLength = 200;
    public const int PhoneMaxLength = 50;
    public const int EmailMaxLength = 255;
    public const int AddressMaxLength = 255;
    public const int CityMaxLength = 100;
    public const int PostalMaxLength = 20;
    public const int CountryNameMaxLength = 100;
    public const int NoteMaxLength = 1000;
    public const int ServiceMaxLength = 50;
    public const int HubMaxLength = 100;
    public const int BranchMaxLength = 50;
    /// <summary>Nguồn của đơn nhập tay trên portal.</summary>
    public const string ManualSource = "manual";

    /// <summary>Trạng thái — trùng <c>EcomStatus</c> của frontend.</summary>
    public const string Created = "created";
    public static readonly string[] Statuses = [Created, "picked_up", "departed", "delivered", "exception", "weighing"];

    public long Id { get; private set; }
    /// <summary>dbo.TCustomer.CustomerID.</summary>
    public long CustomerId { get; private set; }
    /// <summary>dbo.KetNoiTMDT.ID — null với đơn nhập tay / Excel / API.</summary>
    public long? StoreConnectionId { get; private set; }
    /// <summary>Nguồn: mã kênh (tiktok, amazon…) hoặc manual / excel / api.</summary>
    public string Source { get; private set; } = "";
    /// <summary>Id đơn trên sàn.</summary>
    public string? PlatformOrderId { get; private set; }
    /// <summary>Mã đơn hiển thị của shop, vd "#1001".</summary>
    public string OrderName { get; private set; } = "";
    /// <summary>Số vận đơn Việt An (dbo.MaVanDon) khi đã cấp bill.</summary>
    public string? Bill { get; private set; }
    public string Status { get; private set; } = Created;
    public MarketplaceRecipient Recipient { get; private set; } = MarketplaceRecipient.Empty;
    public int ItemCount { get; private set; }
    public decimal? WeightKg { get; private set; }
    public string? Currency { get; private set; }
    public decimal? TotalAmount { get; private set; }
    /// <summary>Dòng sản phẩm dạng JSON (mảng <c>EcomProduct</c>).</summary>
    public string? ProductsJson { get; private set; }
    public string? Note { get; private set; }
    public DateTime? PlacedAt { get; private set; }
    /// <summary>Đơn nhập tay: hãng / dịch vụ, hub, chi nhánh gửi do khách chọn (đơn từ sàn để trống — chọn khi tạo bill).</summary>
    public string? Service { get; private set; }
    public string? Hub { get; private set; }
    public string? Branch { get; private set; }
    /// <summary>Khai báo hải quan nâng cao (JSON) — IOSS, EORI, VAT, giá trị khai…</summary>
    public string? CustomsJson { get; private set; }
    public DateTime? TrackingPushedAt { get; private set; }
    /// <summary>Khách đã sửa tay (địa chỉ Latin, cân nặng, mã HS…) — đồng bộ lại từ sàn không ghi đè nữa.</summary>
    public DateTime? EditedAt { get; private set; }
    /// <summary>Khách đã xóa (ẩn) — giữ dòng để đồng bộ / nhập lại từ sàn không tạo lại đơn này.</summary>
    public DateTime? DeletedAt { get; private set; }
    /// <summary>Tài khoản con đã tạo đơn (dbo.TaiKhoanNhanVien.ID) — đơn nhập tay / nhập CSV; null = tài khoản chính hoặc đồng bộ từ sàn.</summary>
    public long? CreatedByStaffId { get; private set; }
    /// <summary>Khách đã xác nhận gửi → nằm ở tab "Đơn hàng của tôi" (vẫn ở dbo.DonTMDT, không ghi dbo.MaVanDon).</summary>
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime CreateDate { get; private set; }
    public DateTime? ModifyDate { get; private set; }

    private MarketplaceOrder() { }

    public static MarketplaceOrder Import(long customerId, long? storeConnectionId, string source, ImportedOrder o, DateTime now,
        long? createdByStaffId = null)
    {
        var order = new MarketplaceOrder
        {
            CustomerId = customerId, StoreConnectionId = storeConnectionId, Source = source,
            PlatformOrderId = o.PlatformOrderId, CreateDate = now, CreatedByStaffId = createdByStaffId
        };
        order.Apply(o);
        return order;
    }

    /// <summary>Đơn khách nhập tay trên portal (không gắn shop đã kết nối).</summary>
    public static MarketplaceOrder CreateManual(long customerId, string source, ImportedOrder o, ManualShipping shipping, DateTime now,
        long? createdByStaffId = null)
    {
        var order = new MarketplaceOrder
        {
            CustomerId = customerId, Source = source, CreateDate = now, CreatedByStaffId = createdByStaffId,
            // Khách tự nhập = đã muốn gửi → vào thẳng "Đơn hàng của tôi".
            ConfirmedAt = now,
            Service = shipping.Service, Hub = shipping.Hub, Branch = shipping.Branch, CustomsJson = shipping.CustomsJson
        };
        order.Apply(o);
        return order;
    }

    /// <summary>Sàn đổi thông tin đơn: chỉ cập nhật khi chưa cấp bill (bill đã in thì giữ nguyên dữ liệu đã khai).</summary>
    public bool UpdateFrom(ImportedOrder o, DateTime now)
    {
        if (Bill is not null || EditedAt is not null || DeletedAt is not null || ConfirmedAt is not null) return false;
        Apply(o);
        ModifyDate = now;
        return true;
    }

    /// <summary>Xác nhận gửi (đơn đã đủ trường bắt buộc — kiểm ở Application). Đồng bộ lại từ sàn không ghi đè đơn đã xác nhận.</summary>
    public bool Confirm(DateTime now)
    {
        if (ConfirmedAt is not null || DeletedAt is not null) return false;
        ConfirmedAt = now;
        ModifyDate = now;
        return true;
    }

    /// <summary>Trả đơn đã xác nhận (chưa có bill) về tab Đơn hàng.</summary>
    public bool Unconfirm(DateTime now)
    {
        if (ConfirmedAt is null || Bill is not null || DeletedAt is not null) return false;
        ConfirmedAt = null;
        ModifyDate = now;
        return true;
    }

    /// <summary>Đơn đồng bộ từ shop đã kết nối: phản ánh đơn trên sàn — không xóa trong portal, đồng bộ luôn hiện lại.</summary>
    public bool IsSynced => StoreConnectionId is not null;

    /// <summary>Xóa được: đơn nhập tay / nhập file, chưa xác nhận gửi, chưa có bill.</summary>
    public bool CanDelete => !IsSynced && Bill is null && ConfirmedAt is null && DeletedAt is null;

    /// <summary>
    /// Khách xóa đơn (xóa mềm). Đơn đã xác nhận gửi, đã có bill, hoặc đơn đồng bộ từ shop (muốn bỏ thì hủy / hoàn tất trên sàn)
    /// thì không xóa được.
    /// </summary>
    public bool Delete(DateTime now)
    {
        if (!CanDelete) return false;
        DeletedAt = now;
        ModifyDate = now;
        return true;
    }

    /// <summary>
    /// Đơn đã xóa hiện lại với dữ liệu mới (bỏ cả dữ liệu đã sửa trước khi xóa): khách nhập lại file có đơn đó,
    /// hoặc đồng bộ gặp lại đơn của shop đã kết nối (đơn đồng bộ bị xóa trước khi có quy tắc không xóa đơn đồng bộ).
    /// </summary>
    public bool Restore(ImportedOrder o, DateTime now)
    {
        if (DeletedAt is null || Bill is not null) return false;
        DeletedAt = null;
        EditedAt = null;
        ConfirmedAt = null;
        Apply(o);
        ModifyDate = now;
        return true;
    }

    /// <summary>Khách sửa đơn trước khi tạo bill. Đã có bill thì không sửa được (dữ liệu đã khai cho hãng).</summary>
    public bool Edit(MarketplaceRecipient recipient, decimal? weightKg, string? productsJson, int itemCount, decimal? totalAmount,
        string? service, string? hub, string? branch, string? note, DateTime now)
    {
        if (Bill is not null) return false;
        Recipient = recipient;
        WeightKg = weightKg;
        ProductsJson = productsJson;
        ItemCount = itemCount;
        if (totalAmount is not null) TotalAmount = totalAmount;
        Service = service;
        Hub = hub;
        Branch = branch;
        Note = note;
        EditedAt = now;
        ModifyDate = now;
        return true;
    }

    private void Apply(ImportedOrder o)
    {
        OrderName = o.OrderName;
        Recipient = o.Recipient;
        ItemCount = o.ItemCount;
        WeightKg = o.WeightKg;
        Currency = o.Currency;
        TotalAmount = o.TotalAmount;
        ProductsJson = o.ProductsJson;
        Note = o.Note;
        PlacedAt = o.PlacedAt;
    }
}
