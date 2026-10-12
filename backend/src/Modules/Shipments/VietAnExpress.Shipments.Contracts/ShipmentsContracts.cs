namespace VietAnExpress.Shipments.Contracts;

public static class ShipmentsPermissions
{
    public const string View = "shipments.view";
    public const string Create = "shipments.create";
    public const string Update = "shipments.update";
    public const string IssueBill = "shipments.issue-bill";
    /// <summary>Tài khoản con xem được mọi đơn của công ty; không có thì chỉ thấy đơn mình tạo. Tài khoản chính luôn thấy tất cả.</summary>
    public const string ViewAll = "shipments.view-all";
}

/// <summary>Cổng gọi đồng bộ vào module Shipments cho module khác (vận đơn ở dbo.MaVanDon).</summary>
public interface IShipmentsApi
{
    /// <summary>
    /// Vận đơn của khách theo mã bill (VA Bill, số VA 7 chữ số hoặc mã hãng). Không có / thuộc khách khác → null.
    /// </summary>
    Task<BillSummary?> FindBillAsync(long customerId, string bill, CancellationToken cancellationToken = default);
}

/// <summary>Thông tin vận đơn đủ dùng cho module khác: mã bill hiển thị, người nhận, quốc gia.</summary>
public sealed record BillSummary(string Bill, string? ConsigneeName, string? ConsigneeCountry);
