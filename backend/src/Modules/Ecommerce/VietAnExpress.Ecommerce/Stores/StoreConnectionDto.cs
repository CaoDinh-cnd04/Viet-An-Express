using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Stores;

/// <summary>Khớp <c>StoreConnection</c> của frontend (API_CONTRACT.md §5.1). Không bao giờ chứa token.</summary>
internal sealed record StoreConnectionDto(
    string Id, string Platform, string ShopName, string? ShopDomain, string? Region, string Status,
    DateTimeOffset ConnectedAt, DateTimeOffset? LastSyncAt, string? LastError);
