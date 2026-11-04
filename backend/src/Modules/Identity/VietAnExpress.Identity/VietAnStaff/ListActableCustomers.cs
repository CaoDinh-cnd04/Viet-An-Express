using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>Khách nhân viên Việt An được làm thay (theo <see cref="VietAnScope"/>), tìm theo mã / tên / SĐT / tên đăng nhập — tối đa 50 dòng.</summary>
internal sealed record ListActableCustomersQuery(string? Query) : IRequest<Result<IReadOnlyList<ActableCustomerDto>>>;

internal sealed class ListActableCustomersHandler(ICustomersApi customers, ICurrentUser user)
    : IRequestHandler<ListActableCustomersQuery, Result<IReadOnlyList<ActableCustomerDto>>>
{
    private const int ListLimit = 50;

    public async Task<Result<IReadOnlyList<ActableCustomerDto>>> Handle(ListActableCustomersQuery q, CancellationToken ct)
    {
        if (user.VietAnUserId is not { } agentId) return VietAnStaffErrors.VietAnOnly;
        if (await customers.GetVietAnUserAsync(agentId, ct) is not { } agent) return VietAnStaffErrors.VietAnOnly;
        var rows = await customers.SearchCustomersAsync(q.Query, VietAnScope.ManagerFilter(agent), ListLimit, ct);
        return rows.Select(c => new ActableCustomerDto(c.Id, c.Code, c.CompanyName, c.Phone, c.Address, c.LoginUserName)).ToList();
    }
}
