using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Import;

/// <summary>
/// Kiểm tra file (<paramref name="Commit"/> = false) hoặc kiểm tra rồi tạo đơn cho các dòng hợp lệ (true).
/// Tạo đơn đọc lại file và kiểm tra lại từ đầu — không tin kết quả xem trước từ trình duyệt.
/// </summary>
internal sealed record ImportOrdersCommand(
    Stream File, string FileName, long Length, string? Service, string? Hub, string? Branch, bool Commit)
    : IRequest<Result<ImportResultDto>>
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const int MaxRows = 100;
}

internal sealed class ImportOrdersHandler(
    ShipmentsDbContext db, OrderAccess access, ILegacyOrderNumberAllocator numbers, IGeoLookup geo, IRemoteAreaLookup remoteAreas,
    TimeProvider clock)
    : IRequestHandler<ImportOrdersCommand, Result<ImportResultDto>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ImportResultDto>> Handle(ImportOrdersCommand cmd, CancellationToken ct)
    {
        if (!cmd.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return ImportErrors.NotExcel;
        if (cmd.Length > ImportOrdersCommand.MaxBytes) return ImportErrors.TooLarge;
        var service = cmd.Service?.Trim() ?? "";
        var hub = cmd.Hub?.Trim() ?? "";
        if (service.Length > 0 && hub.Length == 0) return ImportErrors.HubRequired;

        var writer = await access.WriterAsync(ct);
        if (writer.IsFailure) return writer.Error;

        SheetData sheet;
        try { sheet = ExcelSheetReader.Read(cmd.File); }
        catch (InvalidDataException e) { return ImportErrors.Unreadable(e.Message); }

        var missing = ExcelOrderParser.MissingHeaders(sheet.Headers);
        if (missing.Count > 0) return ImportErrors.MissingColumns(missing);
        if (sheet.Rows.Count == 0) return ImportErrors.Empty;
        if (sheet.Rows.Count > ImportOrdersCommand.MaxRows) return ImportErrors.TooManyRows(sheet.Rows.Count);

        var countries = ImportCountries.Build(await geo.GetCountriesAsync(ct));
        var ctx = new ImportContext(writer.Value, service, hub, cmd.Branch?.Trim() ?? "", countries);
        var rows = sheet.Rows.Select(r => ExcelOrderParser.Parse(r, ctx)).ToList();
        var warnings = await DuplicateRefWarningsAsync(rows, writer.Value.LegacyCustomerId, ct);

        var bills = new Dictionary<int, string>();
        if (cmd.Commit)
        {
            var valid = rows.Where(r => r.IsValid).ToList();
            if (valid.Count == 0) return ImportErrors.NothingValid;
            bills = await CreateAsync(valid, writer.Value, ctx.Branch, ct);
        }

        var dtos = rows.Select(r => ToDto(r, warnings.GetValueOrDefault(r.Line), bills.GetValueOrDefault(r.Line))).ToList();
        var validCount = rows.Count(r => r.IsValid);
        return new ImportResultDto(rows.Count, validCount, rows.Count - validCount, bills.Count, dtos);
    }

    /// <summary>Ref_No trùng trong file hoặc đã có đơn trước đó → cảnh báo (không chặn, tránh nhập 1 file 2 lần).</summary>
    private async Task<Dictionary<int, List<string>>> DuplicateRefWarningsAsync(List<ImportedRow> rows, long legacyCustomerId, CancellationToken ct)
    {
        var result = new Dictionary<int, List<string>>();
        void Add(int line, string message)
        {
            if (!result.TryGetValue(line, out var list)) result[line] = list = [];
            list.Add(message);
        }

        var refs = rows.Select(r => (r.Line, Ref: r.Payload.Service.Reference.Trim())).Where(x => x.Ref.Length > 0).ToList();
        foreach (var group in refs.GroupBy(x => x.Ref, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            foreach (var (line, _) in group)
                Add(line, $"Ref_No \"{group.Key}\" bị trùng ở các dòng {string.Join(", ", group.Select(g => g.Line))}");

        var distinct = refs.Select(x => x.Ref).Distinct().ToList();
        if (distinct.Count > 0)
        {
            var existing = await db.LegacyOrders.AsNoTracking()
                .Where(o => o.CustomerId == legacyCustomerId && o.CustomerBill != null && distinct.Contains(o.CustomerBill))
                .Select(o => new { o.CustomerBill, o.OrderNumber, o.BillConnect })
                .ToListAsync(ct);
            foreach (var (line, reference) in refs)
            {
                var hit = existing.FirstOrDefault(e => string.Equals(e.CustomerBill, reference, StringComparison.OrdinalIgnoreCase));
                if (hit is not null)
                    Add(line, $"Ref_No \"{reference}\" đã có ở vận đơn {hit.OrderNumber?.ToString(CultureInfo.InvariantCulture) ?? hit.BillConnect}");
            }
        }
        return result;
    }

    /// <summary>
    /// Ghi các đơn vào dbo.MaVanDon + chi tiết kiện / dòng hàng vào 2 bảng chi tiết.
    /// Tất cả trong 1 transaction: lỗi thì không đơn nào được tạo.
    /// </summary>
    private async Task<Dictionary<int, string>> CreateAsync(List<ImportedRow> rows, LegacyCustomerRef customer, string branch, CancellationToken ct)
    {
        var today = VietnamTime.ToVietnam(clock.GetUtcNow()).Date;
        var planned = new List<(ImportedRow Row, long Number)>();

        // VSVX từng dòng, tra trước transaction (bảng tĩnh, cache theo nước) để ghi vào đơn như hệ thống cũ.
        var remote = new Dictionary<int, IReadOnlyList<RemoteAreaHit>>();
        var countryList = await geo.GetCountriesAsync(ct);
        var legacyCountries = await LegacyCountryLookup.LoadAsync(db, ct);
        foreach (var row in rows)
            remote[row.Line] = await remoteAreas.FindAsync(RemoteAreaColumns.CountryCode(row.Payload, countryList), row.Payload.Receiver.Postal, row.Payload.Receiver.City, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        var codes = new Dictionary<int, string>();
        var savedOrders = new Dictionary<int, LegacyOrder>();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Cấp số trong transaction: khóa dãy số tới khi commit (không trùng giữa các lần cấp trên portal).
            var issued = await numbers.NextAsync(rows.Count, ct);
            planned.Clear();
            codes.Clear(); // chạy lại khi lỗi tạm thời: cấp lại số từ đầu
            savedOrders.Clear();
            planned.AddRange(rows.Select((row, i) => (row, issued[i])));

            var saved = new List<(LegacyOrder, OrderPayload)>();
            foreach (var (row, number) in planned)
            {
                var countryId = LegacyCountryLookup.FindId(legacyCountries, row.CountryCode, row.Payload.Receiver.Country);
                var order = LegacyOrderFactory.FromPayload(row.Payload, customer, number, today, countryId);
                codes[row.Line] = number.ToString(CultureInfo.InvariantCulture);
                RemoteAreaColumns.Apply(order, remote[row.Line]);
                access.StampCreator(order);
                db.LegacyOrders.Add(order);
                saved.Add((order, row.Payload));
                savedOrders[row.Line] = order;
            }
            await db.SaveChangesAsync(ct);

            // Có MaVanDon.ID rồi mới ghi chi tiết kiện (MaVanDon_PCS_DIM) và dòng hàng (MaVanDon_ChiTietHang).
            await LegacyOrderLinesWriter.AddAsync(db, saved, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        // Hệ thống cũ chèn trùng số ngay sau khi commit → cấp số khác trước khi trả kết quả cho khách.
        var reissued = await numbers.ReissueTakenAsync([.. savedOrders.Values.Select(o => o.Id)], ct);
        foreach (var (line, order) in savedOrders)
            if (reissued.TryGetValue(order.Id, out var code)) codes[line] = code;
        return codes;
    }

    private static ImportRowDto ToDto(ImportedRow r, List<string>? extraWarnings, string? bill)
    {
        var p = r.Payload;
        var isDoc = p.Shipment.Type == "DOC";
        return new ImportRowDto(
            r.Line, p.Service.Reference, isDoc ? "DOC" : "PACK", p.Receiver.Company, r.CountryCode, p.Receiver.Country, p.Receiver.City,
            int.TryParse(p.Shipment.Pieces, NumberStyles.None, CultureInfo.InvariantCulture, out var pcs) ? pcs : 1,
            r.GrossKg, r.ChargeableKg, r.Value, p.Invoice.Currency, p.Invoice.Items.Count,
            r.Errors, [.. r.Warnings, .. extraWarnings ?? []], bill);
    }
}
