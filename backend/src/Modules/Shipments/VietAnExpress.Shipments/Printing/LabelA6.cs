using System.Globalization;
using System.Text;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;
using static VietAnExpress.Shipments.Printing.Doc;

namespace VietAnExpress.Shipments.Printing;

/// <summary>
/// Nhãn A6 (105 × 148 mm, khổ A6 chuẩn để máy in chọn đúng giấy A6): mỗi đơn 1 trang vận đơn A6 "Liên 1: người gửi lưu" như hệ thống cũ
/// (05/11/2026 người dùng chọn 1 liên — trước có 3 liên rồi bỏ, xem git d7ce74e), sau đó 1 shipping mark / kiện để dán lên thùng.
/// </summary>
internal static class LabelA6
{
    /// <summary>QR số bill thuần để CMS quét — dùng lại cho trang vận đơn và mọi shipping mark của đơn.</summary>
    private sealed record OrderArt(string Qr);

    public static string Render(OrderPrintModel m, CompanyInfo c, DateTime printedAt)
    {
        var art = new OrderArt(BillA4.Qr(m.Bill));
        var pieces = BillA4.PieceList(m.Packages, m.Order.Pieces ?? 1);
        var sb = new StringBuilder(SlipHtml(m, c, art));
        for (var i = 0; i < pieces.Count; i++) sb.Append(Mark(m, c, printedAt, i + 1, pieces.Count, pieces[i], art));
        return sb.ToString();
    }

    // ---------------- Vận đơn A6 (Liên 1) ----------------

    /// <summary>
    /// 6 dòng người gửi như nhãn A6 hệ thống cũ. Đơn hệ thống cũ (<see cref="BillA4.LegacySenderLayout"/>): SenderName · SenderAddress · nước ·
    /// người liên hệ · điện thoại · cột Email (địa chỉ CS ghi). Đơn portal: tên · địa chỉ dòng 1 · nước · người liên hệ · điện thoại · địa chỉ dòng 2.
    /// </summary>
    internal static string[] SenderLines(OrderPrintModel m)
    {
        var o = m.Order;
        if (BillA4.LegacySenderLayout(o)) return [o.SenderName ?? "", o.SenderAddress ?? "", m.SenderCountryText, o.SenderContactName ?? "", o.SenderPhone ?? "", o.SenderEmail ?? ""];
        var (addr1, addr2) = BillA4.SplitAddress(o.SenderAddress);
        return [o.SenderName ?? "", addr1, m.SenderCountryText, o.SenderContactName ?? "", o.SenderPhone ?? "", addr2];
    }

    private static string SlipHtml(OrderPrintModel m, CompanyInfo c, OrderArt art)
    {
        var o = m.Order;
        var phone = BillA4.PhoneWithCode(o.ConsigneePhoneCode, o.ConsigneePhone);
        var tel = string.IsNullOrWhiteSpace(o.ConsigneeEmail) ? H(phone) : $"{H(phone)} | {H(o.ConsigneeEmail)}";
        var lines = SenderLines(m);
        var sender = string.Concat(lines.Select((v, i) => $"<div class=\"ln{(i == lines.Length - 1 ? " last" : "")}\">{H(v)}</div>"));

        return $"""
            <section class="page a6 slip">
              <div class="box">
                {Head(m, c, Code128.Svg(m.Bill, 36), $"<div class=\"awb-no\">{H(m.Bill)}</div>")}
                <div class="bar">1. <i>(Sender's information)</i>:</div>
                <div class="sender">
                  <div class="qr"><div class="qr-frame">{art.Qr}</div><div class="qr-cap">Quét QR để tracking</div></div>
                  <div class="lines">{sender}</div>
                </div>
                <div class="bar">2. <i>(Consignee's information)</i>:</div>
                <div class="party cnee">
                  <div><i>Co.</i> <b>: {H(o.ConsigneeName)}</b></div>
                  <div class="addr"><i>Add</i> <b>: {H(ConsigneeAddress(o))}</b></div>
                  <div class="indent"><b>{H(CityLine(o))}</b></div>
                  <div><i>Att</i> <b>: {H(o.ConsigneeContactName)}</b></div>
                  <div><i>Tel</i> <b>: {tel}</b></div>
                </div>
                <div class="bar">3. <i>(Shipment information)</i>:</div>
                <div class="ship"><span>(Content):</span><b>{H(o.GoodsName)}</b></div>
                <div class="ship"><span>(value invoice):</span><b>{H(BillA4.Value(o))}</b></div>
                {Packages(m)}
                <div class="signs">
                  <div class="sign"><div>Chữ ký người gửi</div><div class="sign-date"><b>Ngày, giờ</b> <i>(Date /time)</i>: {Date(o.CreateDate)}</div></div>
                  <div class="sign"><div>Nhân viên nhận hàng</div><div class="sign-date"><b>Ngày, giờ</b>:<br><i>NV kinh doanh</i> :</div></div>
                </div>
              </div>
              <div class="foot">
                <div class="slip-name">Liên 1: người gửi lưu</div>
                <div class="foot-awb">{Code128.Svg(m.Bill, 30)}<div>{H(m.Bill)}</div></div>
              </div>
            </section>
            """;
    }

