using System.Globalization;
using System.Net;
using System.Text;
using QRCoder;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Printing;

/// <summary>
/// Bill A4 theo mẫu hệ thống cũ (VietAnExpress - Bill Online.pdf): A4 ngang, mỗi đơn 2 trang —
/// trang 1: liên 1 (người gửi lưu) + điều khoản dịch vụ; trang 2: liên 2 (bưu cục gốc) + liên 3 (bưu cục phát);
/// tiếp theo là shipping mark dán lên từng kiện (2 nhãn / trang).
/// </summary>
internal static class BillA4
{
    public const decimal VolumeDivisor = 5000m;

    private enum Slip { Shipper = 1, Origin = 2, Destination = 3 }

    public static string Render(OrderPrintModel m, CompanyInfo c, DateTime printedAt) => $"""
        <section class="page sheet">{SlipHtml(m, c, Slip.Shipper, printedAt)}{Terms}</section>
        <section class="page sheet">{SlipHtml(m, c, Slip.Origin, printedAt)}{SlipHtml(m, c, Slip.Destination, printedAt)}</section>
        {ShippingMarks(m, c, printedAt)}
        """;

    /// <summary>
    /// Cân / kích thước từng kiện theo thứ tự (mở rộng dòng kiện theo số lượng).
    /// Đơn không có chi tiết kiện (hệ thống cũ) → <paramref name="pieces"/> kiện trống số liệu.
    /// </summary>
    public static IReadOnlyList<PrintPackage?> PieceList(IReadOnlyList<PrintPackage> packages, int pieces) =>
        packages.Count > 0
            ? packages.SelectMany(p => Enumerable.Repeat<PrintPackage?>(p with { Qty = 1, StoredVolumeKg = p.Qty > 0 ? p.StoredVolumeKg / p.Qty : null }, p.Qty)).ToList()
            : Enumerable.Repeat<PrintPackage?>(null, Math.Max(1, pieces)).ToList();

    /// <summary>Tên nước tiếng Anh → mã ISO 2 ký tự (vd "Singapore" → "SG"); không nhận ra thì trả tên viết hoa.</summary>
    public static string CountryCode(string? country)
    {
        var name = (country ?? "").Trim();
        if (name.Length == 2) return name.ToUpperInvariant();
        return CountryCodes.Value.TryGetValue(name, out var code) ? code : name.ToUpperInvariant();
    }

