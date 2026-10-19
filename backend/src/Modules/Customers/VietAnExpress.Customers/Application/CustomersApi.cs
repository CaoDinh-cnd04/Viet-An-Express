using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.Infrastructure;

namespace VietAnExpress.Customers.Application;

internal sealed class CustomersApi(CustomersDbContext db) : ICustomersApi
{
    public async Task<CustomerSummary?> GetByIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        return c is null ? null : ToSummary(c);
    }

    public async Task<IReadOnlyList<CustomerSummary>> GetByIdsAsync(IReadOnlyCollection<long> customerIds, CancellationToken cancellationToken = default)
    {
        var result = new List<CustomerSummary>(customerIds.Count);
        // Chia lô: SQL Server giới hạn ~2100 tham số mỗi câu lệnh.
        foreach (var chunk in customerIds.Distinct().Chunk(1000))
        {
            var rows = await db.Customers.AsNoTracking().Where(x => chunk.Contains(x.Id)).ToListAsync(cancellationToken);
            result.AddRange(rows.Select(ToSummary));
        }
        return result;
    }

    public async Task<AccountManager?> GetAccountManagerAsync(long customerId, CancellationToken cancellationToken = default) =>
        (await GetAccountManagersAsync([customerId], cancellationToken)).GetValueOrDefault(customerId);

    public async Task<IReadOnlyDictionary<long, AccountManager>> GetAccountManagersAsync(IReadOnlyCollection<long> customerIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<long, AccountManager>();
        foreach (var chunk in customerIds.Distinct().Chunk(1000))
        {
            var rows = await db.Customers.AsNoTracking()
                .Where(c => chunk.Contains(c.Id) && c.AccountManagerId > 0)
                .Join(db.VietAnUsers.AsNoTracking(), c => c.AccountManagerId, u => u.Id, (c, u) => new { c.Id, User = u })
                .ToListAsync(cancellationToken);
            foreach (var row in rows)
                if (ToAccountManager(row.User) is { } manager) result[row.Id] = manager;
        }
        return result;
    }

    /// <summary>
    /// Nhân viên CMS → thông tin hiển thị: họ tên = FIRST_NAME + LAST_NAME (thiếu thì tên đăng nhập), SĐT di động → SĐT cơ quan (bỏ giá trị không phải số điện thoại),
    /// đang hoạt động khi STATUS = ACTIVE. Nhân viên đã xóa (DELETED) coi như chưa phân công → null. Hàm thuần — có test.
    /// </summary>
    internal static AccountManager? ToAccountManager(VietAnUser u)
    {
        if (u.Deleted == true) return null;
        var name = string.Join(' ', new[] { u.FirstName, u.LastName }.Select(Clean).OfType<string>());
        var userName = Clean(u.UserName) ?? u.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new AccountManager(u.Id, userName, name.Length > 0 ? name : userName, Clean(u.Email), Phone(u.MobilePhone) ?? Phone(u.WorkPhone),
            u.BranchId, string.Equals(Clean(u.Status), "ACTIVE", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Ô SĐT của CMS có khi ghi ngày sinh (vd "01/01/1987") — chỉ nhận chuỗi giống số điện thoại: ≥ 8 chữ số, không có "/".</summary>
    internal static string? Phone(string? value) =>
        Clean(value) is { } v && !v.Contains('/') && v.Count(char.IsAsciiDigit) >= 8 ? v : null;

    /// <summary>
    /// Tên / email / SĐT: ưu tiên thông tin người liên hệ, không có thì lấy của công ty.
    /// SĐT: ContactPhone → Tel → Phone (dữ liệu cũ hay ghi lẫn tên vào cột Phone, Tel thường chỉ có số).
    /// </summary>
    internal static CustomerSummary ToSummary(LegacyCustomer c) => new(
        c.Id,
        Clean(c.Code) ?? c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Clean(c.CompanyName) ?? Clean(c.ContactName) ?? "",
        Clean(c.ContactName),
        Clean(c.ContactEmail) ?? Clean(c.Email),
        Clean(c.ContactPhone) ?? Clean(c.Tel) ?? Clean(c.Phone),
        Clean(c.Address1),
        Clean(c.TaxCode));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
