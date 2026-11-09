using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.NewCustomers;
using VietAnExpress.Customers.Profiles;
using VietAnExpress.Customers.VietAnUsers;

namespace VietAnExpress.Customers.Shared;

internal sealed class CustomersApi(CustomersDbContext db, NewCustomerStore newCustomers, ILogger<CustomersApi> logger) : ICustomersApi
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
        managerId <= 0 ? [] : await db.Customers.AsNoTracking().Where(await ScopeOfAsync(managerId, cancellationToken)).Select(c => c.Id).ToListAsync(cancellationToken);

    /// <summary>Phạm vi khách theo vai trò của nhân viên: CS gồm cả SubTer, sale / vai trò khác chỉ Staff_ID.</summary>
    private async Task<System.Linq.Expressions.Expression<Func<LegacyCustomer, bool>>> ScopeOfAsync(int staffId, CancellationToken ct) =>
        CustomerScope.OfStaff(staffId, VietAnRoles.UsesSubTer((await RoleNamesAsync([staffId], ct)).GetValueOrDefault(staffId) ?? []));

    public async Task<bool> IsCustomerOfStaffAsync(long customerId, int staffId, CancellationToken cancellationToken = default) =>
        staffId > 0 && await db.Customers.AsNoTracking().Where(c => c.Id == customerId).AnyAsync(await ScopeOfAsync(staffId, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<VietAnUserLogin>> FindVietAnUsersAsync(string userName, CancellationToken cancellationToken = default)
    {
        var name = userName.Trim();
        if (name.Length == 0) return [];
        var rows = await db.VietAnUsers.AsNoTracking().Where(u => u.UserName == name).ToListAsync(cancellationToken);
        var roles = await RoleNamesAsync([.. rows.Select(u => u.Id)], cancellationToken);
        var logins = rows.Select(u => ToLogin(u, roles.GetValueOrDefault(u.Id) ?? [])).OfType<VietAnUserLogin>().ToList();
        // Có dòng USERS mà không đăng nhập được → ghi lý do (không ghi mật khẩu) để tra trên máy chủ.
        if (logins.Count == 0)
            logger.LogWarning("USERS '{UserName}': {Rows}", name, rows.Count == 0 ? "không có dòng nào" : string.Join("; ", rows.Select(u =>
                $"ID {u.Id} DELETED={u.Deleted} STATUS='{u.Status}' USER_PASSWORD {(string.IsNullOrEmpty(u.Password) ? "trống" : "có")}")));
        return logins;
    }

    public async Task<VietAnUserLogin?> GetVietAnUserAsync(int userId, CancellationToken cancellationToken = default) =>
        ToLogin(await db.VietAnUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken),
            (await RoleNamesAsync([userId], cancellationToken)).GetValueOrDefault(userId) ?? []);

    /// <summary>
    /// Tên vai trò của nhân viên (dbo.ACL_ROLES_USERS → ACL_ROLES, bỏ dòng DELETED); <paramref name="userIds"/> null = mọi nhân viên.
    /// Administrator / ALL xem mọi khách; Administrator / CS gán người phụ trách khách mới.
    /// Máy chưa nạp 2 bảng ACL (vd laptop dev) → coi như không ai có vai trò, ghi cảnh báo, đăng nhập vẫn chạy.
    /// </summary>
    private async Task<Dictionary<int, List<string?>>> RoleNamesAsync(IReadOnlyCollection<int>? userIds, CancellationToken ct)
    {
        if (userIds is { Count: 0 }) return [];
        try
        {
            var links = db.VietAnRoleUsers.AsNoTracking().Where(ru => ru.UserId != null && ru.Deleted != true);
            if (userIds is not null) links = links.Where(ru => userIds.Contains(ru.UserId!.Value));
            var rows = await links
                .Join(db.VietAnRoles.AsNoTracking().Where(r => r.Deleted != true), ru => ru.RoleId, r => (int?)r.Id, (ru, r) => new { ru.UserId, r.Name })
                .ToListAsync(ct);
            return rows.GroupBy(x => x.UserId!.Value).ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());
        }
        catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 208) // Invalid object name
        {
            logger.LogWarning("Chưa có bảng dbo.ACL_ROLES / ACL_ROLES_USERS — bỏ qua vai trò nhân viên");
            return [];
        }
    }

    public async Task<IReadOnlyDictionary<int, string>> GetVietAnUserNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, string>();
        foreach (var chunk in userIds.Where(id => id > 0).Distinct().Chunk(1000))
        {
            var rows = await db.VietAnUsers.AsNoTracking().Where(u => chunk.Contains(u.Id)).Select(u => new { u.Id, u.UserName }).ToListAsync(cancellationToken);
            foreach (var r in rows) if (!string.IsNullOrWhiteSpace(r.UserName)) result[r.Id] = r.UserName.Trim();
        }
        return result;
    }

    public async Task<IReadOnlyDictionary<long, string>> GetContactLoginNamesAsync(IReadOnlyCollection<long> contactIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<long, string>();
        foreach (var chunk in contactIds.Where(id => id > 0).Distinct().Chunk(1000))
        {
            var rows = await db.ContactLogins.AsNoTracking().Where(c => chunk.Contains(c.Id)).ToListAsync(cancellationToken);
            foreach (var r in rows) if (!string.IsNullOrWhiteSpace(r.UserName)) result[r.Id] = r.UserName.Trim();
        }
        return result;
    }

    public async Task<IReadOnlyList<CustomerSummary>> SearchCustomersAsync(string? query, int? managerId, int take,
        CancellationToken cancellationToken = default)
    {
        var customers = db.Customers.AsNoTracking();
        if (managerId is { } id) customers = customers.Where(await ScopeOfAsync(id, cancellationToken));
        // Gõ 1 phần là đủ (chứa), không phân biệt hoa / thường và dấu tiếng Việt: "saigon", "cong ty viet" đều ra.
        if (Clean(query) is { } q)
        {
            const string ci = "Latin1_General_100_CI_AI";
            customers = customers.Where(c => EF.Functions.Collate(c.Code!, ci).Contains(q) || EF.Functions.Collate(c.CompanyName!, ci).Contains(q)
                || EF.Functions.Collate(c.InvoiceName!, ci).Contains(q) || EF.Functions.Collate(c.ContactName!, ci).Contains(q)
                || EF.Functions.Collate(c.Phone!, ci).Contains(q) || EF.Functions.Collate(c.Tel!, ci).Contains(q)
                || EF.Functions.Collate(c.LoginUserName!, ci).Contains(q));
        }
        var rows = await customers.OrderByDescending(c => c.Source == NewCustomerRules.Marker).ThenBy(c => c.Code)
            .Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    /// <summary>
    /// Nhân viên đăng nhập được khi chưa xóa, STATUS = ACTIVE và có mật khẩu. Hàm thuần — có test.
    /// </summary>
    /// <param name="roleNames">Vai trò ở dbo.ACL_ROLES_USERS: Administrator / ALL → xem mọi khách; Administrator / CS → gán người phụ trách khách mới.</param>
    internal static VietAnUserLogin? ToLogin(VietAnUser? u, IReadOnlyCollection<string?>? roleNames = null)
    {
        if (u is null || ToAccountManager(u) is not { Active: true } manager || string.IsNullOrEmpty(u.Password)) return null;
        var roles = roleNames ?? [];
        // Phạm vi theo vai trò ở dbo.ACL_ROLES_USERS: Administrator / ALL = mọi khách; Sale / CS / khác = khách có
        // TCustomer.Staff_ID là mình. Không dùng USERS.IS_ADMIN / VIEW_CUSTOMER_ALL — dữ liệu cũ bật cho cả sale nên sale thấy hết khách.
        return new VietAnUserLogin(u.Id, manager.UserName, manager.FullName, VietAnRoles.SeesAllCustomers(roles),
            u.Password, manager.Email, NewCustomerRules.CanAssign(roles));
    }

    public Task<long> CreateNewCustomerAsync(int staffId, CancellationToken cancellationToken = default) =>
        newCustomers.CreateAsync(staffId, cancellationToken);

    public Task<bool> FillNewCustomerAsync(long customerId, NewCustomerInfo info, int modifiedBy, CancellationToken cancellationToken = default) =>
        newCustomers.FillAsync(customerId, info, modifiedBy, cancellationToken);

    public async Task<bool> AssignNewCustomerManagerAsync(long customerId, int managerId, int modifiedBy, CancellationToken cancellationToken = default)
    {
        if (!(await GetSupportStaffAsync(cancellationToken)).Any(s => s.Id == managerId)) return false;
        return await newCustomers.AssignManagerAsync(customerId, managerId, modifiedBy, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountManager>> GetSupportStaffAsync(CancellationToken cancellationToken = default)
    {
        var ids = (await RoleNamesAsync(null, cancellationToken)).Where(r => NewCustomerRules.IsSupportStaff(r.Value)).Select(r => r.Key).ToList();
        if (ids.Count == 0) return [];
        var users = await db.VietAnUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(cancellationToken);
        return users.Select(ToAccountManager).OfType<AccountManager>().Where(m => m.Active).OrderBy(m => m.FullName).ToList();
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
        Clean(c.LoginUserName),
        NewCustomerRules.IsNew(c.Source),
        NewCustomerRules.ManagerPending(c.Source, c.AccountManagerId, c.CreatedBy));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