    private static readonly Lazy<Dictionary<string, string>> CountryCodes = new(() =>
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vietnam"] = "VN", ["Viet Nam"] = "VN", ["USA"] = "US", ["United States of America"] = "US",
            ["UK"] = "GB", ["England"] = "GB", ["South Korea"] = "KR", ["Korea"] = "KR", ["Taiwan"] = "TW"
        };
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                if (region.TwoLetterISORegionName.Length == 2) map.TryAdd(region.EnglishName, region.TwoLetterISORegionName);
            }
            catch (ArgumentException) { }
        }
        return map;
    });

    // ---------------- Shipping mark (1 nhãn / kiện, 2 nhãn / trang A4 ngang) ----------------

    private static string ShippingMarks(OrderPrintModel m, CompanyInfo c, DateTime printedAt)
    {
        var pieces = PieceList(m.Packages, m.Order.Pieces ?? 1);
        var marks = pieces.Select((p, i) => ShippingMark(m, c, printedAt, i + 1, pieces.Count, p)).ToList();
        var sb = new StringBuilder();
        for (var i = 0; i < marks.Count; i += 2)
        {
            // Trang lẻ cuối (hoặc đơn 1 kiện): nhãn nằm nửa phải như mẫu cũ.
            var pair = i + 1 < marks.Count ? marks[i] + marks[i + 1] : "<div></div>" + marks[i];
            sb.Append($"<section class=\"page sheet marks\">{pair}</section>");
        }
        return sb.ToString();
    }

    private static string ShippingMark(OrderPrintModel m, CompanyInfo c, DateTime printedAt, int index, int total, PrintPackage? piece)
    {
        var o = m.Order;
        var pieceNo = $"{m.Bill}/{index}";
        var gw = (piece?.WeightKg ?? 0).ToString("0.00", CultureInfo.InvariantCulture);
        var dim = piece is null ? "<b>0</b>*<b>0</b>*<b>0</b>" : $"<b>{Kg(piece.Length)}</b>*<b>{Kg(piece.Width)}</b>*<b>{Kg(piece.Height)}</b>";
        var cityLine = string.Join(", ", new[] { o.ConsigneeCity, o.ConsigneeState, o.ConsigneePostalCode, o.ConsigneeCountry }
            .Select(s => s?.Trim()).Where(s => !string.IsNullOrEmpty(s)));
        var address = string.Join(", ", new[] { o.ConsigneeAddress1, o.ConsigneeAddress2, o.ConsigneeAddress3 }
            .Select(s => s?.Trim()).Where(s => !string.IsNullOrEmpty(s)));

        return $"""
            <div class="mark">
              <div class="m-box">
                <div class="s-head m-head">
                  <img class="head-img" src="{HeaderDataUri.Value}" alt="Viet An Express · Tel {H(c.Phone)} · Hotline {H(c.Hotline)}">
                  <div class="awb">{Code128.Svg(pieceNo, 40)}<div class="awb-no">{H(m.Bill)} <small>/{index}</small></div><div class="route">{H(RouteCode(o.ServiceName))}</div></div>
                </div>
                <div class="m-title">SHIPPING MARK</div>
                <div class="m-hawb"><i>HAWB:</i> <b>{H(m.Bill)}</b></div>
                <div class="bar m-bar center"><i>Ref no.:</i> {H(o.CustomerBill)}</div>
                <div class="m-dest">
                  <div class="qr-frame">{Qr(m.Bill)}</div>
                  <div class="m-dest-txt"><div class="m-dest-lbl">DESTINATION</div><div class="m-dest-code">{H(CountryCode(o.ConsigneeCountry))}</div></div>
                </div>
                <div class="m-pcs"><i>Pcs no:</i> <b>{index} / {total}</b></div>
                <div class="bar m-bar"><i>Sender</i>:</div>
                <div class="m-party small">
                  <div><i>Co.</i>: {H(o.SenderName)}</div>
                  <div><i>Att</i>: {H(o.SenderContactName)}</div>
                </div>
                <div class="bar m-bar"><i>Consignee</i>:</div>
                <div class="m-party">
                  <div><i>Co.</i>: {H(o.ConsigneeName)}</div>
                  <div><i>Add</i>: {H(address)}</div>
                  <div>{H(cityLine)}</div>
                  <div><i>Att</i>: {H(o.ConsigneeContactName)} | <i>Tel</i>: {H(PhoneWithCode(o.ConsigneePhoneCode, o.ConsigneePhone))}</div>
                </div>
              </div>
              <div class="m-foot">
                <div class="foot-awb">{Code128.Svg(pieceNo, 30)}<div>{H(pieceNo)}</div></div>
                <div>G.W: <b class="m-big">{gw}kg</b> &nbsp; DIM: {dim} cm</div>
              </div>
              <div class="m-date">{Date(printedAt)}</div>
            </div>
            """;
    }

    /// <summary>
    /// Mã tuyến in dưới số bill, vd "Chuyên tuyến|Singapore" → "CT-Sin", "DHL|Singapore" → "DHL-Sin".
    /// Hãng nhiều chữ → viết tắt chữ cái đầu; tuyến → 3 ký tự đầu.
    /// </summary>
    public static string RouteCode(string? serviceName)
    {
        var parts = (serviceName ?? "").Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "";
        var words = parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var carrier = words.Length > 1
            ? string.Concat(words.Select(w => char.ToUpperInvariant(w[0])))
            : parts[0];
        if (parts.Length == 1) return carrier;
        var hub = parts[1].Replace(" ", "");
        return $"{carrier}-{hub[..Math.Min(3, hub.Length)]}";
    }

    /// <summary>Số điện thoại người nhận kèm mã nước, vd ("65", "65 453778") → "+65 65 453778". Số đã có "+" thì giữ nguyên.</summary>
    public static string PhoneWithCode(string? code, string? phone)
    {
        var p = phone?.Trim() ?? "";
        var digits = new string((code ?? "").Where(char.IsAsciiDigit).ToArray());
        if (p.Length == 0 || digits.Length == 0 || p.StartsWith('+')) return p;
        return $"+{digits} {p}";
    }

    /// <summary>Địa chỉ người gửi tách 2 dòng: theo xuống dòng nếu có, không thì cắt ở dấu phẩy gần giữa khi quá dài.</summary>
    public static (string Line1, string Line2) SplitAddress(string? address, int maxLine = 48)
    {
        var a = (address ?? "").Trim();
        var byLine = a.Split('\n', 2, StringSplitOptions.TrimEntries);
        if (byLine.Length == 2) return (byLine[0], byLine[1].Replace('\n', ' '));
        if (a.Length <= maxLine) return (a, "");
        var cut = a.LastIndexOf(',', Math.Min(a.Length - 1, maxLine));
        if (cut <= 0) cut = a.LastIndexOf(' ', maxLine);
        return cut <= 0 ? (a, "") : (a[..cut].Trim(), a[(cut + 1)..].Trim());
    }

    // ---------------- 1 liên ----------------

    private static string SlipHtml(OrderPrintModel m, CompanyInfo c, Slip slip, DateTime printedAt)
    {
        var o = m.Order;
        var bill = H(m.Bill);
        var (addr1, addr2) = SplitAddress(o.SenderAddress);

        var signs = slip == Slip.Destination
            ? $"""
              <div class="sign"><div>Nhân Viên Giao hàng <i>(Delivered by)</i></div><div class="sign-date"><b>Ngày, giờ</b> <i>(Date /time)</i>:</div></div>
              <div class="sign"><div>Chữ ký người nhận <i>(Signed By Receiver)</i></div><div class="sign-date"><b>Ngày, giờ</b> <i>(Date /time)</i>:<br><i>NV kinh doanh</i> : </div></div>
              """
            : $"""
              <div class="sign"><div>Chữ ký người gửi <i>(Sender's Signature)</i></div><div class="sign-date"><b>Ngày, giờ</b> <i>(Date /time)</i>: {SenderDateTime(o.CreateDate, printedAt)}</div></div>
              <div class="sign"><div>Nhân viên nhận hàng <i>(Picked up by)</i></div><div class="sign-date"><b>Ngày, giờ</b> <i>(Date /time)</i>:<br><i>NV kinh doanh</i> : </div></div>
              """;

        var slipName = slip switch
        {
            Slip.Shipper => "Liên 1: người gửi lưu <i>(Slip 1: Saved By Shipper)</i>",
            Slip.Origin => "Liên 2: lưu bưu cục gốc <i>(Slip 2: Saved by original Post Office)</i>",
            _ => "Liên 3: lưu bưu cục Phát <i>(Slip 3: Saved by destination Post Office)</i>"
        };
        var total = slip == Slip.Destination ? "" : "<div class=\"total\">Tổng cước:</div>";

        return $"""
            <div class="slip">
              <div class="box">
                <div class="s-head">
                  <img class="head-img" src="{HeaderDataUri.Value}" alt="Viet An Express · Tel {H(c.Phone)} · Hotline {H(c.Hotline)}">
                  <div class="awb">{Code128.Svg(m.Bill, 40)}<div class="awb-code">{H(m.Bill)}</div><div class="route">{H(RouteCode(o.ServiceName))}</div></div>
                </div>

                <div class="bar">1. Thông tin người gửi <i>(Sender's information)</i>:</div>
                <div class="sender">
                  <div class="qr">
                    <div class="qr-frame">{Qr(m.Bill)}</div>
                    <a class="qr-cap" href="{H(TrackingUrl(c, m.Bill))}" target="_blank" rel="noopener noreferrer">{bill}</a>
                  </div>
                  <div class="lines">
                    <div class="ln">{H(addr1)}</div>
                    <div class="ln">{H(addr2)}</div>
                    <div class="ln split"><span>{m.SenderCountryText}</span><span class="lbl">Postal code:</span><span class="ln-in">{H(o.SenderPostalCode)}</span></div>
                    <div class="ln">{H(o.SenderName)}</div>
                    <div class="ln">{H(o.SenderPhone)}</div>
                    <div class="ln">{H(o.SenderContactName)}</div>
                  </div>
                </div>

                <div class="bar">2. Thông tin người nhận(Consignee's information):</div>
                <div class="rows cnee">
                  {Row("C.Ty <i>(Company)</i>:", o.ConsigneeName)}
                  {Row("Địa chỉ <i>(Address)</i>:", o.ConsigneeAddress1)}
                  {Row("", o.ConsigneeAddress2)}
                  {Row("", o.ConsigneeAddress3)}
                  {Row2("Thành phố (City):", o.ConsigneeCity, "Tỉnh (State):", o.ConsigneeState)}
                  {Row2("<b>Nước <i>(Country)</i></b>:", o.ConsigneeCountry, "Postal code:", o.ConsigneePostalCode)}
                  {Row("Người liên hệ <i>(Contact name)</i>:", o.ConsigneeContactName)}
                  {Row("ĐT (Tel):", PhoneWithCode(o.ConsigneePhoneCode, o.ConsigneePhone))}
                  {Row("Email:", o.ConsigneeEmail, underline: false)}
                </div>

                <div class="bar">3. Thông tin Đơn Hàng <i>(Shipment information)</i>:</div>
                <div class="rows ship">
                  {Row("Tên hàng (Content):", o.GoodsName, underline: false)}
                  {Row("Giá trị (value invoice):", ValueWithFee(o), underline: false)}
                </div>
                <div class="gap"></div>
                {Packages(m)}
                <div class="signs">{signs}</div>
              </div>
              <div class="s-foot">
                <div>{total}<div class="slip-name">{slipName}</div></div>
                <div class="foot-awb">{Code128.Svg(m.Bill, 30)}<div>{bill}</div></div>
              </div>
            </div>
            """;
    }

    private static string Packages(OrderPrintModel m)
    {
        var o = m.Order;
        var pk = m.Packages;
        string gross, vol;
        var dims = new List<string>();
        if (pk.Count > 0)
        {
            gross = Kg1(pk.Sum(p => p.GrossKg));
            // Không khai kích thước → TL quy đổi 0 (như hệ thống cũ), ô kích thước để trống.
            vol = Kg1(pk.Sum(BillVolumeKg));
            // Kiện không khai D×R×C → không in "1*(0*0*0)".
            var measured = pk.Where(p => p.Length * p.Width * p.Height > 0).ToList();
            var shown = measured.Count > DimSlots ? measured.Take(DimSlots - 1) : measured;
            // Như hệ thống cũ: "TL thực | SL*(D*R*C)=TL quy đổi", vd "19.5 | 1*(47*37*32)=11.5".
            dims.AddRange(shown.Select(p =>
                $"{H(Kg1(p.GrossKg))} | {p.Qty}*({Kg(p.Length)}*{Kg(p.Width)}*{Kg(p.Height)})=<b>{H(Kg1(BillVolumeKg(p)))}</b>"));
            if (measured.Count > DimSlots) dims.Add($"… và {measured.Count - (DimSlots - 1)} dòng kiện khác");
        }
        else
        {
            gross = o.WeightKg is > 0 ? Kg1(o.WeightKg) : "";
            vol = "";
        }
        while (dims.Count < DimSlots) dims.Add("");

        var pieces = m.TotalPieces;
        return $"""
            <table class="pkg">
              <tr>
                <th class="c-pcs">Số kiện<br><i>(PCS)</i></th>
                <th class="c-w">TL thực<br><i>(G.W)</i></th>
                <th class="c-w">TL Qui Đổi<br><i>(Vol.W)</i></th>
                <th rowspan="2" class="dims"><div class="dims-title">Kích thước (Dimensions)</div><div class="dim-grid">{string.Concat(dims.Select(d => $"<div class=\"dim\">{d}</div>"))}</div></th>
              </tr>
              <tr>
                <td class="pcs">{pieces}</td>
                <td class="w">{H(gross)}</td>
                <td class="w">{H(vol)}</td>
              </tr>
            </table>
            """;
    }

    private static string Row(string labelHtml, string? value, bool underline = true) =>
        $"<div class=\"row\"><div class=\"lbl\">{labelHtml}</div><div class=\"val{(underline ? " ln" : "")}\">{H(value)}</div></div>";

    private static string Row2(string labelHtml, string? value, string label2Html, string? value2) =>
        $"<div class=\"row\"><div class=\"lbl\">{labelHtml}</div><div class=\"val split\"><span class=\"ln ln-main\">{H(value)}</span>" +
        $"<span class=\"lbl\">{label2Html}</span><span class=\"ln ln-in\">{H(value2)}</span></div></div>";

    /// <summary>Giá trị invoice kèm shipping fee khách khai (nếu có): "40.00 USD · Shipping fee: 5.00 USD".</summary>
    internal static string ValueWithFee(LegacyOrder o)
    {
        if (o.ShippingFee is not > 0) return Value(o);
        var fee = $"Shipping fee: {o.ShippingFee.Value.ToString("0.00", CultureInfo.InvariantCulture)} {o.Currency?.Trim()}".Trim();
        var value = Value(o);
        return value.Length == 0 ? fee : $"{value} · {fee}";
    }

    internal static string Value(LegacyOrder o) =>
        o.GoodsValue is null ? "" : $"{(o.GoodsValue ?? 0).ToString("0.00", CultureInfo.InvariantCulture)} {o.Currency?.Trim()}".Trim();

    internal static string TrackingUrl(CompanyInfo c, string bill) =>
        $"{c.PortalUrl.TrimEnd('/')}/tracking/{Uri.EscapeDataString(bill)}";

    /// <summary>CMS nhận số bill thuần như mã vạch; không đưa URL hoặc tiền tố vào QR chứng từ.</summary>
    internal static string Qr(string bill)
    {
        var code = bill.Trim();
        if (code.Length == 0 || !code.All(char.IsAsciiDigit))
            throw new ArgumentException("QR chứng từ phải chứa số bill thuần", nameof(bill));
        // Dữ liệu số ngắn giữ QR thưa, ô đen lớn; mức H giúp đọc khi bản in bị mờ/xước.
        using var data = QRCodeGenerator.GenerateQrCode(code, QRCodeGenerator.ECCLevel.H);
        // Giữ vùng trắng 4 module, không cắt sát ô đen; SVG giữ nét vuông khi in PDF/giấy.
        return new SvgQRCode(data).GetGraphic(8, "#000000", "#ffffff", true, SvgQRCode.SizingMode.ViewBoxAttribute);
    }

    /// <summary>
    /// Hình đầu chứng từ mặc định (logo + Viet An Express + Tel / Hotline / Website, người dùng cung cấp 26/10/2026)
    /// nhúng trong assembly → data URI, để trang in không phụ thuộc đường dẫn web. Đổi số điện thoại thì thay hình Assets/header.webp.
    /// </summary>
    internal static readonly Lazy<string> HeaderDataUri = new(() =>
    {
        using var s = typeof(BillA4).Assembly.GetManifestResourceStream("VietAnExpress.Shipments.header.webp");
        if (s is null) return "";
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return "data:image/webp;base64," + Convert.ToBase64String(ms.ToArray());
    });

    private static string H(string? value) => WebUtility.HtmlEncode(value?.Trim() ?? "");

    /// <summary>
    /// Ngày, giờ ở ô chữ ký người gửi: ngày tạo đơn + giờ in bill (giờ VN), vd "23/10/2026 14:20" — in lại khác ngày cũng chỉ lấy giờ in.
    /// Chưa có ngày tạo đơn thì dùng ngày in. Hàm thuần — có test.
    /// </summary>
    public static string SenderDateTime(DateTime? created, DateTime printedAt) =>
        $"{Date(created ?? printedAt)} {printedAt.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    private static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";

    /// <summary>Ô kích thước: 4 hàng × 2 ô như mẫu hệ thống cũ.</summary>
    private const int DimSlots = 8;

    /// <summary>TL quy đổi in trên bill: SL × D×R×C / 5000, làm tròn LÊN 0.5 kg như hệ thống cũ (11.13 → 11.5, 10.89 → 11.0); số đã lưu thì giữ nguyên.</summary>
    public static decimal BillVolumeKg(PrintPackage p) =>
        p.StoredVolumeKg ?? Math.Ceiling(p.Qty * p.Length * p.Width * p.Height / VolumeDivisor * 2) / 2;

    /// <summary>Cân in trên bill: luôn 1 chữ số thập phân như hệ thống cũ (33.0, 19.5).</summary>
    private static string Kg1(decimal? value) => (value ?? 0).ToString("0.0", CultureInfo.InvariantCulture);

    private static string Kg(decimal? value) => (value ?? 0).ToString("0.##", CultureInfo.InvariantCulture);

    // ---------------- Điều khoản dịch vụ (chép theo mẫu bill hệ thống cũ) ----------------

    private const string Terms = """
        <div class="terms-col">
          <div class="t-title">ĐIỀU KHOẢN DỊCH VỤ:</div>
          <div class="t-h">1: Việt An Express không nhận gửi các hàng hóa như sau:</div>
          <p>- Các loại hàng hóa, tài liệu vi phạm quy định của Nhà nước về hàng cấm gửi, cấm lưu thông, cấm xuất khẩu.</p>
          <p>- Hàng hóa có thể gây nguy hại cho người khai thác và phương tiện vận chuyển.</p>
          <p>- Các loại vật phẩm hàng hóa nước nhận cấm nhập khẩu.</p>
          <div class="t-h">2. Quyền và nghĩa vụ của người gửi:</div>
          <p>- Người gửi có quyền khiếu nại và yêu cầu công ty bồi thường thiệt hại vật chất theo mức Công ty đã công bố. Đối với các lô hàng không mua bảo hiểm:</p>
          <p class="sub">+ Việt An Express bồi thường tối đa đối với tài liệu chuyển phát trong nước là: 200.000đ/ vận đơn.</p>
          <p class="sub">+ Việt An Express bồi thường tối đa đối với tài liệu chuyển phát quốc tế là: 1.500.000đ/ vận đơn.</p>
          <p class="sub">+ Việt An Express đền bù đối với hàng hóa trong nước là 4 lần cước/vận đơn (đã bao gồm phí gửi gốc).</p>
          <p class="sub">+ Việt An Express đền bù đối với hàng hóa quốc tế căn cứ theo dịch vụ chuyển phát quốc tế kết nối.</p>
          <p>- Cung cấp mọi chứng từ đi kèm với hàng gửi, đảm bảo người nhận có chức năng nhập khẩu tại nước đến.</p>
          <p>- Gói bọc đảm bảo an toàn cho hàng hóa.</p>
          <p>- Thông báo cho Việt An Express biết tính chất của hàng hóa. Thanh toán các chi phí phát sinh trong quá trình vận chuyển.</p>
          <div class="t-h">3. Trách nhiệm của Việt An Express:</div>
          <p>- Kiểm tra tính hợp pháp của hàng gửi. Trong trường hợp có dấu hiện vi phạm pháp luật, Việt An Express sẽ từ chối phục vụ.</p>
          <p>- Việt An Express có trách nhiệm đảm bảo an toàn bưu gửi kể từ khi nhận gửi đến khi phát hàng cho người có quyền nhận.</p>
          <p>- Việt an Express chịu trách nhiệm bồi thường trong trường hợp có thiệt hại xảy ra theo mức đã công bố.</p>
          <p>- Việt An Express không hoàn lại cước trong các trường hợp sau:</p>
          <p class="sub">+ Chuyển phát chậm trong các trường hợp bất khả kháng như: Động đất, chiến sự, bão lụt, chậm trễ do thủ tục pháp luật tại nước sở tại chưa hoàn thành, hãng vận chuyển quá tải...</p>
          <p class="sub">+ Việt An Express không phát được do lỗi bởi người gửi hoặc người nhận từ chối nhận;</p>
          <p>- Việt An Express không chịu trách nhiệm bồi thường trong các trường hợp:</p>
          <p class="sub">+ Hàng gửi bị các cơ quan Nhà nước có thẩm quyền thu giữ hoặc gửi ra nước ngoài bị tịch thu, tiêu hủy, trả lại theo điều lệ của nước nhận.</p>
          <p class="sub">+ Hàng hóa,vật phẩm bị hủy hoại do đặc tính của tự nhiên.</p>
          <p class="sub">+ Các thiệt hại gián tiếp hoặc những nguồn lợi không thực hiện do việc mất, chậm trễ, hư hỏng…</p>
          <p class="sub">+ Khiếu nại quá thời hiệu quá thời hiệu quy định.</p>
          <div class="t-h">4. Lưu ý:</div>
          <p>- Không để tiền, các chất nguy hiểm vào hàng gửi. Người gửi khai báo đầy đủ, rõ ràng các nội dung trên và đảm bảo tính chính xác của thông tin, tính hợp pháp của hàng hóa.</p>
          <p>- Thời hạn khiếu kiện trong vòng 60 ngày kể từ ngày gửi.</p>
          <p>- Hàng hóa chuyển hoàn theo yêu cầu của khách hàng, do lỗi của người gửi, do địa chỉ người nhận không đúng, do không có người nhận, do quá thời gian lưu giữ theo quy định của hãng kết nối hoặc do người nhận không đủ điều kiện nhập khẩu tại nước đến sẽ thu thêm phí bằng cước chính đối với bưu gửi trong nước và bằng hai lần cước chính đối với bưu gửi quốc tế.</p>
          <p>- Khách hàng cần xuất hóa đơn thanh toán ngay cần đưa yêu cầu xuất hóa đơn cho Việt An Express trong vòng 20 ngày kể từ ngày gửi hàng. Sau 20 ngày Việt An Express sẽ kê khai vận đơn với các cơ quan thuế dưới hình thức “Người mua không lấy hóa đơn” và từ chối xuất hóa đơn cho khách.</p>
          <p>- Khi sử dụng dịch vụ đồng nghĩa với việc khách hàng đã đồng ý các điều khoản nêu trên.</p>
        </div>
        """;

    // ---------------- CSS riêng cho bill (A4 ngang) ----------------

    public const string Css = """
        @page { size: A4 landscape; margin: 7mm; }
        * { box-sizing: border-box; }
        /* Aptos (font Body của Office) cho mọi chứng từ + cỡ chữ thống nhất (chữ thường 12px, chữ nhỏ 11px); máy chưa có Aptos thì dùng Arial. */
        body { margin: 0; font-family: Aptos, "Aptos Body", Arial, Helvetica, sans-serif; font-size: 12px; color: #000; background: #e9ece9;
               -webkit-print-color-adjust: exact; print-color-adjust: exact; text-rendering: geometricPrecision; }
        .toolbar { position: sticky; top: 0; display: flex; gap: 12px; align-items: center; padding: 10px 16px; background: #1e6b2c; color: #fff; z-index: 1; font-family: Aptos, "Aptos Body", Arial, Helvetica, sans-serif; font-size: 12px; }
        .toolbar button { padding: 6px 18px; border: 0; border-radius: 6px; background: #fff; color: #1e6b2c; font-weight: 700; cursor: pointer; }
        .toolbar .hint { opacity: .85; }
        .sheet { width: 297mm; height: 210mm; padding: 7mm; margin: 16px auto; background: #fff; box-shadow: 0 2px 10px rgba(0,0,0,.15);
                 display: grid; grid-template-columns: 1fr 1fr; gap: 9mm; overflow: hidden; break-after: page; page-break-after: always; }
        .sheet:last-of-type { break-after: auto; page-break-after: auto; }
        .slip { display: flex; flex-direction: column; min-width: 0; height: 100%; }
        .box { border: 1.5px solid #000; flex: 1; display: flex; flex-direction: column; min-height: 0; }
        .s-head { display: grid; grid-template-columns: 28mm 1fr 42mm; gap: 2mm; align-items: center; padding: 1.5mm 3mm; height: 27mm; }
        /* Hình đầu chiếm 2 cột trái (logo + tên + liên hệ), canh trái, giữ tỉ lệ. */
        .head-img { grid-column: 1 / 3; width: 100%; height: 24mm; object-fit: contain; object-position: left center; }
        .awb { text-align: center; font-family: Aptos, "Aptos Body", Arial, Helvetica, sans-serif; }
        .awb .barcode { display: block; width: 100%; height: 9mm; }
        /* Số bill (OrderNumber) dưới mã vạch: Aptos, to, chữ thường (không đậm), căn giữa */
        .awb-code { margin-top: .6mm; font-size: 22px; font-weight: 400; line-height: 1.05; white-space: nowrap; }
        .awb-no { margin-top: .6mm; font-size: 20px; font-weight: 400; line-height: 1.05; white-space: nowrap; }
        .awb-no small { font-size: 14px; }
        .route { font-size: 14px; font-weight: 700; line-height: 1.1; }
        .bar { background: #92d050; font-weight: 700; padding: .4mm 1.5mm; border-top: 1px solid #000; border-bottom: 1px solid #000; }
        .sender { display: grid; grid-template-columns: 50mm 1fr; padding: 1mm 3mm 0 0; }
        .qr { display: flex; flex-direction: column; align-items: center; justify-content: center; }
        /* Vùng trắng 4 module nằm sẵn trong SVG nên khung không cần lề trong: mã ~22 mm (trước ~16 mm). */
        .qr-frame { position: relative; width: 30mm; height: 30mm; padding: 0; background: #fff; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        .qr-frame::before, .qr-frame::after { content: ""; position: absolute; top: 0; bottom: 0; width: 4mm; border: 1.5px solid #000; }
        .qr-frame::before { left: 0; border-right: 0; }
        .qr-frame::after { right: 0; border-left: 0; }
        .qr-frame svg { display: block; width: 100%; height: 100%; shape-rendering: crispEdges; }
        .qr-cap { color: #000; font-size: 14px; font-weight: 700; text-decoration: none; margin-top: .5mm; }
        .lines { display: flex; flex-direction: column; }
        .ln { border-bottom: 1px dashed #000; min-height: 5mm; line-height: 5mm; font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .split { display: flex; gap: 2mm; align-items: flex-end; }
        .split > span:first-child { flex: 1; }
        .split .lbl { font-weight: 400; white-space: nowrap; border: 0; }
        .ln-in { min-width: 18mm; }
        .lines .split { border-bottom: 0; }
        .lines .split > span:first-child { border-bottom: 1px dashed #000; }
        .lines .split .ln-in { border-bottom: 1px dashed #000; }
        /* Mục 1 (người gửi): kẻ dòng chấm chấm. Mục 2 (người nhận): không kẻ dòng. */
        .lines .ln, .lines .split > span:first-child, .lines .split .ln-in { border-bottom-style: dotted; border-bottom-width: 1.5px; }
        .cnee .ln, .cnee .split > span, .cnee .ln-in { border-bottom: 0; }
        .rows { padding: .5mm 3mm 0 0; }
        .row { display: grid; grid-template-columns: 50mm 1fr; gap: 2mm; align-items: end; min-height: 4.6mm; }
        .row .lbl { text-align: right; white-space: nowrap; line-height: 4.6mm; }
        .row .val { min-height: 4.6mm; line-height: 4.6mm; font-weight: 700; min-width: 0; }
        .row .val:not(.ln) { border: 0; }
        .row .split { font-weight: 700; }
        .ln-main { flex: 1; }
        .ship .row { min-height: 7.5mm; border-bottom: 1px solid #000; margin-right: -3mm; padding-right: 3mm; }
        .ship .row .lbl, .ship .row .val { line-height: 7.5mm; }
        .gap { height: 3mm; border-bottom: 1.5px solid #000; }
        table.pkg { width: 100%; border-collapse: collapse; table-layout: fixed; }
        .pkg th, .pkg td { border: 1px solid #000; padding: .5mm 1mm; text-align: center; vertical-align: top; }
        .pkg tr:first-child > * { border-top: 0; }
        .pkg tr > *:first-child { border-left: 0; }
        .pkg tr > *:last-child { border-right: 0; }
        .c-pcs { width: 16mm; }
        .c-w { width: 19mm; }
        .pkg .dims { font-weight: 400; text-align: left; padding: .5mm 2mm; }
        .dims-title { text-align: center; font-weight: 700; }
        .dim-grid { display: grid; grid-template-columns: 1fr 1fr; }
        .dim { border-bottom: 1px dotted #000; min-height: 4.6mm; line-height: 4.6mm; padding-right: 2mm; font-size: 11px; white-space: nowrap; overflow: hidden; }
        .pkg td { height: 14mm; border-top: 0; vertical-align: middle; }
        .pkg .pcs { font-size: 20px; font-weight: 700; }
        /* Số kiện, TL thực, TL quy đổi cùng 1 cỡ. */
        .pkg .w { font-size: 20px; font-weight: 700; }
        .pkg tr:first-child th:not(.dims) { border-bottom: 0; }
        .signs { display: grid; grid-template-columns: 1fr 1fr; flex: 1; min-height: 16mm; border-top: 1px solid #000; }
        .sign { display: flex; flex-direction: column; justify-content: space-between; padding: 1mm 2mm; }
        .sign + .sign { border-left: 1px solid #000; }
        .sign-date { font-size: 11px; line-height: 1.2; }
        .s-foot { display: flex; flex: 0 0 16mm; justify-content: space-between; align-items: center; padding: 1.5mm 3mm 0; }
        .s-foot > div:first-child { flex: 1; min-width: 0; padding-right: 2mm; }
        .total { font-size: 16px; font-weight: 700; margin-bottom: 1.5mm; }
        .slip-name { font-size: 11px; }
        .foot-awb { flex: none; width: 42mm; text-align: center; font-family: Aptos, "Aptos Body", Arial, Helvetica, sans-serif; font-size: 18px; font-weight: 400; line-height: 1.1; }
        .foot-awb .barcode { display: block; width: 100%; height: 9mm; }
        .terms-col { font-size: 10.5px; line-height: 1.28; text-align: justify; overflow: hidden; }
        .terms-col p { margin: 0 0 .55mm; }
        .terms-col .sub { padding-left: 3.5mm; }
        .t-title, .t-h { font-weight: 700; margin: .6mm 0 .3mm; }
        .mark { display: flex; flex-direction: column; min-width: 0; }
        .m-box { border: 1.5px solid #000; }
        .m-head { border-bottom: 1.5px solid #000; }
        .m-title { text-align: center; font-size: 30px; font-weight: 700; padding: 1.5mm 0; border-bottom: 1.5px solid #000; }
        .m-hawb { text-align: center; padding: 1mm 0; font-size: 22px; }
        .m-hawb b { font-size: 44px; margin-left: 3mm; }
        .m-bar { font-size: 16px; font-weight: 700; padding: .5mm 3mm; }
        .m-bar.center { text-align: center; }
        /* 3 cột 1fr | auto | 1fr: mã nước và số kiện nằm đúng giữa shipping mark, thẳng với SHIPPING MARK / HAWB. */
        .m-dest { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; padding: 2mm 3mm; border-bottom: 1.5px solid #000; min-height: 38mm; }
        .m-dest .qr-frame { justify-self: start; margin-left: 3mm; width: 32mm; height: 32mm; }
        .m-dest-txt { text-align: center; }
        .m-dest-lbl { font-weight: 700; font-size: 12px; }
        .m-dest-code { font-size: 56px; font-weight: 700; line-height: 1.05; }
        .m-pcs { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; padding: 1mm 6mm; font-size: 22px; font-weight: 700; }
        .m-pcs b { font-size: 56px; line-height: 1; text-align: center; word-spacing: 2mm; }
        .m-party { padding: .8mm 3mm; font-weight: 700; font-size: 13px; line-height: 1.45; }
        .m-party.small { font-size: 11px; line-height: 1.3; }
        .m-foot { display: flex; justify-content: space-between; align-items: center; padding: 2mm 6mm 0; font-size: 13px; }
        .m-big { font-size: 18px; }
        .m-date { text-align: center; font-size: 30px; font-weight: 700; margin-top: 8mm; }
        @media print {
          body { background: #fff; }
          .toolbar { display: none; }
          .sheet { width: auto; height: 195mm; margin: 0; padding: 0; box-shadow: none; }
        }
        """;
}
