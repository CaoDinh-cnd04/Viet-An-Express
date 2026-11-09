using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>Nhân viên sales / CS còn hoạt động — danh sách chọn ở nút "Gán nhân viên" (chỉ quản trị / CS gọi được).</summary>
internal sealed record ListSupportStaffQuery : IRequest<Result<IReadOnlyList<SupportStaffDto>>>;

internal sealed class ListSupportStaffHandler(ICustomersApi customers, ICurrentUser user)
    : IRequestHandler<ListSupportStaffQuery, Result<IReadOnlyList<SupportStaffDto>>>
{
    public async Task<Result<IReadOnlyList<SupportStaffDto>>> Handle(ListSupportStaffQuery q, CancellationToken ct)
    {
        if (user.VietAnUserId is not { } agentId || await customers.GetVietAnUserAsync(agentId, ct) is not { } agent) return VietAnStaffErrors.VietAnOnly;
        if (!agent.CanAssignManager) return VietAnStaffErrors.CannotAssignManager;
        var staff = await customers.GetSupportStaffAsync(ct);
        return staff.Select(s => new SupportStaffDto(s.Id, s.UserName, s.FullName)).ToList();
    }
}
