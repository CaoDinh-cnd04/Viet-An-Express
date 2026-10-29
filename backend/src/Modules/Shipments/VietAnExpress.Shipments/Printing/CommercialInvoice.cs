using System.Globalization;
using System.Text;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Printing;

/// <summary>
/// Invoice theo mẫu hệ thống cũ (VietAnExpress - Bill invoice.pdf), A4 dọc, mỗi đơn in 3 bản giống nhau:
/// đầu trang "INVOICE" + mã vạch; cột trái SHIPPER / CONSIGNEE (nhãn + dòng kẻ), cột phải Air waybill No., Date, No. of pkgs, Weight, Dimensions;
/// bảng hàng (mô tả Anh/ Việt + dòng nhà sản xuất - xuất xứ, HS Code, số lượng | đơn vị, đơn giá, thành tiền), Total Value,
/// Reason for Export, lời cam kết và chỗ ký.
/// </summary>
internal static class CommercialInvoice
{
    /// <summary>Số bản in mỗi đơn — mẫu hệ thống cũ in 3 bản.</summary>
    public const int Copies = 3;

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Invoice chung: 3 bản. Đơn khai invoice riêng từng kiện: mỗi kiện 3 bản, chỉ hàng / cân / kích thước của kiện đó.</summary>
    public static string Render(OrderPrintModel m)
    {
        if (!m.InvoicePerPackage) return string.Concat(Enumerable.Repeat(Page(m, InvoiceScope.Whole(m)), Copies));
        var pages = new StringBuilder();
        foreach (var scope in InvoiceScope.PerPackage(m))
        {
            var page = Page(m, scope);
            for (var i = 0; i < Copies; i++) pages.Append(page);
        }
        return pages.ToString();
    }

    /// <summary>Phạm vi 1 tờ invoice: cả đơn, hoặc 1 kiện (số kiện, cân, kích thước, dòng hàng của riêng kiện đó).</summary>
    internal sealed record InvoiceScope(IReadOnlyList<PrintItem> Lines, IReadOnlyList<PrintPackage> Packages, int Pieces, decimal GrossKg,
        decimal ShippingFee, string? PackageLabel)
    {
        public static InvoiceScope Whole(OrderPrintModel m) =>
            new(m.InvoiceLines, m.Packages, m.TotalPieces, m.TotalGrossKg, m.Order.ShippingFee is > 0 ? m.Order.ShippingFee.Value : 0m, null);

        /// <summary>
        /// Kiện 1..N (N = tổng số kiện, hoặc số kiện lớn nhất có dòng hàng nếu lớn hơn). Shipping fee của đơn in ở invoice kiện 1
        /// để tổng các invoice khớp tổng giá trị đơn. Hàm thuần — có test.
        /// </summary>
        public static IReadOnlyList<InvoiceScope> PerPackage(OrderPrintModel m)
        {
            var singles = m.SinglePackages;
            var count = Math.Max(Math.Max(singles.Count, m.TotalPieces), m.Items.Max(i => i.Package ?? 0));
            var fee = m.Order.ShippingFee is > 0 ? m.Order.ShippingFee.Value : 0m;
            return [.. Enumerable.Range(1, count).Select(n =>
            {
                var pkg = n <= singles.Count ? singles[n - 1] : null;
                return new InvoiceScope(
                    [.. m.Items.Where(i => i.Package == n)],
                    pkg is null ? [] : [pkg], 1, pkg?.GrossKg ?? 0,
                    n == 1 ? fee : 0m,
                    string.Create(CultureInfo.InvariantCulture, $"{n}/{count}"));
            })];
        }
    }

    /// <summary>Ngày trên invoice dạng "Sep 25, 2026" như mẫu.</summary>
    public static string InvoiceDate(DateTime? value) => value?.ToString("MMM d, yyyy", En) ?? "";

