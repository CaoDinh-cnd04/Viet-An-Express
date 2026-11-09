using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.VietAnStaff;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

// Nhân viên Việt An đăng nhập portal: trang Hỗ trợ & góp ý thành trang quản lý — như tab hỗ trợ của /admin
// nhưng chỉ yêu cầu của khách trong phạm vi (VietAnScope).

internal sealed record GetManagedSupportRequestsQuery : IRequest<Result<IReadOnlyList<AdminSupportRequestDto>>>;

internal sealed class GetManagedSupportRequestsHandler(ICustomersApi customers, ICurrentUser user, ISender sender)
    : IRequestHandler<GetManagedSupportRequestsQuery, Result<IReadOnlyList<AdminSupportRequestDto>>>
{
    public async Task<Result<IReadOnlyList<AdminSupportRequestDto>>> Handle(GetManagedSupportRequestsQuery q, CancellationToken ct)
    {
        if (user.VietAnUserId is not { } agentId || await customers.GetVietAnUserAsync(agentId, ct) is not { } agent)
            return VietAnStaffErrors.VietAnOnly;
        return Result.Success(await sender.Send(new GetAdminSupportRequestsQuery(VietAnScope.ManagerFilter(agent)), ct));
    }
}

/// <summary>Trả lời / đổi trạng thái như /admin, sau khi kiểm tra yêu cầu thuộc khách trong phạm vi (ngoài phạm vi → không tìm thấy).</summary>
internal sealed record ReplyManagedSupportRequestCommand(string Id, string? Reply, string? Status) : IRequest<Result<AdminSupportRequestDto>>;

internal sealed class ReplyManagedSupportRequestHandler(IdentityDbContext db, ICustomersApi customers, ICurrentUser user, ISender sender)
    : IRequestHandler<ReplyManagedSupportRequestCommand, Result<AdminSupportRequestDto>>
{
    public async Task<Result<AdminSupportRequestDto>> Handle(ReplyManagedSupportRequestCommand c, CancellationToken ct)
    {
        if (user.VietAnUserId is not { } agentId || await customers.GetVietAnUserAsync(agentId, ct) is not { } agent)
            return VietAnStaffErrors.VietAnOnly;
        if (SupportRequest.ParseCode(c.Id) is not { } id) return SupportRequestErrors.NotFound;
        var customerId = await db.SupportRequests.AsNoTracking().Where(t => t.Id == id).Select(t => (long?)t.CustomerId).FirstOrDefaultAsync(ct);
        if (customerId is null) return SupportRequestErrors.NotFound;
        if (!agent.ViewAllCustomers && !await customers.IsCustomerOfStaffAsync(customerId.Value, agent.Id, ct))
            return SupportRequestErrors.NotFound;
        return await sender.Send(new ReplySupportRequestCommand(c.Id, c.Reply, c.Status), ct);
    }
}