    /// <summary>Số kiện · TL thực · TL quy đổi (như bill A4: mỗi dòng kiện làm tròn lên 0,5 kg) · tối đa 3 dòng kích thước.</summary>
    private static string Packages(OrderPrintModel m)
    {
        var pk = m.Packages;
        const int maxLines = 3;
        var measured = pk.Where(p => p.Length * p.Width * p.Height > 0).ToList();
        var dims = measured.Take(maxLines).Select(p =>
            $"{H(Kg1(p.GrossKg))} | {p.Qty}*({Qty(p.Length)}*{Qty(p.Width)}*{Qty(p.Height)})=<b>{H(Kg1(BillA4.BillVolumeKg(p)))}</b>").ToList();
        if (measured.Count > maxLines) dims.Add($"… +{measured.Count - maxLines}");
        var vol = pk.Count > 0 ? Kg1(pk.Sum(BillA4.BillVolumeKg)) : "";

        return $"""
            <div class="pkg">
              <div class="ph">Số kiện<br><i>(PCS)</i></div>
              <div class="ph">TL thực<br><i>(G.W)</i></div>
              <div class="ph">TL Qui Đổi<br><i>(Vol.W)</i></div>
              <div class="dims"><div class="dims-title">(Dimensions)</div>{string.Concat(dims.Select(d => $"<div class=\"dim\">{d}</div>"))}</div>
              <div class="pv">{m.TotalPieces}</div>
              <div class="pv">{Kg1(m.TotalGrossKg)}</div>
              <div class="pv">{vol}</div>
            </div>
            """;
    }

    // ---------------- Shipping mark (1 / kiện) ----------------

    private static string Mark(OrderPrintModel m, CompanyInfo c, DateTime printedAt, int index, int total, PrintPackage? piece, OrderArt art)
    {
        var o = m.Order;
        var pieceNo = $"{m.Bill}/{index}";
        var gw = BillA4.PieceWeight(m, piece, total).ToString("0.00", CultureInfo.InvariantCulture);
        var dim = piece is null ? "<b>0</b>*<b>0</b>*<b>0</b>" : $"<b>{Qty(piece.Length)}</b>*<b>{Qty(piece.Width)}</b>*<b>{Qty(piece.Height)}</b>";
        // Kích thước có số lẻ dài (vd 108.25*132.5*123.75) → chữ nhỏ lại để vẫn nằm cùng hàng với mã vạch.
        var dimClass = piece is not null && $"{Qty(piece.Length)}*{Qty(piece.Width)}*{Qty(piece.Height)}".Length > 11 ? "m-wd sm" : "m-wd";
        var phone = BillA4.PhoneWithCode(o.ConsigneePhoneCode, o.ConsigneePhone);

        return $"""
            <section class="page a6 mark">
              <div class="box">
                {Head(m, c, Code128.Svg(pieceNo, 36), $"<div class=\"awb-no\">{H(m.Bill)} <small>/{index}</small></div>")}
                <div class="m-title"><span>SHIPPING MARK<br><i>HAWB:</i></span><b>{H(m.Bill)}</b></div>
                <div class="bar center"><i>Ref no.:</i> {H(o.CustomerBill)}</div>
                <div class="m-dest">
                  <div class="qr-frame">{art.Qr}</div>
                  <div class="m-dest-txt"><div class="m-dest-lbl">DESTINATION</div><div class="m-dest-code">{H(m.Destination)}</div></div>
                </div>
                <div class="m-pcs"><i>Pcs no:</i><b>{index} / {total}</b></div>
                <div class="bar sender-part"><i>Sender</i>:</div>
                <div class="party small sender-part">
                  <div><i>Co.</i> <b>: {H(o.SenderName)}</b></div>
                  <div><i>Att</i> <b>: {H(o.SenderContactName)}</b></div>
                </div>
                <div class="bar"><i>Consignee</i>:</div>
                <div class="party">
                  <div><i>Co.</i> <b>: {H(o.ConsigneeName)}</b></div>
                  <div class="addr"><i>Add</i><b>: {H(ConsigneeAddress(o))}</b></div>
                  <div class="indent"><b>{H(CityLine(o))}</b></div>
                  <div><i>Att</i> <b>: {H(o.ConsigneeContactName)}</b> | <i>Tel</i> <b>: {H(phone)}</b></div>
                </div>
              </div>
              <div class="m-foot">
                <div class="foot-awb">{Code128.Svg(pieceNo, 30)}<div>{H(pieceNo)}</div></div>
                <div class="m-wd">G.W: <b>{gw}kg</b></div>
                <div class="{dimClass}">DIM: {dim} cm</div>
              </div>
              <div class="m-date">{Date(printedAt)}</div>
            </section>
            """;
    }

