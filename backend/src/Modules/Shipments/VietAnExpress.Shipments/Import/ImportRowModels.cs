using System.Text.RegularExpressions;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Import;

/// <summary>Nước đến theo mã ISO 2 ký tự: tên tiếng Anh (lưu vào đơn như hệ thống cũ) và mã điện thoại.</summary>
internal sealed record ImportCountry(string Code, string Name, string? DialCode);

/// <summary>Thông tin dùng chung cho cả file: khách đang đăng nhập, dịch vụ / hub chọn trên trang, danh mục nước.</summary>
internal sealed record ImportContext(
    LegacyCustomerRef Customer, string Service, string Hub, string Branch, IReadOnlyDictionary<string, ImportCountry> Countries);

/// <summary>Kết quả kiểm tra 1 dòng: đơn dựng được (khi không có lỗi) + lỗi / cảnh báo + số liệu để xem trước.</summary>
internal sealed record ImportedRow(int Line, OrderPayload Payload, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
    public decimal GrossKg { get; init; }
    public decimal ChargeableKg { get; init; }
    public decimal Value { get; init; }
    public string CountryCode { get; init; } = "";
}
