using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.AddressBook;

/// <summary>Người gửi đã dùng ở các đơn trước (dbo.MaVanDon) — tài khoản con gõ tên công ty / người gửi để chọn lại, khỏi nhập lại.</summary>
internal sealed record RecentSenderDto(
    string Company, string? Contact, string? Phone, string? Address, string? TaxId, string? Email, string? OriginalShipper, DateOnly? LastUsed);

/// <summary>Hàm thuần — có test.</summary>
internal static class RecentSenders
{
    /// <summary>
    /// Gộp dòng trùng (cùng tên + điện thoại + địa chỉ lấy hàng, không phân biệt hoa thường / khoảng trắng) theo đơn mới nhất;
    /// ô nào đơn mới nhất để trống thì lấy từ lần gần nhất có giá trị.
    /// </summary>
    public static IReadOnlyList<RecentSenderDto> Distinct(IEnumerable<RecentSenderDto> rows, int limit) =>
        [.. rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Company))
            .Select(Trim)
            .GroupBy(r => (Key(r.Company), Key(r.Phone), Key(r.Address)))
            .Select(g =>
            {
                var all = g.ToList();
                string? First(Func<RecentSenderDto, string?> field) => all.Select(field).FirstOrDefault(v => v is not null);
                return all[0] with
                {
                    Contact = First(r => r.Contact), TaxId = First(r => r.TaxId), Email = First(r => r.Email), OriginalShipper = First(r => r.OriginalShipper)
                };
            })
            .Take(limit)];

    private static string Key(string? value) =>
        string.Join(' ', (value ?? "").ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? T(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RecentSenderDto Trim(RecentSenderDto r) => r with
    {
        Company = r.Company.Trim(), Contact = T(r.Contact), Phone = T(r.Phone), Address = T(r.Address),
        TaxId = T(r.TaxId), Email = T(r.Email), OriginalShipper = T(r.OriginalShipper)
    };
}
