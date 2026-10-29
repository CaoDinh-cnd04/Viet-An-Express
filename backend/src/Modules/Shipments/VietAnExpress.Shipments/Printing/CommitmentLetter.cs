using System.Globalization;
using System.Text;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Printing;

/// <summary>Công văn cam kết nội dung hàng xuất — chép theo mẫu hệ thống cũ (Export CVCK.pdf).</summary>
internal static class CommitmentLetter
{
    private static readonly string[] Recipients =
    [
        "- Chi cục Hải Quan cửa khẩu Tân Sơn Nhất",
        "- Ban soi chiếu an ninh hàng không Tân Sơn Nhất",
        "- Công ty TNHH dịch vụ hàng hóa Tân Sơn Nhất (TCS)/ TECS).",
        "- Công ty TNHH dịch vụ hàng hóa Sài Gòn (SCSC)",
        "- Công ty TNHH DV Giao Nhận Quốc Tế Việt An"
    ];

    public static string Render(OrderPrintModel m, DateTime printedAt)
    {
        var o = m.Order;
        var date = o.CreateDate ?? printedAt;
        var gross = m.TotalGrossKg;
        var pieces = m.TotalPieces;
        var rows = new StringBuilder();
        var n = 0;
        foreach (var l in m.InvoiceLines)
        {
            var qty = $"{l.Quantity.ToString("0.00", CultureInfo.InvariantCulture)} {l.Unit}".Trim();
            rows.Append(CultureInfo.InvariantCulture, $"<tr><td class=\"c\">{++n}</td><td>{Doc.H(l.Name())}</td><td class=\"c\">{Doc.H(qty)}</td></tr>");
        }

        return $$"""
            <section class="page a4 cvck">
              <div class="c">CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
              <div class="c">Độc Lập – Tự Do – Hạnh Phúc</div>
              <div class="c">---***---</div>

              <div class="c title">CÔNG VĂN CAM KẾT NỘI DUNG HÀNG XUẤT</div>
              <div class="c subtitle">LETTER OF GUARANTEE</div>

              <div class="c date">TP.HCM Ngày {{date.Day}} tháng {{date.Month}} năm {{date.Year}}</div>

              <div class="to"><span>Kính gửi :</span><div>{{string.Concat(Recipients.Select(r => $"<div>{Doc.H(r)}</div>"))}}</div></div>

              <div class="block">
                <div>Chúng tôi là/ <i>We’re</i>: {{Doc.H(o.SenderName)}}</div>
                <div>MST / CMND số: {{Doc.H(o.SenderTax)}}</div>
                <div>Địa chỉ/ <i>Address</i> : {{Doc.H(o.SenderAddress)}}</div>
                <div>Có gửi đến/ <i>Shipment send to</i>: {{Doc.H(o.ConsigneeCountry)}}</div>
                <div>Số bill/ <i>Consignment note No.</i>: {{Doc.H(m.Bill)}}</div>
              </div>

              <div class="block">
                <div>Nội dung hàng gửi gồm/ <i>Content</i>:</div>
                <div class="indent">Số Kiện / <i>No pcs</i>: {{pieces}} Trọng lượng thực tế / <i>Grosweight</i>: {{gross.ToString("0.00", CultureInfo.InvariantCulture)}}</div>
                <div class="indent">Tên Hàng + số lượng/ <i>Descriptions of good + Quantity</i>:</div>
              </div>
              <table class="cv-goods">
                <tr><th class="stt">STT</th><th>TÊN HÀNG</th><th class="qty">SỐ LƯỢNG</th></tr>
                {{rows}}
              </table>

              <p>Chúng tôi xin cam kết lô hàng này không phải là hàng nguy hiểm, độc hại, không chứa xăng dầu, nhớt, khí gas, khí nén, từ tính, không phải hàng dễ cháy nổ theo phiên bản 55 của IATA DGR 2014, không có tiền chất ma tuý, không chứa ma tuý, không chất gây nghiện, không phải hàng quốc cấm, hàng cấm xuất khẩu. cam kết hàng đúng như khai báo; cam kết hàng vướng thủ tục hải quan nước đến không khiếu nại./ <i>We commit that the above shipment is not banned from export, it is non dangerous goods, no toxic, no flammable… according to IATA Regulations 55th 2014 and poses no risk to environment &amp; community.</i></p>

              <p>Vậy nên kính mong quý công ty, quý cơ quan tạo điều kiện cho lô hàng được xuất đi trong thời gian sớm nhất. Chúng tôi hoàn toàn chịu trách nhiệm về nội dung lô hàng xuất nói trên./ <i>I declare all the information contained in this letter to be true and correct.</i></p>

              <p>Trân trọng. <i>Best regards</i>,</p>
              <p><b>Giám đốc</b>/ <i>Director</i> (Ký, đóng dấu ghi rõ họ tên và sđt)</p>
            </section>
            """;
    }

    public const string Css = """
        .cvck { font-family: Aptos, "Aptos Body", Arial, Helvetica, sans-serif; font-size: 15px; line-height: 1.35; }
        .cvck .c { text-align: center; }
        .cvck .title { font-weight: 700; margin-top: 6mm; }
        .cvck .subtitle { font-weight: 700; font-style: italic; }
        .cvck .date { margin: 5mm 0; }
        .cvck .to { display: grid; grid-template-columns: 24mm 1fr; margin-bottom: 5mm; }
        .cvck .block { margin-bottom: 4mm; }
        .cvck .indent { padding-left: 4mm; }
        .cvck p { margin: 0 0 4mm; text-align: justify; }
        table.cv-goods { width: 100%; border-collapse: collapse; margin: 0 0 5mm; }
        .cv-goods th, .cv-goods td { border: 1px solid #000; padding: 0 2mm; }
        .cv-goods th { font-weight: 700; text-align: center; }
        .cv-goods .stt { width: 14mm; }
        .cv-goods .qty { width: 40mm; }
        .cv-goods td.c { text-align: center; }
        """;
}
