using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Contracts;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Khách nhân viên Việt An được làm thay (theo <see cref="VietAnScope"/>), tìm theo mã / tên / SĐT / tên đăng nhập — tối đa 50 dòng,
/// kèm cờ khách mới + nhân viên phụ trách. Thứ tự: <see cref="ActableCustomerRanking"/> (khách mới, khách hay / mới tạo đơn lên đầu).
/// </summary>
internal sealed record ListActableCustomersQuery(string? Query) : IRequest<Result<IReadOnlyList<ActableCustomerDto>>>;

internal sealed class ListActableCustomersHandler(ICustomersApi customers, IShipmentsApi shipments, ICurrentUser user)
    : IRequestHandler<ListActableCustomersQuery, Result<IReadOnlyList<ActableCustomerDto>>>
{
    private const int ListLimit = 50;
    /// <summary>Có từ khóa tìm: lấy rộng hơn rồi xếp theo đơn, để khách hay gửi không bị cắt mất vì đứng sau theo mã.</summary>
    private const int SearchPool = 200;

    public async Task<Result<IReadOnlyList<ActableCustomerDto>>> Handle(ListActableCustomersQuery q, CancellationToken ct)
    {
        if (user.VietAnUserId is not { } agentId) return VietAnStaffErrors.VietAnOnly;
        if (await customers.GetVietAnUserAsync(agentId, ct) is not { } agent) return VietAnStaffErrors.VietAnOnly;
        var managerFilter = VietAnScope.ManagerFilter(agent);

        List<CustomerSummary> candidates;
        IReadOnlyList<CustomerOrderActivity> activity;
        if (string.IsNullOrWhiteSpace(q.Query))
        {
            // Không tìm: khách có đơn gần đây trong phạm vi + danh sách thường (khách mới đứng đầu) để bù cho đủ.
            IReadOnlyCollection<long>? scope = managerFilter is { } id ? await customers.GetCustomerIdsOfManagerAsync(id, ct) : null;
            activity = await shipments.GetCustomerActivityAsync(scope, ListLimit, ct);
            candidates = [.. await customers.GetByIdsAsync([.. activity.Select(a => a.CustomerId)], ct),
                .. await customers.SearchCustomersAsync(null, managerFilter, ListLimit, ct)];
        }
        else
        {
            candidates = [.. await customers.SearchCustomersAsync(q.Query, managerFilter, SearchPool, ct)];
            activity = await shipments.GetCustomerActivityAsync([.. candidates.Select(c => c.Id)], SearchPool, ct);
        }

        var rows = ActableCustomerRanking.Order(candidates, activity, ListLimit);
        // Người phụ trách chỉ Administrator / CS thấy (08/11/2026) — sale không nhận dữ liệu này.
        var managers = agent.CanAssignManager
            ? await customers.GetAccountManagersAsync([.. rows.Select(c => c.Id)], ct)
            : new Dictionary<long, AccountManager>();
        return rows.Select(c =>
        {
            // Khách mới chưa gán: Staff_ID chỉ là người tạo (tạm) — hiện "Chưa gán".
            // Cột "Phụ trách" hiện USER_NAME (tên đăng nhập CMS, không trùng) — FIRST_NAME + LAST_NAME của CMS hay lặp tên (06/11/2026).
            var m = c.ManagerPending ? null : managers.GetValueOrDefault(c.Id);
            return new ActableCustomerDto(c.Id, c.Code, c.CompanyName, c.Phone, c.Address, c.LoginUserName, c.IsNew, m?.Id, m?.UserName);
        }).ToList();
    }
}
