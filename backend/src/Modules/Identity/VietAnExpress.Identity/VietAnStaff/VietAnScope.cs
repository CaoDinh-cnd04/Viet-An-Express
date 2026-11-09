using VietAnExpress.Customers.Contracts;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>Nhân viên Việt An đang đăng nhập + khách đang làm thay (null = chưa chọn).</summary>
internal sealed record VietAnContext(VietAnUserLogin Agent, CustomerSummary? Customer);

/// <summary>
/// Phạm vi khách của nhân viên Việt An theo vai trò (dbo.ACL_ROLES_USERS, 06/11/2026): Administrator làm thay mọi khách,
/// còn lại khách có <c>TCustomer.Staff_ID</c> = USERS.ID (sale) hoặc USERS.ID nằm trong <c>TCustomer.SubTer</c> (CS, như CMS).
/// </summary>
internal static class VietAnScope
{
    /// <summary>Lọc khách theo người phụ trách: null = mọi khách.</summary>
    public static int? ManagerFilter(VietAnUserLogin agent) => agent.ViewAllCustomers ? null : agent.Id;

    /// <summary>Khách trong phạm vi của nhân viên; ngoài phạm vi / không có → null.</summary>
    public static async Task<CustomerSummary?> CustomerInScopeAsync(this ICustomersApi customers, VietAnUserLogin agent, long customerId, CancellationToken ct)
    {
        if (customerId <= 0) return null;
        if (!agent.ViewAllCustomers && !await customers.IsCustomerOfStaffAsync(customerId, agent.Id, ct)) return null;
        return await customers.GetByIdAsync(customerId, ct);
    }

    /// <summary>
    /// Dựng lại phiên (làm mới token, GET /me): nhân viên phải còn hoạt động; khách đã chọn mà nay ngoài phạm vi (đổi người phụ trách)
    /// thì quay về "chưa chọn khách" thay vì đăng xuất.
    /// </summary>
    public static async Task<VietAnContext?> LoadAsync(ICustomersApi customers, int agentId, long customerId, CancellationToken ct)
    {
        if (await customers.GetVietAnUserAsync(agentId, ct) is not { } agent) return null;
        return new VietAnContext(agent, await customers.CustomerInScopeAsync(agent, customerId, ct));
    }
}
