using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

// Trang quản trị Việt An: xem yêu cầu, đổi trạng thái / ghi chú cho khách.

/// <param name="ManagerId">Chỉ khách do nhân viên này phụ trách (USERS.ID) — cho CMS khi nhân viên thường đăng nhập; null = mọi khách.</param>
internal sealed record GetAdminSupportRequestsQuery(int? ManagerId = null) : IRequest<IReadOnlyList<AdminSupportRequestDto>>;

internal sealed class GetAdminSupportRequestsHandler(IdentityDbContext db, ICustomersApi customers)
    : IRequestHandler<GetAdminSupportRequestsQuery, IReadOnlyList<AdminSupportRequestDto>>
{
    private const int ListLimit = 300;

    private static AdminSupportRequestDto WithManager(AdminSupportRequestDto dto, AccountManager? manager) =>
        manager is null ? dto : dto with { ManagerId = manager.Id, ManagerName = manager.FullName };

    public async Task<IReadOnlyList<AdminSupportRequestDto>> Handle(GetAdminSupportRequestsQuery q, CancellationToken ct)
    {
        var tickets = db.SupportRequests.AsNoTracking();
        if (q.ManagerId is { } managerId)
        {
            var mine = (await customers.GetCustomerIdsOfManagerAsync(managerId, ct)).ToList();
            tickets = tickets.Where(t => mine.Contains(t.CustomerId));
        }
        var rows = await tickets
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
            .Take(ListLimit)
            .ToListAsync(ct);
        var ids = rows.Select(t => t.CustomerId).Distinct().ToList();
        var found = (await customers.GetByIdsAsync(ids, ct)).ToDictionary(c => c.Id);
        var managers = await customers.GetAccountManagersAsync(ids, ct);
        return rows.Select(t => WithManager(SupportRequestRules.ToAdminDto(t, found.GetValueOrDefault(t.CustomerId)?.Code), managers.GetValueOrDefault(t.CustomerId))).ToList();
    }
}
