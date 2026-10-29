using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Shipments.OrderData;

namespace VietAnExpress.Shipments.Tracking;

/// <summary>
/// Tra cứu vận đơn công khai (trang ngoài, không đăng nhập) trong dbo.MaVanDon. Tối đa <see cref="MaxBills"/> mã mỗi lần.
/// <paramref name="CustomerCode"/> có giá trị (trang MyTracking /tracking/{mã khách}/...) = chỉ tìm đơn của khách đó;
/// mã khách không tồn tại → không tìm thấy mã nào.
/// </summary>
internal sealed record TrackShipmentsQuery(IReadOnlyList<string> Bills, string? CustomerCode = null) : IRequest<IReadOnlyList<PublicTrackResultDto>>
{
    public const int MaxBills = 10;

    /// <summary>Viết hoa, bỏ khoảng trắng, bỏ trùng, giữ thứ tự nhập.</summary>
    public IReadOnlyList<string> NormalizedBills =>
        Bills.Select(b => (b ?? string.Empty).Trim().ToUpperInvariant())
            .Where(b => b.Length > 0)
            .Distinct()
            .ToList();
}

internal sealed class TrackShipmentsHandler(ILegacyBillReader legacyBills, TimeProvider clock, IHubEzTracking hubEz, ICustomersApi customers)
    : IRequestHandler<TrackShipmentsQuery, IReadOnlyList<PublicTrackResultDto>>
{
    public async Task<IReadOnlyList<PublicTrackResultDto>> Handle(TrackShipmentsQuery q, CancellationToken ct)
    {
        var bills = q.NormalizedBills;
        long? owner = null;
        if (!string.IsNullOrWhiteSpace(q.CustomerCode))
        {
            owner = (await customers.GetByCodeAsync(q.CustomerCode, ct))?.Id;
            if (owner is null) return bills.Select(b => new PublicTrackResultDto(b, Found: false)).ToList();
        }
        var rows = await legacyBills.FindAsync(bills, ct, owner);
        var today = VietnamTime.ToVietnam(clock.GetUtcNow()).Date;
        var matched = bills.Select(bill => (Bill: bill, Row: rows.FirstOrDefault(r => LegacyTrackingMapper.Matches(r, bill)))).ToList();

        // Đơn đi HUB-EZ: lấy hành trình HUB-EZ 1 lần cho mọi mã hãng (HUB-EZ lỗi → chỉ hành trình cũ).
        var hawbs = matched.Where(m => m.Row is not null && hubEz.Handles(m.Row.ServiceName, m.Row.BillConnect))
            .Select(m => m.Row!.BillConnect!.Trim()).ToList();
        var tracks = hawbs.Count == 0 ? new Dictionary<string, HubEzTrack>() : await hubEz.TrackAsync(hawbs, ct);

        return matched.Select(m => m.Row is { } row
                ? LegacyTrackingMapper.ToResult(m.Bill, row, today, row.BillConnect is { } connect ? tracks.GetValueOrDefault(connect.Trim()) : null)
                : new PublicTrackResultDto(m.Bill, Found: false))
            .ToList();
    }
}
