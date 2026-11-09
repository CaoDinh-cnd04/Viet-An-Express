using MediatR;

namespace VietAnExpress.Shipments.Services;

/// <summary>Danh mục hãng / dịch vụ → hub cho ô Dịch vụ / Hub ở form tạo đơn và trang import (<see cref="IServiceHubCatalog"/>).</summary>
internal sealed record GetServiceHubsQuery : IRequest<IReadOnlyList<ServiceCarrierDto>>;

internal sealed class GetServiceHubsHandler(IServiceHubCatalog catalog) : IRequestHandler<GetServiceHubsQuery, IReadOnlyList<ServiceCarrierDto>>
{
    public Task<IReadOnlyList<ServiceCarrierDto>> Handle(GetServiceHubsQuery request, CancellationToken ct) => catalog.GetAsync(ct);
}
