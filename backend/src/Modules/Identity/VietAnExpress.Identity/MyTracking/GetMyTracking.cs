using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.MyTracking;

internal sealed record GetMyTrackingQuery : IRequest<Result<MyTrackingDto>>;

internal sealed class GetMyTrackingHandler(IdentityDbContext db, ICustomersApi customers, ICurrentUser user, MyTrackingImageStore images)
    : IRequestHandler<GetMyTrackingQuery, Result<MyTrackingDto>>
{
    public async Task<Result<MyTrackingDto>> Handle(GetMyTrackingQuery q, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        if (!await db.IsMyTrackingAllowedAsync(customerId, ct)) return MyTrackingErrors.MyTrackingNotAllowed;
        var page = await db.MyTrackingPages.AsNoTracking().Include(p => p.Banners).FirstOrDefaultAsync(p => p.CustomerId == customerId, ct);
        var customer = await customers.GetByIdAsync(customerId, ct);
        // Chưa cấu hình: config null — frontend dùng cấu hình mặc định.
        return page is null
            ? new MyTrackingDto(null, MyTrackingMapper.CustomerCode(customerId, customer), customer?.TrackingName ?? "")
            : MyTrackingMapper.ToDto(page, customerId, customer, images);
    }
}