    private static string Page(OrderPrintModel m, InvoiceScope scope)
    {
        var o = m.Order;
        var lines = scope.Lines;
        var currency = Doc.H(string.IsNullOrWhiteSpace(o.Currency) ? "USD" : o.Currency.Trim());
        var rows = new StringBuilder();
        foreach (var l in lines)
        {
            var detail = Doc.Join(" - ", l.Manufacturer, string.IsNullOrWhiteSpace(l.Origin) ? null : $"Country of origin: {l.Origin}");
            rows.Append(CultureInfo.InvariantCulture, $"""
                <tr>
                  <td class="desc"><div>{Doc.H(l.Name("/ "))}</div>{(detail.Length > 0 ? $"<div>{Doc.H(detail)}</div>" : "")}</td>
                  <td class="c">{Doc.H(l.HsCode)}</td>
                  <td class="qty">{l.Quantity.ToString("0.00", CultureInfo.InvariantCulture)}</td><td class="unit">{Doc.H(l.Unit)}</td>
                  <td class="num">{Doc.Money(l.UnitPrice)}</td><td class="num">{Doc.Money(l.Amount)}</td>
                </tr>
                """);
        }
        // Shipping fee khách khai (nếu có) in thành 1 dòng riêng và cộng vào Total Value — khớp tổng invoice lúc tạo đơn.
        var fee = scope.ShippingFee;
        var feeRow = fee > 0
            ? $"<tr class=\"fee\"><td colspan=\"5\">Shipping fee (in {currency})</td><td class=\"num\">{Doc.Money(fee)} {currency}</td></tr>"
            : "";
        var total = lines.Sum(l => l.Amount) + fee;
        var gross = scope.GrossKg;
        var pieces = scope.PackageLabel is null ? scope.Pieces.ToString(CultureInfo.InvariantCulture) : $"1 (Package {scope.PackageLabel})";
        var dims = string.Join("<br>", scope.Packages.Select(p =>
            Doc.H(string.Create(CultureInfo.InvariantCulture, $"{p.Qty}*({Doc.Qty(p.Length)}*{Doc.Qty(p.Width)}*{Doc.Qty(p.Height)})"))));
        // Như hệ thống cũ: dòng 1 địa chỉ ở "Address", dòng 2 nối sau số điện thoại ở "Phone/Fax/Mail".
        var (addr1, addr2) = BillA4.SplitAddress(o.SenderAddress, 60);
        var consigneeAddress = new[] { o.ConsigneeAddress1, o.ConsigneeAddress2, o.ConsigneeAddress3 }
            .Where(a => !string.IsNullOrWhiteSpace(a)).DefaultIfEmpty("").ToList();

        return $$"""
            <section class="page a4 invoice">
              <div class="inv-head">
                <div class="inv-title">INVOICE</div>
                {{Code128.Svg(m.Bill, 36)}}
              </div>
              <div class="inv-top">
                <div class="inv-left">
                  <div class="sec">SHIPPER</div>
                  {{Field("Company Name:", o.SenderName)}}
                  {{Field("Address:", addr1)}}
                  {{Field("Town/ Area Code:", o.SenderPostalCode)}}
                  {{Field("State/ Country:", m.SenderCountryText)}}
                  {{Field("Contact Name:", o.SenderContactName)}}
                  {{Field("Phone/Fax/Mail:", Doc.Join(" ", o.SenderPhone, addr2, o.SenderEmail))}}

                  <div class="sec consignee">CONSIGNEE</div>
                  {{Field("Company Name:", o.ConsigneeName, bold: true)}}
                  {{string.Concat(consigneeAddress.Select((a, i) => Field(i == 0 ? "Address:" : "", a)))}}
                  {{Field("Post code/ City/ State:", Doc.Join(", ", o.ConsigneePostalCode, o.ConsigneeCity, o.ConsigneeState))}}
                  {{Field("Country:", o.ConsigneeCountry)}}
                  {{Field("Contact Name:", o.ConsigneeContactName)}}
                  {{Field("Phone/Fax No.:", BillA4.PhoneWithCode(o.ConsigneePhoneCode, o.ConsigneePhone))}}
                </div>
                <div class="inv-right">
                  <div class="kv awb"><span>Air waybill<br>No.:</span><b>{{Doc.H(m.Bill)}}</b></div>
                  <div class="kv"><span>Date:</span><div>{{InvoiceDate(o.CreateDate)}}</div></div>
                  <div class="kv"><span>No. of pkgs :</span><div>{{Doc.H(pieces)}}</div></div>
                  <div class="kv"><span>Weight:</span><div>{{Doc.Qty(gross)}} kg</div></div>
                  <div class="kv"><span>Dimensions:</span><div>{{dims}}</div></div>
                </div>
              </div>

              <table class="goods">
                <tr>
                  <th class="desc">Full Description of Goods<br><b>(Name of goods, composition of material, marks, etc)</b></th>
                  <th class="hs">HS Code</th>
                  <th colspan="2" class="qty-h">Quantily<br>(pcs)</th>
                  <th class="money">Unit Price<br>(in {{currency}})</th>
                  <th class="money">Subtotal<br>(in {{currency}})</th>
                </tr>
                {{rows}}
                {{feeRow}}
                <tr class="total"><td colspan="5">Total Value (in {{currency}})</td><td class="num">{{Doc.Money(total)}} {{currency}}</td></tr>
              </table>

              <div class="inv-foot">
                <p>Reason for Export: <b>{{Doc.H(o.ExportReason)}}</b></p>
                <p>I declare that the information is true and correct to the best of my knowledge<br>and that the goods are of Viet Nam origin.</p>
                <p>I (name) &nbsp;<b>{{Doc.H(o.SenderName)}}</b>&nbsp; certify that the particulars and<br>quantity of goods specified in this document are goods which are submitted for<br>clearance for export out of Viet Nam.</p>
              </div>
              <div class="inv-sign">Signature/Title/Stamp</div>
            </section>
            """;
    }

