using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Infrastructure;
using VietAnExpress.Shipments.Infrastructure.Legacy;

namespace VietAnExpress.Shipments.Application.Orders;

internal sealed record CopyOrderQuery(string Bill) : IRequest<Result<JsonElement>>;

/// <summary>Return form data only; the new order never inherits the source bill, tracking or delivery state.</summary>
internal sealed class CopyOrderHandler(ShipmentsDbContext db, OrderAccess access) : IRequestHandler<CopyOrderQuery, Result<JsonElement>>
{
    public async Task<Result<JsonElement>> Handle(CopyOrderQuery q, CancellationToken ct)
    {
        var code = q.Bill.Trim();
        var number = VaBillCode.Number(code);
        var scope = await access.ScopeAsync(ct);
        var orders = access.Apply(db.LegacyOrders.AsNoTracking(), scope);
        orders = number is { } parsed ? orders.Where(o => o.OrderNumber == parsed || o.BillConnect == code) : orders.Where(o => o.BillConnect == code);
        var order = await orders.OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
        if (order is null) return OrderErrors.NotFound(q.Bill);

        var id = order.Id is > 0 and <= int.MaxValue ? (int)order.Id : 0;
        var packages = await db.LegacyPackageLines.AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.Id).ToListAsync(ct);
        var items = await db.LegacyInvoiceLines.AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.Id).ToListAsync(ct);
        return JsonSerializer.SerializeToElement(OrderCopy.FromLegacy(order, packages, items), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}

internal static class OrderCopy
{
    private static string S(string? v) => v?.Trim() ?? "";
    private static string N(decimal? v) => v?.ToString("0.############################", CultureInfo.InvariantCulture) ?? "";
    public static OrderPayload FromLegacy(LegacyOrder o, IReadOnlyList<LegacyPackageLine> packages, IReadOnlyList<LegacyInvoiceLine> items)
    {
        var doc = LegacyOrderView.IsDocument(o.GoodsName);
        var route = S(o.ServiceName).Replace("|", " - ");
        var carrier = route.Split(" - ", 2)[0];
        return new OrderPayload
        {
            Shipper = new()
            {
                Company = S(o.SenderName), OriginalShipper = S(o.ForwarderShipperName), Contact = S(o.SenderContactName),
                Tel = S(o.SenderPhone), Address = S(o.SenderAddress), Email = S(o.SenderEmail), TaxId = S(o.SenderTax),
                Country = "Vietnam", Branch = VaBillCode.BranchOf(o.VaBill) ?? VaBillCode.BranchOfPrefix(o.VaBill) ?? "TP.HCM"
            },
            Receiver = new()
            {
                Company = S(o.ConsigneeName), Contact = S(o.ConsigneeContactName), Tel = S(o.ConsigneePhone),
                Country = S(o.ConsigneeCountry), City = S(o.ConsigneeCity), State = S(o.ConsigneeState), Postal = S(o.ConsigneePostalCode),
                Addr1 = S(o.ConsigneeAddress1), Addr2 = S(o.ConsigneeAddress2), Addr3 = S(o.ConsigneeAddress3),
                Email = S(o.ConsigneeEmail), TaxId = S(o.ConsigneeVatTax), PhoneCode = S(o.ConsigneePhoneCode),
                IossNo = S(o.ConsigneeIossNo), EoriNo = S(o.ConsigneeEoriNo)
            },
            Service = new() { Carrier = carrier, Hub = route.Contains(" - ") ? route : "", Reference = S(o.CustomerBill) },
            Shipment = new()
            {
                Type = doc ? "DOC" : "PACK", Pieces = N(packages.Count > 0 ? packages.Sum(p => p.Quantity) : o.Pieces),
                GrossWeight = N(packages.Count > 0 ? packages.Sum(p => p.WeightKg) : o.WeightKg)
            },
            Goods = new() { Description = doc ? "" : S(o.GoodsName), DocContent = doc ? S(o.GoodsName) : "" },
            Packages = packages.Select(p => new OrderPayload.PackagePart
            {
                Type = S(p.PackType), Packaging = Packaging(p.PackType), Qty = N(p.Quantity), Length = N(p.LengthCm), Width = N(p.WidthCm), Height = N(p.HeightCm),
                Weight = N(p.Quantity > 0 ? p.WeightKg / p.Quantity : p.WeightKg), Category = LegacyOrderFactory.MultiCategory, Description = S(o.GoodsName)
            }).ToList(),
            Invoice = new()
            {
                Currency = S(o.Currency).Length > 0 ? S(o.Currency) : "USD", ExportType = S(o.ExportReason), DutyTerms = "DDU",
                ShippingFee = N(o.ShippingFee), DeclaredValue = N(o.GoodsValue),
                Items = items.Select(i => new OrderPayload.InvoiceItemPart
                {
                    DescEn = S(i.DescriptionEn), DescVi = S(i.DescriptionVi), Manufacturer = S(i.Manufacturer), Origin = S(i.Origin),
                    Hs = S(i.HsCode), Qty = N(i.Quantity), Unit = S(i.Unit), Price = N(i.UnitPrice)
                }).ToList()
            }
        };
    }

    private static string Packaging(string value) => value.Trim().ToUpperInvariant() switch
    {
        "CARTON" or "CTN" or "BOX" => "Thùng carton",
        "BAG" or "ENVELOPE" => "Bao / túi", "PALLET" => "Pallet", "WOODEN CASE" or "WOOD" => "Kiện gỗ",
        _ => value
    };
}
