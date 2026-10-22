using System.Globalization;
using System.Net;
using System.Text;
using VietAnExpress.Shipments.Infrastructure.Legacy;

namespace VietAnExpress.Shipments.Application.Orders.Documents;

/// <summary>Loại chứng từ in — khớp PRINT_DOCUMENTS của frontend (web/src/features/orders/constants.ts).</summary>
internal static class PrintDocs
{
    public const string BillA4 = "bill-a4";
    public const string Invoice = "invoice";
    public const string Cvck = "cvck";
    /// <summary>Nhãn A6: chỉ shipping mark (1 nhãn / kiện để dán thùng).</summary>
    public const string LabelA6 = "label-a6";

    public static readonly string[] All = [BillA4, Invoice, Cvck, LabelA6];

    /// <summary>"bill-a4,invoice" → danh sách loại (bỏ trùng, theo thứ tự chuẩn của <see cref="All"/>); loại lạ giữ lại để validator báo lỗi.</summary>
    public static IReadOnlyList<string> Parse(string? doc)
    {
        var picked = (doc ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        return [.. All.Where(picked.Contains), .. picked.Where(d => !All.Contains(d))];
    }
}

/// <summary>1 dòng hàng trên invoice / CVCK. <paramref name="Description"/> là tên tiếng Anh.</summary>
/// <paramref name="Package"/>: kiện số mấy khi đơn khai invoice riêng từng kiện; null = invoice chung.
internal sealed record PrintItem(string Description, decimal Quantity, string Unit, decimal UnitPrice, string? HsCode, string? Origin,
    string? DescriptionVi = null, string? Manufacturer = null, int? Package = null)
{
    public decimal Amount => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    /// <summary>"Tên tiếng Anh / Tên tiếng Việt" như mẫu hệ thống cũ; thiếu tên Việt thì chỉ tên Anh.</summary>
    public string Name(string separator = " / ") =>
        string.IsNullOrWhiteSpace(DescriptionVi) ? Description.Trim() : $"{Description.Trim()}{separator}{DescriptionVi.Trim()}";
}

/// <summary>
/// Dữ liệu 1 đơn để in: dòng dbo.MaVanDon + dòng hàng và dòng kiện (nếu đơn tạo trên portal còn giữ form).
/// </summary>
internal sealed record OrderPrintModel(LegacyOrder Order, IReadOnlyList<PrintItem> Items, IReadOnlyList<PrintPackage>? PackageLines = null)
{
    public IReadOnlyList<PrintPackage> Packages => PackageLines ?? [];

    public string Bill => LegacyOrderView.BillOf(Order);

    /// <summary>Tổng số kiện / cân thực: theo dòng kiện nếu có, không thì theo dbo.MaVanDon.</summary>
    public int TotalPieces => Packages.Count > 0 ? Packages.Sum(p => p.Qty) : Order.Pieces ?? 1;
    public decimal TotalGrossKg => Packages.Count > 0 ? Packages.Sum(p => p.GrossKg) : Order.WeightKg ?? 0;

    /// <summary>Nước người gửi in trên chứng từ — đơn nội địa (chưa khai hoặc mã nước 231) là "Viet Nam".</summary>
    public string SenderCountryText => Order.SenderCountryId is null or 231 ? "Viet Nam" : "";

    /// <summary>Đơn khai invoice riêng từng kiện (có dòng hàng mang số kiện) → in mỗi kiện 1 invoice.</summary>
    public bool InvoicePerPackage => Items.Any(i => i.Package is not null);

    /// <summary>Dòng kiện tách thành từng kiện (SL 3 → 3 kiện SL 1), theo thứ tự: kiện số n là phần tử thứ n - 1.</summary>
    public IReadOnlyList<PrintPackage> SinglePackages =>
        [.. Packages.SelectMany(p => Enumerable.Repeat(p with { Qty = 1, StoredVolumeKg = p.Qty > 0 && p.StoredVolumeKg is { } v ? v / p.Qty : null },
            Math.Max(0, p.Qty)))];

