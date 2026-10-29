using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>Trang theo mã khách (TCustomer.CustomerCode) — link /tracking/{mã khách}/....</summary>
internal sealed record GetCustomerMyTrackingQuery(string CustomerCode) : IRequest<Result<PublicMyTrackingDto>>;

internal sealed class GetCustomerMyTrackingHandler(IdentityDbContext db, ICustomersApi customers, MyTrackingImageStore images)
    : IRequestHandler<GetCustomerMyTrackingQuery, Result<PublicMyTrackingDto>>
{
    public async Task<Result<PublicMyTrackingDto>> Handle(GetCustomerMyTrackingQuery q, CancellationToken ct)
    {
        if (await customers.GetByCodeAsync(q.CustomerCode, ct) is not { } customer) return MyTrackingErrors.MyTrackingNotFound;
        var page = await db.MyTrackingPages.AsNoTracking().Include(p => p.Banners)
            .FirstOrDefaultAsync(p => p.CustomerId == customer.Id, ct);
        return page is null
            ? MyTrackingErrors.MyTrackingNotFound
            : new PublicMyTrackingDto(new RawJson(images.ForDisplay(MyTrackingJson.WithCompanyName(page.ConfigJson, customer.TrackingName))), customer.Code);
    }
}
