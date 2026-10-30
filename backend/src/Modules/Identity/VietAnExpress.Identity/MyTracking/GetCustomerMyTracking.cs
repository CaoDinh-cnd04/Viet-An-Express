using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>
/// Trang theo mã khách (TCustomer.CustomerCode) — link /tracking/{mã khách}/.... Như trang mytracking.top cũ: khách nào cũng có trang
/// (thương hiệu từ hồ sơ khách), phần ưu đãi / ảnh / mạng xã hội có khi khách đã cấu hình.
/// </summary>
internal sealed record GetCustomerMyTrackingQuery(string CustomerCode) : IRequest<Result<PublicMyTrackingDto>>;

internal sealed class GetCustomerMyTrackingHandler(IdentityDbContext db, ICustomersApi customers, MyTrackingImageStore images)
    : IRequestHandler<GetCustomerMyTrackingQuery, Result<PublicMyTrackingDto>>
{
    public async Task<Result<PublicMyTrackingDto>> Handle(GetCustomerMyTrackingQuery q, CancellationToken ct)
    {
        if (await customers.GetByCodeAsync(q.CustomerCode, ct) is not { } customer) return MyTrackingErrors.MyTrackingNotFound;
        var page = await db.MyTrackingPages.AsNoTracking().Include(p => p.Banners)
            .FirstOrDefaultAsync(p => p.CustomerId == customer.Id, ct);
        var brand = await db.MyTrackingBrandAsync(customer.Id, ct);
        return new PublicMyTrackingDto(new RawJson(images.ForDisplay(MyTrackingMapper.ConfigJson(page, brand, customer.TrackingName))), customer.Code);
    }
}
