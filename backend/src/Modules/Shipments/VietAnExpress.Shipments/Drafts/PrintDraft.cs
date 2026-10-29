using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Drafts;

/// <summary>In &amp; cấp bill: cấp số vận đơn cho chính dòng MaVanDon chưa in (Status 0 → 1), trong 1 transaction có khóa dòng.</summary>
internal sealed record PrintDraftCommand(long Id) : IRequest<Result<PrintDraftResponse>>;

internal sealed class PrintDraftHandler(
    ShipmentsDbContext db, OrderAccess access, ILegacyOrderNumberAllocator numbers, IRemoteAreaLookup remoteAreas,
    IGeoLookup geo, TimeProvider clock)
    : IRequestHandler<PrintDraftCommand, Result<PrintDraftResponse>>
{
    public async Task<Result<PrintDraftResponse>> Handle(PrintDraftCommand cmd, CancellationToken ct)
    {
        var scope = await access.ScopeAsync(ct);
        if (!await access.Apply(db.Drafts(), scope).AnyAsync(o => o.Id == cmd.Id, ct)) return OrderErrors.DraftNotFound;
        var countries = await geo.GetCountriesAsync(ct);
        var today = VietnamTime.ToVietnam(clock.GetUtcNow()).Date;

        long number = 0;
        string? code = null;
        Error? failure = null;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            failure = null;
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Khóa đơn rồi mới đọc / kiểm tra: 2 người bấm In cùng lúc → người sau chờ, thấy đơn đã in và báo lỗi,
            // không cấp thêm số mới đè lên bill người trước đã nhận; ai đang sửa / xoá nháp cũng phải chờ.
            if (!await DraftStore.LockAsync(db, cmd.Id, ct))
            {
                failure = OrderErrors.DraftChanged;
                return;
            }
            var order = await db.Drafts().FirstAsync(o => o.Id == cmd.Id, ct);
            if (!DraftOrders.IsReady(order))
            {
                failure = OrderErrors.DraftNotReady;
                return;
            }

            var (packages, items) = await DraftStore.LinesAsync(db, [order.Id], ct);
            var payload = OrderCopy.FromLegacy(order, packages[(int)order.Id].ToList(), items[(int)order.Id].ToList());
            var taxValidation = new ReceiverTaxValidator().Validate(payload.Receiver);
            if (!taxValidation.IsValid)
            {
                failure = Error.Validation("RECEIVER_TAX_INVALID", taxValidation.Errors[0].ErrorMessage);
                return;
            }

            // VSVX (bảng tĩnh, có cache) ghi vào đơn như hệ thống cũ.
            var countryCode = RemoteAreaColumns.CountryCode(payload, countries);
            var remote = await remoteAreas.FindAsync(countryCode, payload.Receiver.Postal, payload.Receiver.City, ct);

            // Cấp số trong transaction: khóa dãy số tới khi commit (không trùng giữa các lần in trên portal).
            number = (await numbers.NextAsync(1, ct))[0];
            order.OrderNumber = number;
            order.VaBill = null; // như hệ thống cũ; mã bill hiển thị = OrderNumber (nháp cũ còn tiền tố chi nhánh thì xóa luôn)
            order.Status = LegacyOrder.IssuedStatus;
            order.CreateDate = today;
            order.ModifyDate = today;
            RemoteAreaColumns.Apply(order, remote);
            code = LegacyOrderView.BillOf(order);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        if (failure is not null) return failure;
        // Hệ thống cũ chèn trùng số ngay sau khi commit → cấp số khác trước khi khách nhận bill.
        if ((await numbers.ReissueTakenAsync([cmd.Id], ct)).TryGetValue(cmd.Id, out var reissued)) code = reissued;
        var bill = code ?? number.ToString(CultureInfo.InvariantCulture);
        return new PrintDraftResponse($"Đã cấp mã vận đơn {bill}", bill);
    }
}
