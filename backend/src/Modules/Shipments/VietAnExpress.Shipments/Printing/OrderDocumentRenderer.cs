using System.Globalization;
using System.Net;
using System.Text;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Printing;

/// <summary>
/// Dựng trang HTML để in bill A4, invoice, công văn cam kết, nhãn A6. Hàm thuần — có test.
/// Mọi dữ liệu đơn đều được HtmlEncode (dữ liệu do khách nhập). Trang hiện mẫu để xem trước, bấm nút "In" mới mở hộp thoại in (05/11/2026 — trước tự in khi tải xong).
/// </summary>
internal static class OrderDocumentRenderer
{
    /// <summary>
    /// In nhiều loại chứng từ 1 lần: 1 loại → trang in như cũ; từ 2 loại → trang gộp, mỗi loại 1 khung (iframe) giữ nguyên
    /// CSS / khổ giấy riêng; bấm "In lần lượt tất cả" để in từng loại (mỗi loại 1 hộp thoại in vì khổ giấy khác nhau).
    /// </summary>
    public static string RenderMany(IReadOnlyList<string> requested, IReadOnlyList<OrderPrintModel> orders, CompanyInfo company, DateTime printedAtVn)
    {
        // Đơn chứng từ (DOC) không có invoice: bỏ khỏi bản in invoice; loại nào không còn đơn thì bỏ luôn loại đó.
        var docs = requested.Where(d => For(d, orders).Count > 0).ToList();
        if (docs.Count == 0) return NothingToPrint(requested, orders);
        if (docs.Count == 1) return Render(docs[0], For(docs[0], orders), company, printedAtVn);

        var buttons = new StringBuilder();
        var sections = new StringBuilder();
        for (var i = 0; i < docs.Count; i++)
        {
            var html = Render(docs[i], For(docs[i], orders), company, printedAtVn, embed: true);
            buttons.Append(CultureInfo.InvariantCulture, $"<button type=\"button\" class=\"ghost\" data-print-doc=\"{i}\">In {H(Title(docs[i]))}</button>");
            sections.Append(CultureInfo.InvariantCulture,
                $"<section class=\"doc\"><h2>{H(Title(docs[i]))} <small>{H(Paper(docs[i]))}</small></h2><iframe title=\"{H(Title(docs[i]))}\" srcdoc=\"{Attr(html)}\"></iframe></section>");
        }

        return $$"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <title>{{H(FileTitle(orders))}}</title>
            <style>{{BundleCss}}</style>
            </head>
            <body>
            <div class="toolbar">
              <strong>Chứng từ</strong> · {{orders.Count}} đơn · {{docs.Count}} loại
              <button type="button" data-print-all>In lần lượt tất cả</button>
              {{buttons}}
              {{(docs.Contains(PrintDocs.LabelA6) ? SenderToggle + LabelA4Toggle : "")}}
              <span class="hint">Xem mẫu bên dưới rồi bấm In. Mỗi loại mở 1 hộp thoại in riêng — chọn đúng khổ giấy ghi ở từng mục, tắt "Headers and footers".</span>
            </div>
            {{sections}}
            <script>{{BundleScript}}</script>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// Script của trang in (1 loại): nút "In" (không tự in — khách xem mẫu trước). Viết 1 dòng, không dùng onclick inline — trang in được ghi vào portal
    /// (PrintPage document.write) nên chịu CSP của portal: script inline chỉ chạy khi mã băm nằm trong script-src (<see cref="ScriptHashes"/>).
    /// </summary>
    internal const string SingleScript =
        "document.querySelectorAll('[data-print]').forEach(function (b) { b.addEventListener('click', function () { window.print(); }); }); " +
        "document.querySelectorAll('[data-toggle-sender]').forEach(function (b) { b.addEventListener('click', function () { b.setAttribute('aria-pressed', document.body.classList.toggle('no-sender')); }); }); " +
        "function paper(a4) { document.body.classList.toggle('a4', a4); var s = document.getElementById('page-size'); if (s) s.textContent = a4 ? '" + LabelA6.A4PageRule + "' : ''; } " +
        "document.querySelectorAll('[data-print-size]').forEach(function (b) { b.addEventListener('click', function () { paper(b.getAttribute('data-print-size') === 'a4'); window.print(); }); });";

    /// <summary>
    /// Nút in nhãn A6 theo loại máy in (05/11/2026): máy in A6 / nhiệt → 1 nhãn đúng khổ 105 × 148 mm; giấy A4 ngang → 2 nhãn / tờ, giữ cỡ A6.
    /// </summary>
    private const string LabelPrintButtons =
        "<button type=\"button\" data-print-size=\"a6\">In máy A6</button>" +
        "<button type=\"button\" class=\"ghost\" data-print-size=\"a4\">In giấy A4 ngang (2 nhãn / tờ)</button>";

    /// <summary>Nút ẩn / hiện mục người gửi trên nhãn A6 (như nút Show/Hide Sender của hệ thống cũ) — bấm lại để hiện.</summary>
    private const string SenderToggle = "<button type=\"button\" class=\"ghost\" data-toggle-sender aria-pressed=\"false\">Ẩn / hiện người gửi</button>";

    /// <summary>Trang gộp: bật / tắt in nhãn A6 ra giấy A4 ngang (2 nhãn / tờ) — tắt = khổ A6 như máy in nhãn.</summary>
    private const string LabelA4Toggle = "<button type=\"button\" class=\"ghost\" data-label-a4 aria-pressed=\"false\">Nhãn A6: in giấy A4 ngang</button>";

    /// <summary>Script của trang gộp nhiều loại: khung tự giãn, nút in từng loại / in lần lượt (đóng hộp thoại loại này mới mở loại sau).</summary>
    internal const string BundleScript =
        "var frames = Array.prototype.slice.call(document.querySelectorAll('iframe')); " +
        "function fit(f) { try { f.style.height = f.contentDocument.documentElement.scrollHeight + 'px'; } catch (e) {} } " +
        "frames.forEach(function (f) { f.addEventListener('load', function () { fit(f); }); }); " +
        "function printDoc(i) { var w = frames[i].contentWindow; w.focus(); w.print(); } " +
        "function printAll() { var i = 0; (function next() { if (i >= frames.length) return; var w = frames[i++].contentWindow; " +
        "var done = function () { w.removeEventListener('afterprint', done); setTimeout(next, 300); }; w.addEventListener('afterprint', done); w.focus(); w.print(); })(); } " +
        "document.querySelectorAll('[data-print-doc]').forEach(function (b) { b.addEventListener('click', function () { printDoc(+b.getAttribute('data-print-doc')); }); }); " +
        "document.querySelectorAll('[data-print-all]').forEach(function (b) { b.addEventListener('click', printAll); }); " +
        "document.querySelectorAll('[data-toggle-sender]').forEach(function (b) { b.addEventListener('click', function () { var on = b.getAttribute('aria-pressed') !== 'true'; b.setAttribute('aria-pressed', on); " +
        "frames.forEach(function (f) { try { f.contentDocument.body.classList.toggle('no-sender', on); fit(f); } catch (e) {} }); }); }); " +
        "document.querySelectorAll('[data-label-a4]').forEach(function (b) { b.addEventListener('click', function () { var on = b.getAttribute('aria-pressed') !== 'true'; b.setAttribute('aria-pressed', on); " +
        "frames.forEach(function (f) { try { var d = f.contentDocument, s = d.getElementById('page-size'); if (!s) return; d.body.classList.toggle('a4', on); s.textContent = on ? '" + LabelA6.A4PageRule + "' : ''; fit(f); } catch (e) {} }); }); }); " +
        "window.addEventListener('load', function () { frames.forEach(fit); });";

    /// <summary>
    /// Mã băm CSP ('sha256-…') của 2 script trên — phải có trong script-src của frontend (backend/scripts/iis/frontend.web.config) và
    /// <c>SecurityHeaders.PortalCsp</c> của API. Sửa script thì test báo lệch → chép mã băm mới sang 2 chỗ đó.
    /// </summary>
    internal static IReadOnlyList<string> ScriptHashes { get; } =
        [CspHash(SingleScript), CspHash(BundleScript)];

    internal static string CspHash(string script) =>
        $"'sha256-{Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(script)))}'";

    /// <summary>Đơn in được cho loại chứng từ: invoice bỏ đơn chứng từ (DOC) — chứng từ không khai invoice. Hàm thuần — có test.</summary>
    public static IReadOnlyList<OrderPrintModel> For(string doc, IReadOnlyList<OrderPrintModel> orders) =>
        doc == PrintDocs.Invoice ? orders.Where(o => !LegacyDocumentRule.Matches(o.Order.GoodsName)).ToList() : orders;

    /// <summary>Chỉ chọn invoice cho đơn chứng từ → trang báo không có gì để in (không mở hộp thoại in trống).</summary>
    private static string NothingToPrint(IReadOnlyList<string> docs, IReadOnlyList<OrderPrintModel> orders)
    {
        var bills = string.Join(", ", orders.Select(o => o.Bill));
        var what = string.Join(", ", docs.Select(Title));
        return $$"""
            <!doctype html>
            <html lang="vi">
            <head><meta charset="utf-8"><title>{{H(FileTitle(orders))}}</title><style>{{BundleCss}}</style></head>
            <body>
            <div class="toolbar"><strong>{{H(what)}}</strong> · {{H(bills)}}</div>
            <p style="margin: 24px; font-size: 15px;">Đơn chứng từ (DOC) không có invoice — không có gì để in.</p>
            </body>
            </html>
            """;
    }

    /// <param name="embed">true = nằm trong trang gộp nhiều loại: bỏ thanh công cụ và lệnh tự in (trang gộp điều khiển việc in).</param>
    public static string Render(string doc, IReadOnlyList<OrderPrintModel> orders, CompanyInfo company, DateTime printedAtVn, bool embed = false)
    {
        var title = Title(doc);

        var pages = new StringBuilder();
        foreach (var o in orders)
        {
            pages.Append(doc switch
            {
                PrintDocs.BillA4 => BillA4.Render(o, company, printedAtVn),
                PrintDocs.Invoice => CommercialInvoice.Render(o),
                PrintDocs.Cvck => CommitmentLetter.Render(o, printedAtVn),
                _ => LabelA6.Render(o, company, printedAtVn)
            });
        }

        var label = doc == PrintDocs.LabelA6;
        var toolbar = embed ? "" :
            $"<div class=\"toolbar\"><strong>{H(title)}</strong> · {orders.Count} đơn " +
            (label ? LabelPrintButtons + SenderToggle : "<button type=\"button\" data-print>In</button>") +
            (label
                ? "<span class=\"hint\">Xem mẫu rồi bấm In. Máy in A6: chọn khổ A6 (105 × 148 mm). Giấy A4: chọn A4 ngang. Tắt \"Headers and footers\" trong hộp thoại in.</span></div>"
                : $"<span class=\"hint\">Xem mẫu rồi bấm In. Chọn khổ giấy {H(Paper(doc))}, tắt \"Headers and footers\" trong hộp thoại in.</span></div>");
        // Chỗ để nút "In giấy A4 ngang" ghi đè khổ giấy (@page) của nhãn — đặt sau CSS chính để thắng @page A6.
        var pageSize = label ? "<style id=\"page-size\"></style>" : "";
        var autoPrint = embed ? "" :
            $"<script>{SingleScript}</script>";
        // Bill A4 / nhãn A6: nút copy ảnh theo trang / liên — file tĩnh của portal (CSP script-src 'self', không cần mã băm),
        // có cả trong khung của trang gộp (srcdoc dùng địa chỉ trang cha). Xem web/public/print-copy.js.
        var copyImage = doc is PrintDocs.BillA4 or PrintDocs.LabelA6 ? $"<script src=\"{CopyImageScript}\"></script>" : "";
        return $$"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <title>{{H(FileTitle(orders))}}</title>
            <style>{{Css(doc)}}</style>
            {{pageSize}}
            </head>
            <body>
            {{toolbar}}
            {{pages}}
            {{autoPrint}}
            {{copyImage}}
            </body>
            </html>
            """;
    }

    /// <summary>Script copy ảnh bill / nhãn (web/public/print-copy.js) — đường dẫn tuyệt đối vì tab in nằm ở /print/:job.</summary>
    internal const string CopyImageScript = "/print-copy.js";

    /// <summary>Số bill tối đa ghi trong tên file; in nhiều hơn thì ghi "+n".</summary>
    private const int FileTitleBills = 3;

    /// <summary>
    /// Tiêu đề trang in = tên file gợi ý khi "Lưu thành PDF": "Việt An Express - bill online - 6010860"
    /// (nhiều đơn: "… - 6010860, 6010859, 6010858 +2"). Hàm thuần — có test.
    /// </summary>
    public static string FileTitle(IReadOnlyList<OrderPrintModel> orders)
    {
        var bills = orders.Select(o => o.Bill).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToList();
        var shown = string.Join(", ", bills.Take(FileTitleBills));
        var more = bills.Count > FileTitleBills ? $" +{bills.Count - FileTitleBills}" : "";
        return $"Việt An Express - bill online - {shown}{more}".TrimEnd(' ', '-');
    }

    private static string H(string? value) => WebUtility.HtmlEncode(value?.Trim() ?? "");

    /// <summary>Đặt cả trang HTML vào thuộc tính srcdoc: chỉ cần thoát &amp; và dấu ngoặc kép.</summary>
    private static string Attr(string html) => html.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Title(string doc) => doc switch
    {
        PrintDocs.BillA4 => "Vận đơn",
        PrintDocs.Invoice => "Commercial invoice",
        PrintDocs.Cvck => "Công văn cam kết",
        PrintDocs.LabelA6 => "Shipping mark",
        _ => throw new ArgumentOutOfRangeException(nameof(doc), doc, null)
    };

    private static string Paper(string doc) => doc switch
    {
        PrintDocs.LabelA6 => "A6 (105 × 148 mm)",
        PrintDocs.BillA4 => "A4 ngang (landscape)",
        _ => "A4"
    };

    /// <summary>Trang gộp: thanh công cụ + mỗi loại chứng từ 1 khung xem trước.</summary>
    private const string BundleCss = """
        * { box-sizing: border-box; }
        body { margin: 0; font-family: "Times New Roman", Times, serif; font-size: 13px; color: #111; background: #e9ece9; }
        .toolbar { position: sticky; top: 0; display: flex; flex-wrap: wrap; gap: 8px 12px; align-items: center; padding: 10px 16px; background: #1e6b2c; color: #fff; z-index: 1; }
        .toolbar button { padding: 6px 14px; border: 0; border-radius: 6px; background: #fff; color: #1e6b2c; font-weight: 700; cursor: pointer; }
        .toolbar button.ghost { background: transparent; color: #fff; border: 1px solid rgba(255,255,255,.6); font-weight: 600; }
        .toolbar .hint { opacity: .85; font-size: 12px; }
        .doc { margin: 16px auto; max-width: 1180px; padding: 0 16px; }
        .doc h2 { margin: 0 0 6px; font-size: 15px; }
        .doc h2 small { font-weight: 400; color: #555; font-size: 12px; }
        .doc iframe { display: block; width: 100%; height: 900px; border: 1px solid #c8cfc9; border-radius: 8px; background: #fff; }
        """;

    /// <summary>Bill A4 và nhãn A6 có CSS riêng; invoice / CVCK dùng khung trang A4 dọc chung + CSS riêng của từng mẫu.</summary>
    private static string Css(string doc) => doc switch
    {
        PrintDocs.BillA4 => BillA4.Css,
        PrintDocs.LabelA6 => LabelA6.Css,
        PrintDocs.Invoice => A4Css + CommercialInvoice.Css,
        _ => A4Css + CommitmentLetter.Css
    };

    private const string A4Css = """
        /* Lề in 2,54 cm 4 cạnh như Word cho invoice / CVCK (12 mm cũ sát mép giấy); khung xem trên màn hình cùng lề. */
        @page { size: A4; margin: 25.4mm; }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: "Times New Roman", Times, serif; font-size: 12px; color: #111; background: #e9ece9; }
        .toolbar { position: sticky; top: 0; display: flex; gap: 12px; align-items: center; padding: 10px 16px; background: #1e6b2c; color: #fff; z-index: 1; }
        .toolbar button { padding: 6px 18px; border: 0; border-radius: 6px; background: #fff; color: #1e6b2c; font-weight: 700; cursor: pointer; }
        .toolbar .hint { opacity: .85; font-size: 12px; }
        .page { background: #fff; margin: 16px auto; padding: 25.4mm; box-shadow: 0 2px 10px rgba(0,0,0,.15); page-break-after: always; break-after: page; }
        .page:last-of-type { page-break-after: auto; break-after: auto; }
        .a4 { width: 210mm; min-height: 297mm; }
        @media print {
          body { background: #fff; }
          .toolbar { display: none; }
          .page { margin: 0; box-shadow: none; }
          .a4 { width: auto; min-height: auto; padding: 0; }
        }
        """;
}
