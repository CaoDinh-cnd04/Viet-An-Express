using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.Profiles;
using VietAnExpress.Customers.VietAnUsers;

namespace VietAnExpress.Customers.Shared;

internal sealed class CustomersApi(CustomersDbContext db, ILogger<CustomersApi> logger) : ICustomersApi
{
    public async Task<CustomerSummary?> GetByIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        return c is null ? null : ToSummary(c);
    }

    public async Task<CustomerSummary?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var value = code.Trim();
        if (value.Length == 0) return null;
        // Cột CustomerCode theo collation mặc định (không phân biệt hoa thường).
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Code == value, cancellationToken);
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

    public async Task<IReadOnlyList<long>> GetCustomerIdsOfManagerAsync(int managerId, CancellationToken cancellationToken = default) =>
        managerId <= 0 ? [] : await db.Customers.AsNoTracking().Where(c => c.AccountManagerId == managerId).Select(c => c.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VietAnUserLogin>> FindVietAnUsersAsync(string userName, CancellationToken cancellationToken = default)
    {
        var name = userName.Trim();
        if (name.Length == 0) return [];
        var rows = await db.VietAnUsers.AsNoTracking().Where(u => u.UserName == name).ToListAsync(cancellationToken);
        var logins = rows.Select(ToLogin).OfType<VietAnUserLogin>().ToList();
        // Có dòng USERS mà không đăng nhập được → ghi lý do (không ghi mật khẩu) để tra trên máy chủ.
        if (logins.Count == 0)
            logger.LogWarning("USERS '{UserName}': {Rows}", name, rows.Count == 0 ? "không có dòng nào" : string.Join("; ", rows.Select(u =>
                $"ID {u.Id} DELETED={u.Deleted} STATUS='{u.Status}' USER_PASSWORD {(string.IsNullOrEmpty(u.Password) ? "trống" : "có")}")));
        return logins;
    }

    public async Task<VietAnUserLogin?> GetVietAnUserAsync(int userId, CancellationToken cancellationToken = default) =>
        ToLogin(await db.VietAnUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken));

    public async Task<IReadOnlyList<CustomerSummary>> SearchCustomersAsync(string? query, int? managerId, int take,
        CancellationToken cancellationToken = default)
    {
        var customers = db.Customers.AsNoTracking();
        if (managerId is { } id) customers = customers.Where(c => c.AccountManagerId == id);
        if (Clean(query) is { } q)
            customers = customers.Where(c => c.Code!.Contains(q) || c.CompanyName!.Contains(q) || c.InvoiceName!.Contains(q)
                || c.ContactName!.Contains(q) || c.Phone!.Contains(q) || c.Tel!.Contains(q) || c.LoginUserName!.Contains(q));
        var rows = await customers.OrderBy(c => c.Code).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    /// <summary>
    /// Nhân viên đăng nhập được khi chưa xóa, STATUS = ACTIVE và có mật khẩu. Hàm thuần — có test.
    /// </summary>
    internal static VietAnUserLogin? ToLogin(VietAnUser? u)
    {
        if (u is null || ToAccountManager(u) is not { Active: true } manager || string.IsNullOrEmpty(u.Password)) return null;
        return new VietAnUserLogin(u.Id, manager.UserName, manager.FullName, u.IsAdmin == true || u.ViewCustomerAll == 1, u.Password, manager.Email);
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
        Clean(c.TaxCode),
        Clean(c.InvoiceName),
        Clean(c.LoginUserName));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
