using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Drafts;

/// <summary>Tạo (<see cref="Id"/> null) hoặc sửa đơn nháp từ form tạo đơn.</summary>
internal sealed record SaveDraftCommand(long? Id, DraftInput Draft) : IRequest<Result<DraftDto>>;

internal sealed class SaveDraftHandler(ShipmentsDbContext db, OrderAccess access, IGeoLookup geo, TimeProvider clock)
    : IRequestHandler<SaveDraftCommand, Result<DraftDto>>
{
    public async Task<Result<DraftDto>> Handle(SaveDraftCommand cmd, CancellationToken ct)
    {
        var payload = cmd.Draft.ReadPayload();
        if (payload is null) return OrderErrors.InvalidPayload;
        var writer = await access.WriterAsync(ct);
        if (writer.IsFailure) return writer.Error;

        LegacyOrder? existing = null;
        if (cmd.Id is { } id)
        {
            var scope = await access.ScopeAsync(ct);
            existing = await access.Apply(db.Drafts(), scope).FirstOrDefaultAsync(o => o.Id == id, ct);
            if (existing is null) return OrderErrors.DraftNotFound;
        }

        var today = VietnamTime.ToVietnam(clock.GetUtcNow()).Date;
        var countries = await LegacyCountryLookup.LoadAsync(db, ct);
        var countryCode = RemoteAreaColumns.CountryCode(payload, await geo.GetCountriesAsync(ct));
        var fresh = DraftOrders.Build(payload, writer.Value, today, LegacyCountryLookup.FindId(countries, countryCode, payload.Receiver.Country));

        var saved = existing ?? fresh;
        Error? failure = null;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            failure = null;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (existing is not null)
            {
                // Người khác vừa in / xoá nháp này → không ghi đè đơn đã cấp bill.
                if (!await DraftStore.LockAsync(db, existing.Id, ct))
                {
                    failure = OrderErrors.DraftChanged;
                    return;
                }
                fresh.Id = existing.Id;
                fresh.CreateDate = existing.CreateDate;
                // Sửa nháp không đổi người tạo (tài khoản chính sửa nháp của nhân viên thì nháp vẫn của nhân viên đó).
                fresh.CustomerStaffId = existing.CustomerStaffId;
                db.Entry(existing).CurrentValues.SetValues(fresh);
                await DraftStore.RemoveLinesAsync(db, existing.Id, ct);
                await db.SaveChangesAsync(ct);
                await LegacyOrderLinesWriter.AddAsync(db, [(existing, payload)], ct);
            }
            else
            {
                // Nháp của tài khoản con: ghi người tạo ngay để tài khoản con chỉ thấy nháp của mình.
                access.StampCreator(fresh);
                db.LegacyOrders.Add(fresh);
                await db.SaveChangesAsync(ct);
                await LegacyOrderLinesWriter.AddAsync(db, [(fresh, payload)], ct);
            }
            await tx.CommitAsync(ct);
        });
        if (failure is not null) return failure;

        var (packages, items) = await DraftStore.LinesAsync(db, [saved.Id], ct);
        return DraftOrders.ToDto(saved, packages[(int)saved.Id].ToList(), items[(int)saved.Id].ToList());
    }
}