    private static string Head(OrderPrintModel m, CompanyInfo c, string barcodeSvg, string awbHtml) => $"""
        <div class="head">
          <img class="head-img" src="{BillA4.HeaderDataUri.Value}" alt="Viet An Express · Tel {H(c.Phone)} · Hotline {H(c.Hotline)}">
          <div class="awb">{barcodeSvg}{awbHtml}<div class="route">{H(m.ServiceText)}</div></div>
        </div>
        """;

    private static string ConsigneeAddress(LegacyOrder o) => Join(" , ", o.ConsigneeAddress1, o.ConsigneeAddress2, o.ConsigneeAddress3);

    private static string CityLine(LegacyOrder o) => Join(", ", o.ConsigneeCity, o.ConsigneeState, o.ConsigneePostalCode, o.ConsigneeCountry);

    private static string Kg1(decimal value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    // ---------------- CSS (A6: 105 × 148 mm) ----------------

    /// <summary>Khổ giấy khi bấm "In giấy A4 ngang" (script trang in ghi vào &lt;style id="page-size"&gt;). Không chứa dấu nháy đơn (nằm trong chuỗi JS).</summary>
    public const string A4PageRule = "@page { size: 297mm 210mm; margin: 0; }";

    public const string Css = """
        @page { size: 105mm 148mm; margin: 0; }
        * { box-sizing: border-box; }
        /* Times New Roman như mọi chứng từ (05/11/2026), chữ nhỏ nhất 8px (trước 6px): máy in nhiệt 203 dpi in rõ, không đứt nét. */
        body { margin: 0; font-family: "Times New Roman", Times, serif; font-size: 9px; color: #000; background: #e9ece9;
               -webkit-print-color-adjust: exact; print-color-adjust: exact; text-rendering: geometricPrecision; }
        .toolbar { position: sticky; top: 0; display: flex; gap: 12px; align-items: center; padding: 10px 16px; background: #1e6b2c; color: #fff; z-index: 1; font-size: 12px; }
        .toolbar button { padding: 6px 18px; border: 0; border-radius: 6px; background: #fff; color: #1e6b2c; font-weight: 700; cursor: pointer; }
        .toolbar .hint { opacity: .85; }
        /* Nút "Ẩn / hiện người gửi" (05/11/2026, như nút Show/Hide Sender của hệ thống cũ): ẩn mục Sender khỏi nhãn khi in. */
        .no-sender .sender-part { display: none; }
        .a6 { width: 105mm; height: 148mm; padding: 3mm; margin: 16px auto; background: #fff; box-shadow: 0 2px 10px rgba(0,0,0,.15);
              display: flex; flex-direction: column; overflow: hidden; break-after: page; page-break-after: always; }
        .a6:last-of-type { break-after: auto; page-break-after: auto; }
        .box { border: 1px solid #000; display: flex; flex-direction: column; flex: none; }
        .head { display: grid; grid-template-columns: 22mm 1fr 31mm; gap: 1.5mm; align-items: center; padding: 1mm 1.5mm; height: 23mm; border-bottom: 1px solid #000; }
        /* Hình đầu chiếm 2 cột trái (logo + tên + liên hệ), canh trái, giữ tỉ lệ. */
        .head-img { grid-column: 1 / 3; width: 100%; height: 21mm; object-fit: contain; object-position: left center; }
        .awb { text-align: center; line-height: 1.1; font-family: "Times New Roman", Times, serif; }
        .awb .barcode { display: block; width: 100%; height: 8mm; }
        /* Mã bill dưới mã vạch: chữ thường, cỡ tự nhiên, căn giữa (không kéo giãn bằng mã vạch) */
        .awb-code { margin-top: .5mm; font-size: 14px; font-weight: 400; line-height: 1.05; white-space: nowrap; }
        .awb-no { margin-top: .3mm; font-size: 15px; font-weight: 400; line-height: 1.05; white-space: nowrap; }
        .awb-no small { font-size: 11px; }
        /* Tuyến dài (CT-AU_Toll(T.Phẩm/M.Phẩm)): chữ nhỏ, tối đa 2 dòng — không đẩy cao phần đầu nhãn . */
        .route { font-size: 8px; font-weight: 700; line-height: 1.1; max-height: 2.2em; overflow: hidden; overflow-wrap: anywhere; }
        .bar { background: none; font-size: 11px; font-weight: 700; padding: .2mm 1.5mm; border-top: 1px solid #000; border-bottom: 1px solid #000; }
        .bar.center { text-align: center; }
        .head + .bar, .m-title + .bar, .m-pcs + .bar { border-top: 0; }
        .sender { display: grid; grid-template-columns: 22mm 1fr; padding: .5mm 1.5mm .8mm 0; }
        .qr { display: flex; flex-direction: column; align-items: center; justify-content: center; }
        .qr-frame { position: relative; width: 17mm; height: 17mm; padding: 1.2mm; background: #fff; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        .qr-frame::before, .qr-frame::after { content: ""; position: absolute; top: 0; bottom: 0; width: 2.5mm; border: 1px solid #000; }
        .qr-frame::before { left: 0; border-right: 0; }
        .qr-frame::after { right: 0; border-left: 0; }
        .qr-frame svg { display: block; width: 100%; height: 100%; shape-rendering: crispEdges; }
        .qr-cap { color: #e00; font-style: italic; font-size: 8px; }
        .lines { display: flex; flex-direction: column; min-width: 0; }
        .ln { border-bottom: .8px dashed #000; min-height: 3.6mm; line-height: 3.6mm; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .ln.last { border-bottom: 0; }
        .party { padding: .5mm 1.5mm; font-size: 11.5px; line-height: 1.25; }
        .party > div { overflow: hidden; }
        .party .addr { max-height: 3.5em; }
        .party .indent { padding-left: 4mm; }
        .party.small { font-size: 10px; line-height: 1.25; }
        .ship { display: grid; grid-template-columns: 22mm 1fr; gap: 2mm; padding: .6mm 1.5mm; border-top: 1px solid #000; white-space: nowrap; overflow: hidden; }
        .ship span { text-align: right; }
        .ship b { overflow: hidden; text-overflow: ellipsis; }
        /* Trang vận đơn A6 (Liên 1): bảng kiện dạng lưới như bill A4 — số nằm giữa ô dưới tiêu đề. */
        .pkg { display: grid; grid-template-columns: 13mm 13mm 13mm 1fr; grid-template-rows: auto 1fr; border-top: 1.2px solid #000; }
        .pkg .ph, .pkg .pv { border-right: 1px solid #000; text-align: center; padding: .4mm .6mm; }
        .pkg .ph { font-size: 8px; font-weight: 700; line-height: 1.15; }
        .pkg .pv { display: flex; align-items: center; justify-content: center; min-height: 8mm; font-size: 15px; font-weight: 700; }
        .pkg .dims { grid-column: 4; grid-row: 1 / 3; font-weight: 400; text-align: left; padding: .4mm 1mm; }
        .slip .party { font-size: 10px; line-height: 1.35; }
        .slip .ln { font-size: 9px; }
        .dims-title { text-align: center; font-weight: 700; border-bottom: .8px dashed #000; }
        .dim { font-size: 8px; line-height: 1.4; }
        .signs { display: grid; grid-template-columns: 1fr 1fr; min-height: 13mm; border-top: 1px solid #000; }
        .sign { display: flex; flex-direction: column; justify-content: space-between; padding: .6mm 1.2mm; }
        .sign + .sign { border-left: 1px solid #000; }
        .sign-date { font-size: 8px; line-height: 1.25; }
        .foot { display: flex; justify-content: space-between; align-items: flex-end; padding: 1.5mm 1mm 0; }
        .total { font-size: 11px; font-weight: 700; }
        .slip-name { font-size: 8px; }
        /* Mã vạch số bill/số kiện để máy quét kho đọc: đủ rộng (vạch ≥ 0,3mm) và cao */
        .foot-awb { flex: none; width: 38mm; text-align: center; font-family: "Times New Roman", Times, serif; font-size: 15px; font-weight: 400; line-height: 1.1; }
        .foot-awb .barcode { display: block; width: 100%; height: 10mm; }
        .m-title { display: flex; justify-content: center; align-items: center; gap: 2mm; padding: .3mm 0; border-bottom: 1px solid #000; }
        .m-title span { font-size: 12px; font-weight: 700; text-align: right; line-height: 1.15; }
        .m-title b { font-size: 34px; line-height: 1; }
        /* 3 cột 1fr | auto | 1fr: mã nước và số kiện nằm đúng giữa nhãn, thẳng với Ref no. */
        .m-dest { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; padding: 1mm 1.5mm; border-bottom: 1px solid #000; }
        /* Mã nước đến to, căn giữa phần còn lại bên phải QR */
        .m-dest-txt { text-align: center; }
        .m-dest .qr-frame { justify-self: start; margin-left: 2mm; width: 23mm; height: 23mm; }
        .m-dest-lbl { font-weight: 700; font-size: 12px; }
        .m-dest-code { font-size: 72px; font-weight: 700; line-height: 1; }
        .m-pcs { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; padding: .5mm 1.5mm; font-size: 15px; font-weight: 700; border-bottom: 1px solid #000; }
        .m-pcs i { padding-left: 1.5mm; }
        .m-pcs b { font-size: 48px; line-height: 1; text-align: center; word-spacing: 2mm; }
        /* Mã vạch kiện, G.W, DIM cùng 1 hàng */
        .m-wd { font-size: 12px; white-space: nowrap; }
        .m-wd b { font-size: 16px; }
        .m-wd.sm { font-size: 8px; }
        .m-wd.sm b { font-size: 10px; }
        .m-foot { display: flex; justify-content: space-between; align-items: center; gap: 2mm; padding: 1.5mm .5mm 0; }
        .m-date { text-align: center; font-size: 22px; font-weight: 700; margin-top: 2mm; }
        @media print {
          /* Máy in nhiệt / đen trắng: hình đầu chuyển xám đậm để logo xanh không bị mất. */
          .head-img { filter: grayscale(1) contrast(1.3); }
          body { background: #fff; }
          .toolbar { display: none; }
          /* Căn nhãn giữa trang: khổ giấy khác A6 (vd A4, giấy nhiệt khổ rộng) vẫn nằm chính giữa chiều ngang. */
          .a6 { margin: 0 auto; box-shadow: none; }
        }
        /* Giấy A4 ngang (nút "In giấy A4 ngang", 05/11/2026): 2 nhãn cạnh nhau mỗi tờ, giữ đúng cỡ A6 để cắt dán thùng;
           dòng 2 nhãn cao 148 mm > nửa tờ 210 mm nên mỗi tờ đúng 1 dòng, tự sang tờ mới. */
        body.a4 { text-align: center; }
        .a4 .a6 { display: inline-flex; vertical-align: top; text-align: left; margin: 16px 6mm; break-after: auto; page-break-after: auto; break-inside: avoid; }
        @media print {
          .a4 .a6 { margin: 31mm 8mm 0; outline: .2mm dashed #999; }
        }
        """;
}
