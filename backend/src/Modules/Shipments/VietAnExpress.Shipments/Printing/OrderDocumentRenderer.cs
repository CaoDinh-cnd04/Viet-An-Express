using System.Globalization;
using System.Net;
using System.Text;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Printing;

/// <summary>
/// Dựng trang HTML để in bill A4, invoice, công văn cam kết, nhãn A6. Hàm thuần — có test.
/// Mọi dữ liệu đơn đều được HtmlEncode (dữ liệu do khách nhập). Trang tự mở hộp thoại in khi tải xong.
/// </summary>
internal static class OrderDocumentRenderer
{
    /// <summary>
    /// In nhiều loại chứng từ 1 lần: 1 loại → trang in như cũ; từ 2 loại → trang gộp, mỗi loại 1 khung (iframe) giữ nguyên
    /// CSS / khổ giấy riêng và tự in lần lượt từng loại (mỗi loại 1 hộp thoại in vì khổ giấy khác nhau).
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

        var bills = string.Join(", ", orders.Select(o => o.Bill));
        return $$"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <title>Chứng từ {{H(bills)}}</title>
            <style>{{BundleCss}}</style>
            </head>
            <body>
            <div class="toolbar">
              <strong>Chứng từ</strong> · {{orders.Count}} đơn · {{docs.Count}} loại
              <button type="button" data-print-all>In lần lượt tất cả</button>
              {{buttons}}
              <span class="hint">Mỗi loại mở 1 hộp thoại in riêng — chọn đúng khổ giấy ghi ở từng mục, tắt "Headers and footers".</span>
            </div>
            {{sections}}
            <script>{{BundleScript}}</script>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// Script của trang in (1 loại): nút "In" + tự mở hộp thoại in. Viết 1 dòng, không dùng onclick inline — trang in được ghi vào portal
    /// (PrintPage document.write) nên chịu CSP của portal: script inline chỉ chạy khi mã băm nằm trong script-src (<see cref="ScriptHashes"/>).
    /// </summary>
    internal const string SingleScript =
        "document.querySelectorAll('[data-print]').forEach(function (b) { b.addEventListener('click', function () { window.print(); }); }); " +
        "window.addEventListener('load', function () { setTimeout(function () { window.focus(); window.print(); }, 300); });";

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
        "window.addEventListener('load', function () { frames.forEach(fit); setTimeout(printAll, 400); });";

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
            <head><meta charset="utf-8"><title>{{H(what)}} {{H(bills)}}</title><style>{{BundleCss}}</style></head>
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

        var bills = string.Join(", ", orders.Select(o => o.Bill));
        var toolbar = embed ? "" :
            $"<div class=\"toolbar\"><strong>{H(title)}</strong> · {orders.Count} đơn <button type=\"button\" data-print>In</button>" +
            $"<span class=\"hint\">Chọn khổ giấy {H(Paper(doc))}, tắt \"Headers and footers\" trong hộp thoại in.</span></div>";
        var autoPrint = embed ? "" :
            $"<script>{SingleScript}</script>";
        return $$"""
            <!doctype html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <title>{{H(title)}} {{H(bills)}}</title>
            <style>{{Css(doc)}}</style>
            </head>
            <body>
            {{toolbar}}
            {{pages}}
            {{autoPrint}}
            </body>
            </html>
            """;
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
        body { margin: 0; font-family: Aptos, "Aptos Body", Arial, Helvetica, sans-serif; font-size: 13px; color: #111; background: #e9ece9; }
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
        body { margin: 0; font-family: Aptos, "Aptos Body", Arial, Helvetica, sans-serif; font-size: 12px; color: #111; background: #e9ece9; }
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
