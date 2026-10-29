using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal sealed record GetProductLibraryQuery : IRequest<IReadOnlyList<SavedProductDto>>
{
    public const int MaxProducts = 300;

    /// <summary>Số dòng hàng gần nhất được xét — đủ cho khách gửi thường xuyên, không quét cả bảng.</summary>
    public const int ScanLines = 3000;
}

internal sealed class GetProductLibraryHandler(ShipmentsDbContext db, OrderAccess access, ICurrentUser user)
    : IRequestHandler<GetProductLibraryQuery, IReadOnlyList<SavedProductDto>>
{
    public async Task<IReadOnlyList<SavedProductDto>> Handle(GetProductLibraryQuery q, CancellationToken ct)
    {
        var orders = access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct));
        var lines = await db.LegacyInvoiceLines.AsNoTracking()
            .Where(l => l.OrderId != null && orders.Any(o => o.Id == l.OrderId))
            .OrderByDescending(l => l.Id)
            .Take(GetProductLibraryQuery.ScanLines)
            .ToListAsync(ct);
        var marks = user.CustomerId is { } customerId
            ? await db.CatalogMarks.AsNoTracking()
                .Where(m => m.CustomerId == customerId && m.Kind == CatalogMark.Product)
                .ToDictionaryAsync(m => m.Key, m => new ProductMark(m.IsFavorite, m.IsDeleted), ct)
            : null;
        return ProductLibrary.Build(lines, GetProductLibraryQuery.MaxProducts, marks);
    }
}
