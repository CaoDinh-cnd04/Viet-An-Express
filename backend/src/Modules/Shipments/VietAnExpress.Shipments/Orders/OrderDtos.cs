using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Orders;

// Dạng dữ liệu khớp hợp đồng frontend — web/docs/API_CONTRACT.md §1, §2.

internal sealed record OrderPodDto(string Date, string Time, string Signer);

internal sealed record OrderEventDto(string Time, string Title, string? Location);

internal sealed record OrderDto(
    string Id,
    long Seq,
    string Bill,
    string Ref,
    string? Connect,
    string Cnee,
    string Ct,
    string Route,
    string Branch,
    string Created,
    string? Sent,
    string Type,
    string St,
    string Pcs,
    string Content,
    OrderPodDto? Pod,
    int Photos,
    string? PodEstimate,
    OrderShipperDto? Shipper = null,
    OrderReceiverDto? Receiver = null,
    IReadOnlyList<OrderPackageDto>? Packages = null,
    OrderInvoiceDto? Invoice = null,
    /// <summary>VSVX mọi hãng ghi lúc cấp bill (Remote_Area_FedEx, Remote_Area_UPS, Remote_Area), vd "Fedex (Tier B), UPS"; null khi không thuộc vùng.</summary>
    string? RemoteArea = null,
    decimal? ChargeableWeight = null,
    /// <summary>Nhân viên Việt An tạo giúp khách (MaVanDon.CreateUser &gt; 0).</summary>
    bool CreatedByVietAn = false,
    /// <summary>Khách của đơn — chỉ có ở /vietan/orders (đơn của nhiều khách nhân viên Việt An phụ trách).</summary>
    OrderCustomerDto? Customer = null);

/// <summary>Khách của đơn (dbo.TCustomer): <c>Id</c> = CustomerID (để chọn làm thay), <c>Code</c> = mã khách, <c>Name</c> = tên công ty.</summary>
/// <param name="CreatedBy">
/// Tên đăng nhập tài khoản đã tạo đơn — chỉ nhân viên Việt An thấy (05/11/2026): nhân viên Việt An tạo giúp (MaVanDon.CreateUser → USERS.USER_NAME),
/// tài khoản con (Customer_Staff_ID → TCustomer_NguoiLienLac.TinhCach), còn lại tài khoản chính (TCustomer.Login_UserName); không rõ → null.
/// </param>
/// <param name="SenderName">Tên người gửi ghi trên vận đơn (MaVanDon.SenderName) — cột "Người gửi" của trang Đơn khách phụ trách (06/11/2026).</param>
internal sealed record OrderCustomerDto(string Id, string Code, string Name, string? CreatedBy = null, string? SenderName = null);

/// <summary>1 dòng kiện (dbo.MaVanDon_PCS_DIM) — chỉ trả ở GET /orders/{bill}. <c>WeightKg</c> là cân 1 kiện.</summary>
internal sealed record OrderPackageDto(int Qty, string PackType, int Length, int Width, int Height, decimal WeightKg,
    decimal? VolumetricWeight = null, decimal? ChargeableWeight = null);

/// <summary>1 dòng hàng invoice (dbo.MaVanDon_ChiTietHang).</summary>
internal sealed record OrderItemDto(string DescEn, string DescVi, decimal Qty, string Unit, decimal Price, decimal Amount, string Hs, string Origin,
    int? Package = null);

/// <summary>Invoice của vận đơn — chỉ trả ở GET /orders/{bill}.</summary>
internal sealed record OrderInvoiceDto(string Currency, string ExportType, decimal? ShippingFee, decimal? GoodsValue, IReadOnlyList<OrderItemDto> Items);

/// <summary>Người gửi / người nhận đầy đủ — chỉ trả ở GET /orders/{bill} (dùng cho "Nhân bản đơn"). Tên trường khớp form tạo đơn.</summary>
internal sealed record OrderShipperDto(string Company, string Contact, string Tel, string Address, string TaxId, string Email);

internal sealed record OrderReceiverDto(
    string Company, string Contact, string Tel, string Country, string City, string Postal, string State,
    string Addr1, string Addr2, string Addr3, string TaxId, string Email,
    string IossNo = "", string EoriNo = "", string CountryCode = "");

internal sealed record OrderSummaryDto(IReadOnlyDictionary<string, int> StatusCounts, int TotalPieces, decimal TotalWeight);

/// <summary>GET /orders — không bọc { success, data } vì frontend đọc thẳng các trường này.</summary>
internal sealed record OrderListResponse(
    IReadOnlyList<OrderDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages,
    OrderSummaryDto Summary);