    /// <summary>Không có chi tiết hàng (đơn hệ thống cũ) → 1 dòng từ tên hàng, số kiện, giá trị tổng.</summary>
    public IReadOnlyList<PrintItem> InvoiceLines
    {
        get
        {
            if (Items.Count > 0) return Items;
            var qty = Math.Max(1, Order.Pieces ?? 1);
            var total = Order.GoodsValue ?? 0;
            return [new PrintItem(Order.GoodsName ?? "Goods", qty, "PCS", decimal.Round(total / qty, 2), null, "Vietnam")];
        }
    }
}

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
    public static string RenderMany(IReadOnlyList<string> docs, IReadOnlyList<OrderPrintModel> orders, CompanyInfo company, DateTime printedAtVn)
    {
        if (docs.Count == 1) return Render(docs[0], orders, company, printedAtVn);

        var buttons = new StringBuilder();
        var sections = new StringBuilder();
        for (var i = 0; i < docs.Count; i++)
        {
            var html = Render(docs[i], orders, company, printedAtVn, embed: true);
            buttons.Append(CultureInfo.InvariantCulture, $"<button type=\"button\" class=\"ghost\" onclick=\"printDoc({i})\">In {H(Title(docs[i]))}</button>");
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
              <button type="button" onclick="printAll()">In lần lượt tất cả</button>
              {{buttons}}
              <span class="hint">Mỗi loại mở 1 hộp thoại in riêng — chọn đúng khổ giấy ghi ở từng mục, tắt "Headers and footers".</span>
            </div>
            {{sections}}
            <script>
            var frames = Array.prototype.slice.call(document.querySelectorAll('iframe'));
            function fit(f) { try { f.style.height = f.contentDocument.documentElement.scrollHeight + 'px'; } catch (e) {} }
            frames.forEach(function (f) { f.addEventListener('load', function () { fit(f); }); });
            function printDoc(i) { var w = frames[i].contentWindow; w.focus(); w.print(); }
            // In lần lượt: đóng hộp thoại in loại này rồi mới mở loại kế tiếp.
            function printAll() {
              var i = 0;
              (function next() {
                if (i >= frames.length) return;
                var w = frames[i++].contentWindow;
                var done = function () { w.removeEventListener('afterprint', done); setTimeout(next, 300); };
                w.addEventListener('afterprint', done);
                w.focus();
                w.print();
              })();
            }
            window.addEventListener('load', function () { frames.forEach(fit); setTimeout(printAll, 400); });
            </script>
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
            $"<div class=\"toolbar\"><strong>{H(title)}</strong> · {orders.Count} đơn <button type=\"button\" onclick=\"window.print()\">In</button>" +
            $"<span class=\"hint\">Chọn khổ giấy {H(Paper(doc))}, tắt \"Headers and footers\" trong hộp thoại in.</span></div>";
        var autoPrint = embed ? "" :
            "<script>window.addEventListener('load', function () { setTimeout(function () { window.focus(); window.print(); }, 300); });</script>";
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
        PrintDocs.LabelA6 => "100 × 150 mm (A6)",
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
        @page { size: A4; margin: 12mm; }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: Aptos, "Aptos Body", Arial, Helvetica, sans-serif; font-size: 12px; color: #111; background: #e9ece9; }
        .toolbar { position: sticky; top: 0; display: flex; gap: 12px; align-items: center; padding: 10px 16px; background: #1e6b2c; color: #fff; z-index: 1; }
        .toolbar button { padding: 6px 18px; border: 0; border-radius: 6px; background: #fff; color: #1e6b2c; font-weight: 700; cursor: pointer; }
        .toolbar .hint { opacity: .85; font-size: 12px; }
        .page { background: #fff; margin: 16px auto; padding: 14mm; box-shadow: 0 2px 10px rgba(0,0,0,.15); page-break-after: always; break-after: page; }
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
