using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.AddressBook;

/// <summary>Người nhận khách đã gửi trước đây (lấy từ đơn trong dbo.MaVanDon) — để chọn lại khi tạo đơn, khỏi nhập lại.</summary>
internal sealed record RecentReceiverDto(
    string Company, string? Contact, string? Phone, string? PhoneCode, string? Email, string? TaxId,
    string? Country, string? City, string? State, string? PostalCode,
    string? Address1, string? Address2, string? Address3, string? IossNo, string? EoriNo, DateOnly? LastUsed);

/// <summary>Hàm thuần — có test.</summary>
internal static class RecentReceivers
{
    /// <summary>
    /// Gộp dòng trùng (cùng tên công ty + số điện thoại + địa chỉ 1, không phân biệt hoa thường / khoảng trắng) thành 1 dòng
    /// theo đơn mới nhất (đầu vào đã xếp mới nhất trước); ô nào đơn mới nhất để trống (vd mã bưu chính, tỉnh, email)
    /// thì lấy từ lần gửi gần nhất có giá trị. Địa chỉ 2 / 3 giữ theo đơn mới nhất.
    /// </summary>
    public static IReadOnlyList<RecentReceiverDto> Distinct(IEnumerable<RecentReceiverDto> rows, int limit) =>
        [.. rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Company))
            .Select(Trim)
            .GroupBy(r => (Key(r.Company), Key(r.Phone), Key(r.Address1)))
            .Select(Merge)
            .Take(limit)];

    private static RecentReceiverDto Merge(IEnumerable<RecentReceiverDto> sameReceiver)
    {
        var all = sameReceiver.ToList();
        string? First(Func<RecentReceiverDto, string?> field) => all.Select(field).FirstOrDefault(v => v is not null);
        return all[0] with
        {
            Contact = First(r => r.Contact), PhoneCode = First(r => r.PhoneCode), Email = First(r => r.Email), TaxId = First(r => r.TaxId),
            Country = First(r => r.Country), City = First(r => r.City), State = First(r => r.State), PostalCode = First(r => r.PostalCode),
            IossNo = First(r => r.IossNo), EoriNo = First(r => r.EoriNo)
        };
    }

    private static string Key(string? value) =>
        string.Join(' ', (value ?? "").ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? T(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RecentReceiverDto Trim(RecentReceiverDto r) => r with
    {
        Company = r.Company.Trim(), Contact = T(r.Contact), Phone = T(r.Phone), PhoneCode = T(r.PhoneCode), Email = T(r.Email),
        TaxId = T(r.TaxId), Country = T(r.Country), City = T(r.City), State = T(r.State), PostalCode = T(r.PostalCode),
        Address1 = T(r.Address1), Address2 = T(r.Address2), Address3 = T(r.Address3), IossNo = T(r.IossNo), EoriNo = T(r.EoriNo)
    };
}
