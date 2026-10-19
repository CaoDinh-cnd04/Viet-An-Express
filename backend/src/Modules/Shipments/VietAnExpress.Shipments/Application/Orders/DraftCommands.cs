using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Application.Dtos;
using VietAnExpress.Shipments.Domain;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Geo;
using VietAnExpress.Shipments.Infrastructure.Legacy;

namespace VietAnExpress.Shipments.Application.Orders;

/// <summary>Body POST / PUT /drafts — khớp NewDraft của frontend (web/src/features/drafts/api.ts).</summary>
internal sealed record DraftInput(
    string Stt,
    string? Cnee,
    string? Ct,
    string? Service,
    string? Branch,
    string? Ref,
    string? Pcs,
    string? Content,
    JsonElement? Payload)
{
    public const string StatusDraft = "draft";
    public const string StatusReady = "ready";

    public string PayloadJson
    {
        get
        {
            if (Payload is not { ValueKind: JsonValueKind.Object } p) return "{}";
            var node = JsonNode.Parse(p.GetRawText())!;
            if (node["receiver"] is JsonObject receiver)
            {
                var country = receiver["country"] is JsonValue cv && cv.TryGetValue<string>(out var c) ? c : "";
                var code = receiver["countryCode"] is JsonValue cc && cc.TryGetValue<string>(out var iso) ? iso : "";
                if (!EuCountries.IsEuCountry(EuCountries.ReceiverCode(country, code)))
                {
                    receiver["iossNo"] = "";
                    receiver["eoriNo"] = "";
                }
            }
            return node.ToJsonString();
        }
    }

    /// <summary>Form tạo đơn đã gửi; không đọc được → null.</summary>
    public OrderPayload? ReadPayload()
    {
        try { return JsonSerializer.Deserialize<OrderPayload>(PayloadJson, DraftOrders.Json); }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// Đơn chưa in nằm luôn trong dbo.MaVanDon với Status = 0, chưa có số bill (OrderNumber NULL); VA_Bill tạm giữ tiền tố chi nhánh.
/// Kiện / dòng hàng ghi vào MaVanDon_PCS_DIM / MaVanDon_ChiTietHang như đơn đã in. Chỉ lưu thông tin có cột trong các bảng này.
/// </summary>
internal static class DraftOrders
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Đơn chưa in (bỏ bộ lọc ẩn nháp của LegacyOrder).</summary>
    public static IQueryable<LegacyOrder> Drafts(this ShipmentsDbContext db) =>
        db.LegacyOrders.IgnoreQueryFilters().Where(o => o.Status == LegacyOrder.DraftStatus);

    /// <summary>Dựng dòng MaVanDon chưa in từ form. Hàm thuần — có test.</summary>
    public static LegacyOrder Build(OrderPayload p, LegacyCustomerRef customer, DateTime todayVn, int? consigneeCountryId)
    {
        var order = LegacyOrderFactory.FromPayload(p, customer, orderNumber: 0, todayVn, consigneeCountryId);
        order.OrderNumber = null;
        order.VaBill = VaBillCode.DraftPrefix(p.Shipper.Branch);
        order.Status = LegacyOrder.DraftStatus;
        // Form chưa khai hàng: không ghi chữ mặc định "Goods" / "Documents" để mở lại nháp vẫn trống.
        var isDoc = string.Equals(p.Shipment.Type, "DOC", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(isDoc ? p.Goods.DocContent : LegacyOrderFactory.GoodsName(p))) order.GoodsName = null;
        return order;
    }

    /// <summary>
    /// "Chờ in" (ready) khi đủ thông tin để cấp bill: người gửi, người nhận (tên, nước, địa chỉ), dịch vụ, cân &gt; 0 — còn lại là "nháp".
    /// Hàm thuần — có test.
    /// </summary>
    public static bool IsReady(LegacyOrder o) =>
        !string.IsNullOrWhiteSpace(o.SenderName)
        && !string.IsNullOrWhiteSpace(o.ConsigneeName)
        && !string.IsNullOrWhiteSpace(o.ConsigneeCountry)
        && !string.IsNullOrWhiteSpace(o.ConsigneeAddress1)
        && !string.IsNullOrWhiteSpace(o.ServiceName)
        && o.WeightKg > 0;

    public static DraftDto ToDto(LegacyOrder o, IReadOnlyList<LegacyPackageLine> packages, IReadOnlyList<LegacyInvoiceLine> items)
    {
        var weight = (o.WeightKg ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        return new DraftDto(
            o.Id.ToString(CultureInfo.InvariantCulture),
            IsReady(o) ? DraftInput.StatusReady : DraftInput.StatusDraft,
            o.ConsigneeName ?? "", o.ConsigneeCountry ?? "", o.ServiceName?.Replace("|", " - ") ?? "",
            VaBillCode.BranchOfPrefix(o.VaBill) ?? "", o.CustomerBill ?? "",
            $"{o.Pieces ?? 0} kiện · {weight} kg", o.GoodsName ?? "",
            (o.ModifyDate ?? o.CreateDate)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
            JsonSerializer.SerializeToElement(OrderCopy.FromLegacy(o, packages, items), Json));
    }

    /// <summary>Kiện + dòng hàng của các đơn (theo MaVanDon.ID).</summary>
    public static async Task<(ILookup<int, LegacyPackageLine> Packages, ILookup<int, LegacyInvoiceLine> Items)> LinesAsync(
        ShipmentsDbContext db, IReadOnlyCollection<long> orderIds, CancellationToken ct)
    {
        var ids = orderIds.Where(id => id is > 0 and <= int.MaxValue).Select(id => (int)id).ToList();
        var packages = await db.LegacyPackageLines.AsNoTracking().Where(p => ids.Contains(p.OrderId)).OrderBy(p => p.Id).ToListAsync(ct);
        var items = await db.LegacyInvoiceLines.AsNoTracking()
            .Where(i => i.OrderId != null && ids.Contains(i.OrderId.Value)).OrderBy(i => i.Id).ToListAsync(ct);
        return (packages.ToLookup(p => p.OrderId), items.ToLookup(i => i.OrderId!.Value));
    }

    /// <summary>
    /// Khóa dòng MaVanDon chưa in tới hết transaction hiện tại (UPDLOCK: portal và hệ thống cũ cùng ghi dòng này phải chờ).
    /// false = đơn không còn ở trạng thái chưa in (vừa được in / xoá ở nơi khác).
    /// </summary>
    public static async Task<bool> LockAsync(ShipmentsDbContext db, long id, CancellationToken ct) =>
        await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM dbo.MaVanDon WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE ID = {id} AND Status = {LegacyOrder.DraftStatus}")
            .SingleAsync(ct) > 0;

    /// <summary>Xoá kiện / dòng hàng cũ của 1 đơn (sửa nháp ghi lại từ form, xoá nháp).</summary>
    public static async Task RemoveLinesAsync(ShipmentsDbContext db, long orderId, CancellationToken ct)
    {
        var id = checked((int)orderId);
        db.LegacyPackageLines.RemoveRange(await db.LegacyPackageLines.Where(p => p.OrderId == id).ToListAsync(ct));
        db.LegacyInvoiceLines.RemoveRange(await db.LegacyInvoiceLines.Where(i => i.OrderId == id).ToListAsync(ct));
    }
}

// ---------- Danh sách / lưu / sửa / xoá nháp ----------

internal sealed record GetDraftsQuery : IRequest<IReadOnlyList<DraftDto>>;

internal sealed record SaveDraftCommand(long? Id, DraftInput Draft) : IRequest<Result<DraftDto>>;

internal sealed record DeleteDraftCommand(long Id) : IRequest<Result>;

internal sealed class DraftHandlers(ShipmentsDbContext db, OrderAccess access, IGeoLookup geo, TimeProvider clock) :
    IRequestHandler<GetDraftsQuery, IReadOnlyList<DraftDto>>,
    IRequestHandler<SaveDraftCommand, Result<DraftDto>>,
    IRequestHandler<DeleteDraftCommand, Result>
{
    private const int ListLimit = 500;

    public async Task<IReadOnlyList<DraftDto>> Handle(GetDraftsQuery q, CancellationToken ct)
    {
        var scope = await access.ScopeAsync(ct);
        var drafts = await access.Apply(db.Drafts().AsNoTracking(), scope)
            .OrderByDescending(o => o.Id)
            .Take(ListLimit)
            .ToListAsync(ct);
        var (packages, items) = await DraftOrders.LinesAsync(db, drafts.Select(d => d.Id).ToList(), ct);
        return drafts.Select(d => DraftOrders.ToDto(d, packages[(int)d.Id].ToList(), items[(int)d.Id].ToList())).ToList();
    }

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
                if (!await DraftOrders.LockAsync(db, existing.Id, ct))
                {
                    failure = OrderErrors.DraftChanged;
                    return;
                }
                fresh.Id = existing.Id;
                fresh.CreateDate = existing.CreateDate;
                // Sửa nháp không đổi người tạo (tài khoản chính sửa nháp của nhân viên thì nháp vẫn của nhân viên đó).
                fresh.CustomerStaffId = existing.CustomerStaffId;
                db.Entry(existing).CurrentValues.SetValues(fresh);
                await DraftOrders.RemoveLinesAsync(db, existing.Id, ct);
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

        var (packages, items) = await DraftOrders.LinesAsync(db, [saved.Id], ct);
        return DraftOrders.ToDto(saved, packages[(int)saved.Id].ToList(), items[(int)saved.Id].ToList());
    }

    public async Task<Result> Handle(DeleteDraftCommand cmd, CancellationToken ct)
    {
        var scope = await access.ScopeAsync(ct);
        if (!await access.Apply(db.Drafts(), scope).AnyAsync(o => o.Id == cmd.Id, ct)) return OrderErrors.DraftNotFound;

        // Đơn chưa in xoá hẳn khỏi dbo.MaVanDon cùng chi tiết (như hệ thống cũ cho xoá đơn chưa in).
        // Khóa dòng trước: đơn vừa được in ở nơi khác thì không xoá mất vận đơn đã cấp bill.
        var deleted = false;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            deleted = await DraftOrders.LockAsync(db, cmd.Id, ct);
            if (!deleted) return;
            await DraftOrders.RemoveLinesAsync(db, cmd.Id, ct);
            db.LegacyOrders.Remove(await db.Drafts().FirstAsync(o => o.Id == cmd.Id, ct));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return deleted ? Result.Success() : OrderErrors.DraftChanged;
    }
}

// ---------- In & cấp bill: cấp số cho chính dòng MaVanDon chưa in ----------

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
            if (!await DraftOrders.LockAsync(db, cmd.Id, ct))
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

            var (packages, items) = await DraftOrders.LinesAsync(db, [order.Id], ct);
            var payload = OrderCopy.FromLegacy(order, packages[(int)order.Id].ToList(), items[(int)order.Id].ToList());
            var taxValidation = new Validators.ReceiverTaxValidator().Validate(payload.Receiver);
            if (!taxValidation.IsValid)
            {
                failure = Error.Validation("RECEIVER_TAX_INVALID", taxValidation.Errors[0].ErrorMessage);
                return;
            }

            // VSVX (bảng tĩnh, có cache) ghi vào đơn như hệ thống cũ.
            var countryCode = RemoteAreaColumns.CountryCode(payload, countries);
            var remote = await remoteAreas.FindAsync(countryCode, payload.Receiver.Postal, payload.Receiver.City, ct);
            var branch = VaBillCode.BranchOfPrefix(order.VaBill);

            // Cấp số trong transaction: khóa dãy số tới khi commit, không trùng với hệ thống cũ.
            number = (await numbers.NextAsync(1, ct))[0];
            order.OrderNumber = number;
            order.VaBill = VaBillCode.Format(branch, number, countryCode);
            order.Status = LegacyOrder.IssuedStatus;
            order.CreateDate = today;
            order.ModifyDate = today;
            RemoteAreaColumns.Apply(order, remote);
            code = order.VaBill;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        if (failure is not null) return failure;
        var bill = code ?? number.ToString(CultureInfo.InvariantCulture);
        return new PrintDraftResponse($"Đã cấp mã vận đơn {bill}", bill);
    }
}