    private static string Field(string label, string? value, bool bold = false) =>
        $"<div class=\"field\"><span class=\"lbl\">{Doc.H(label)}</span><span class=\"val{(bold ? " b" : "")}\">{Doc.H(value)}</span></div>";

    /// <summary>Kích thước đo theo mẫu PDF (A4 dọc, lề trái / phải ~19 mm).</summary>
    public const string Css = """
        .invoice { font-family: Aptos, "Aptos Body", Arial, Helvetica, sans-serif; font-size: 9px; color: #222; padding: 14mm 19mm; }
        .inv-head { display: grid; grid-template-columns: 85mm 37mm; align-items: center; height: 12mm; margin-bottom: 4mm; }
        .inv-title { padding-left: 25mm; font-size: 21px; color: #555; letter-spacing: .3px; }
        .inv-head .barcode { display: block; width: 37mm; height: 8mm; }
        .inv-top { display: grid; grid-template-columns: 99mm 1fr; gap: 12mm; margin-bottom: 6mm; }
        .sec { font-size: 10px; font-weight: 700; color: #666; letter-spacing: .5px; margin-bottom: 2mm; }
        .sec.consignee { margin-top: 5mm; }
        .field { display: grid; grid-template-columns: 24mm 1fr; align-items: end; height: 5.6mm; }
        .field .lbl, .field .val { line-height: 4mm; padding-bottom: .9mm; white-space: nowrap; }
        .field .val { border-bottom: 1px solid #000; padding-left: 1.2mm; min-height: 4.9mm; overflow: hidden; text-overflow: ellipsis; }
        .field .val.b { font-weight: 700; }
        .inv-right { padding-top: 1mm; width: 38mm; }
        .kv { display: grid; grid-template-columns: 17.5mm 1fr; align-items: end; min-height: 6mm; }
        .kv > span { font-weight: 700; padding-bottom: 1.2mm; line-height: 1.3; }
        .kv > div, .kv > b { border-bottom: 1px solid #000; padding: 0 0 1.2mm 1.2mm; min-height: 4.6mm; }
        .kv.awb { min-height: 8mm; }
        .kv.awb b { font-size: 16px; line-height: 1.1; }
        table.goods { width: 100%; border-collapse: collapse; margin-bottom: 2.5mm; }
        .goods th, .goods td { border: 1px solid #333; padding: 1.8mm 1.5mm; }
        .goods th { font-size: 9px; font-weight: 700; text-align: center; vertical-align: middle; line-height: 1.6; }
        .goods th.desc { width: 53%; }
        .goods th.desc b { font-size: 9px; }
        .goods th.hs { width: 9.5%; }
        .goods th.money { width: 9.5%; }
        .goods td { vertical-align: middle; line-height: 1.7; }
        .goods td.desc { font-size: 8.5px; }
        .goods .c { text-align: center; }
        .goods td.qty { text-align: right; width: 8.5%; border-right: 1px solid #333; }
        .goods td.unit { text-align: left; width: 8.5%; }
        .goods .num { text-align: right; white-space: nowrap; }
        .goods tr.total td { font-weight: 700; text-align: center; }
        .goods tr.total td.num { text-align: right; }
        .goods tr.fee td { text-align: center; }
        .goods tr.fee td.num { text-align: right; }
        .inv-foot { font-size: 8.5px; line-height: 1.6; }
        .inv-foot p { margin: 0 0 2mm; }
        .inv-sign { text-align: right; font-size: 8.5px; padding-right: 1.5mm; margin-top: 2mm; }
        @media print { .invoice { padding: 2mm 7mm 0; } }
        """;
}
