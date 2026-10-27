using ClosedXML.Excel;
using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Application.Orders.Documents;
using VietAnExpress.Shipments.Infrastructure.Legacy;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class OrderDocumentRendererTests
{
    [Fact]
    public void CMS_QR_uses_a_numeric_bill_and_keeps_the_full_quiet_zone()
    {
        var company = new CompanyInfo();
        const string bill = "6179767";
        var svg = BillA4.Qr(bill);
        var element = System.Xml.Linq.XElement.Parse(svg);
        using var data = QRCoder.QRCodeGenerator.GenerateQrCode(bill, QRCoder.QRCodeGenerator.ECCLevel.H);
        var fullSize = data.ModuleMatrix.Count;
        Assert.Equal($"0 0 {fullSize} {fullSize}", element.Attribute("viewBox")!.Value);
        Assert.Equal("crispEdges", element.Attribute("shape-rendering")!.Value);
        Assert.Equal(29, fullSize); // Version 1: 21 module dữ liệu + vùng trắng 4 module mỗi bên.
        Assert.All(data.ModuleMatrix.Take(4), row => Assert.All(row.Cast<bool>(), cell => Assert.False(cell)));
        Assert.Throws<ArgumentException>(() => BillA4.Qr(BillA4.TrackingUrl(company, bill)));
        Assert.Throws<ArgumentException>(() => BillA4.Qr("6179767/1"));
        var output = Environment.GetEnvironmentVariable("VIETAN_PRINT_PREVIEW_DIR");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            var model = new OrderPrintModel(Order(), [new PrintItem("METAL SIGN LOGO", 1, "PCS", 8, null, "Vietnam")], [new PrintPackage(1, 32, 54, 23, 8)]);
            File.WriteAllText(Path.Combine(output, "bill-a4.html"), Render(PrintDocs.BillA4, model));
            File.WriteAllText(Path.Combine(output, "label-a6.html"), Render(PrintDocs.LabelA6, model));
            File.WriteAllText(Path.Combine(output, "qr.svg"), BillA4.Qr(model.Bill));
            File.WriteAllText(Path.Combine(output, "qr-expected.txt"), model.Bill);
        }
    }

    [Theory]
    [InlineData(PrintDocs.BillA4)]
    [InlineData(PrintDocs.LabelA6)]
    public void All_printed_QRs_encode_the_same_bill_as_the_main_barcode(string doc)
    {
        var order = Order(pieces: 2);
        var html = Render(doc, new OrderPrintModel(order, []));
        var expected = BillA4.Qr(order.OrderNumber!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var groups = System.Text.RegularExpressions.Regex.Matches(html, "<div class=\"qr-frame\">(.*?)</div>", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.NotEmpty(groups);
        foreach (System.Text.RegularExpressions.Match group in groups) Assert.Equal(expected, group.Groups[1].Value);
    }

    private static readonly CompanyInfo Company = new();
    private static readonly DateTime Now = new(2026, 9, 29, 14, 30, 0);

    private static LegacyOrder Order(int pieces = 2, string consignee = "ACME LTD") => new()
    {
        Id = 1, OrderNumber = 90000001, BillConnect = "4681242285", CustomerBill = "PO-1",
        SenderName = "SCS CO., LTD", SenderPhone = "0909000000", SenderAddress = "14 Sam Sơn",
        ConsigneeName = consignee, ConsigneeCity = "LONDON", ConsigneeCountry = "United Kingdom", ConsigneeAddress1 = "1 King St",
        ServiceName = "DHL|Singapore", GoodsName = "Women dress", Pieces = pieces, WeightKg = 24, GoodsValue = 40, Currency = "USD",
        ExportReason = "GIFT", CreateDate = new DateTime(2026, 9, 29)
    };

    private static string Render(string doc, OrderPrintModel model) =>
        OrderDocumentRenderer.Render(doc, [model], Company, Now);

    [Fact]
    public void Ngay_gio_nguoi_gui_lay_gio_in_bill()
    {
        Assert.Equal("29/09/2026 14:30", BillA4.SenderDateTime(new DateTime(2026, 9, 29), Now));
        Assert.Equal("29/09/2026 14:30", BillA4.SenderDateTime(null, Now));
        Assert.Equal("25/09/2026 14:30", BillA4.SenderDateTime(new DateTime(2026, 9, 25), Now)); // in lại khác ngày: ngày tạo đơn + giờ in
    }

    [Theory]
    [InlineData(PrintDocs.BillA4)]
    [InlineData(PrintDocs.Invoice)]
    [InlineData(PrintDocs.Cvck)]
    [InlineData(PrintDocs.LabelA6)]
    public void Moi_chung_tu_co_so_van_don_ma_vach_va_tu_mo_hop_thoai_in(string doc)
    {
        var html = Render(doc, new OrderPrintModel(Order(), []));

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("90000001", html);
        Assert.Contains("window.print()", html);
        if (doc != PrintDocs.Cvck) Assert.Contains("<svg", html);
    }

    [Fact]
    public void Du_lieu_khach_nhap_duoc_escape_HTML()
    {
        var html = Render(PrintDocs.BillA4, new OrderPrintModel(Order(consignee: "<script>alert(1)</script>"), []));

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
    }

    [Fact]
    public void Bill_A4_ngang_moi_don_2_trang_3_lien_va_dieu_khoan()
    {
        var o = Order();
        o.ConsigneePhoneCode = "44";
        o.ConsigneePhone = "20 7946 0000";
        var packages = new[] { new PrintPackage(2, 50, 40, 30, 5) };
        var html = Render(PrintDocs.BillA4, new OrderPrintModel(o, [], packages));

        Assert.Contains("size: A4 landscape", html);
        Assert.Equal(2, html.Split("class=\"page sheet\"").Length - 1);
        Assert.Equal(1, html.Split("class=\"page sheet marks\"").Length - 1); // 2 kiện → 2 nhãn trên 1 trang
        Assert.Contains("SHIPPING MARK", html);
        Assert.Contains("90000001/2", html);
        Assert.Contains(">GB<", html);
        Assert.Contains("29/09/2026 14:30", html); // ô chữ ký người gửi: ngày + giờ in
        Assert.Contains("Liên 1: người gửi lưu", html);
        Assert.Contains("Liên 2: lưu bưu cục gốc", html);
        Assert.Contains("Liên 3: lưu bưu cục Phát", html);
        Assert.Contains("ĐIỀU KHOẢN DỊCH VỤ", html);
        Assert.Contains("DHL-Sin", html);
        Assert.Contains("+44 20 7946 0000", html);
        Assert.Contains("40.00 USD", html);
        Assert.Contains("10.0 | 2*(50*40*30)=<b>24.0</b>", html); // TL thực | SL*(D*R*C)=TL quy đổi như hệ thống cũ
        Assert.Contains(">10.0<", html);  // G.W = 2 × 5
        Assert.Contains(">24.0<", html);  // Vol.W = 2 × 50×40×30 / 5000
        Assert.Contains("data:image/webp;base64,", html);
        Assert.Contains("viewBox", html); // QR tracking
    }

    [Fact]
    public void Bill_A4_kich_thuoc_lam_tron_len_nua_kg_nhu_he_thong_cu()
    {
        // Mẫu hệ thống cũ: 19.5 | 1*(47*37*32)=11.5 · 13.5 | 1*(46*37*32)=11.0 → G.W 33.0, Vol.W 22.5.
        var html = Render(PrintDocs.BillA4, new OrderPrintModel(Order(), [],
            [new PrintPackage(1, 47, 37, 32, 19.5m), new PrintPackage(1, 46, 37, 32, 13.5m)]));

        Assert.Contains("19.5 | 1*(47*37*32)=<b>11.5</b>", html);
        Assert.Contains("13.5 | 1*(46*37*32)=<b>11.0</b>", html);
        Assert.Contains(">33.0<", html);
        Assert.Contains(">22.5<", html);
        Assert.Equal(11.5m, BillA4.BillVolumeKg(new PrintPackage(1, 47, 37, 32, 0)));
        Assert.Equal(19.5m, BillA4.BillVolumeKg(new PrintPackage(1, 60, 43, 37, 0))); // 19.09 → 19.5
    }

    [Theory]
    [InlineData("Singapore", "SG")]
    [InlineData("United Kingdom", "GB")]
    [InlineData("Vietnam", "VN")]
    [InlineData("us", "US")]
    [InlineData("Atlantis", "ATLANTIS")]
    public void Ma_nuoc_den_tren_shipping_mark(string country, string expected) =>
        Assert.Equal(expected, BillA4.CountryCode(country));

    [Fact]
    public void Moi_kien_mot_shipping_mark()
    {
        var list = BillA4.PieceList([new PrintPackage(2, 40, 30, 20, 4), new PrintPackage(1, 10, 10, 10, 1)], 0);
        Assert.Equal(3, list.Count);
        Assert.Equal(4, list[1]!.WeightKg);
        Assert.Equal(1, list[2]!.WeightKg);
        Assert.Equal(3, BillA4.PieceList([], 3).Count(p => p is null));
    }

    [Theory]
    [InlineData("Chuyên tuyến|Singapore", "CT-Sin")]
    [InlineData("DHL|Singapore", "DHL-Sin")]
    [InlineData("UPS", "UPS")]
    [InlineData(null, "")]
    public void Ma_tuyen_in_duoi_so_bill(string? service, string expected) =>
        Assert.Equal(expected, BillA4.RouteCode(service));

    [Theory]
    [InlineData("65", "65 453778", "+65 65 453778")]
    [InlineData("+1", "+1 555 0100", "+1 555 0100")]
    [InlineData("", "0909", "0909")]
    public void So_dien_thoai_nguoi_nhan_kem_ma_nuoc(string code, string phone, string expected) =>
        Assert.Equal(expected, BillA4.PhoneWithCode(code, phone));

    [Fact]
    public void Dia_chi_nguoi_gui_dai_tach_2_dong_o_dau_phay()
    {
        var (l1, l2) = BillA4.SplitAddress("14 Sâm Sơn, Phường 4, Quận Tân Bình, TP. Hồ Chí Minh, Việt Nam");
        Assert.Equal("14 Sâm Sơn, Phường 4, Quận Tân Bình", l1);
        Assert.Equal("TP. Hồ Chí Minh, Việt Nam", l2);
        Assert.Equal(("Ngắn", ""), BillA4.SplitAddress("Ngắn"));
    }

    [Fact]
    public void Nhan_A6_chi_in_shipping_mark_moi_kien_mot_nhan()
    {
        var html = System.Net.WebUtility.HtmlDecode(Render(PrintDocs.LabelA6, new OrderPrintModel(Order(pieces: 3), [])));

        Assert.Equal(3, html.Split("class=\"page a6 mark\"").Length - 1); // 1 shipping mark / kiện
        Assert.DoesNotContain("Liên 1", html);
        Assert.DoesNotContain("Liên 3", html);
        Assert.Contains("1 / 3", html);
        Assert.Contains("3 / 3", html);
        Assert.Contains("90000001/3", html);
        Assert.Contains("DHL-Singapore", html);
        Assert.Contains("size: 105mm 148mm", html);
    }

    [Fact]
    public void Invoice_in_ten_Anh_Viet_nha_san_xuat_va_xuat_xu()
    {
        var items = new[] { new PrintItem("Dried Mango", 2, "Bag", 5, "0804", "Vietnam", "Xoài sấy dẻo", "Vinamit") };
        var html = System.Net.WebUtility.HtmlDecode(Render(PrintDocs.Invoice, new OrderPrintModel(Order(), items)));

        Assert.Contains("Dried Mango/ Xoài sấy dẻo", html);
        Assert.Contains("Vinamit - Country of origin: Vietnam", html);
        Assert.Contains("SHIPPER", html);
        Assert.Contains("CONSIGNEE", html);
        Assert.Contains("Full Description of Goods", html);
    }

    [Fact]
    public void Invoice_theo_mau_he_thong_cu_in_3_ban_kem_ngay_va_cam_ket()
    {
        var items = new[] { new PrintItem("Cinnamon Sticks", 1, "Bag", 2.9m, "0906190000", "Vietnam", "Quế") };
        var html = System.Net.WebUtility.HtmlDecode(Render(PrintDocs.Invoice, new OrderPrintModel(Order(), items)));

        Assert.Equal(CommercialInvoice.Copies, html.Split("class=\"page a4 invoice\"").Length - 1);
        Assert.Contains("Sep 29, 2026", html);
        Assert.Contains("Total Value (in USD)", html);
        Assert.Contains("2.90 USD", html);
        Assert.Contains("Reason for Export: <b>GIFT</b>", html);
        Assert.Contains("<b>SCS CO., LTD</b>", html); // I (name) … certify
        Assert.Contains("Signature/Title/Stamp", html);
    }

    [Fact]
    public void Invoice_in_shipping_fee_thanh_dong_rieng_va_cong_vao_tong()
    {
        var order = Order();
        order.ShippingFee = 5;
        var items = new[] { new PrintItem("Dress", 2, "PCS", 20, "6204", "Vietnam") };
        var html = System.Net.WebUtility.HtmlDecode(Render(PrintDocs.Invoice, new OrderPrintModel(order, items)));

        Assert.Contains("Shipping fee (in USD)", html);
        Assert.Contains("5.00 USD", html);
        Assert.Contains("45.00 USD", html); // 2 × 20 + 5
    }

    [Fact]
    public void Invoice_khong_khai_shipping_fee_thi_khong_in_dong_phi()
    {
        var html = Render(PrintDocs.Invoice, new OrderPrintModel(Order(), [new PrintItem("Dress", 2, "PCS", 20, null, "Vietnam")]));

        Assert.DoesNotContain("Shipping fee", html);
    }

    [Fact]
    public void Bill_A4_in_shipping_fee_canh_gia_tri_invoice()
    {
        var order = Order();
        order.ShippingFee = 12.5m;

        Assert.Equal("40.00 USD · Shipping fee: 12.50 USD", BillA4.ValueWithFee(order));
        Assert.Equal("40.00 USD", BillA4.ValueWithFee(Order()));
        Assert.Contains("Shipping fee: 12.50 USD", System.Net.WebUtility.HtmlDecode(Render(PrintDocs.BillA4, new OrderPrintModel(order, []))));
    }

    [Fact]
    public void In_nhieu_loai_1_lan_gop_moi_loai_1_khung_va_in_lan_luot()
    {
        var model = new OrderPrintModel(Order(), [new PrintItem("Dress", 2, "PCS", 20, null, "Vietnam")]);
        var html = OrderDocumentRenderer.RenderMany(PrintDocs.Parse("label-a6,bill-a4,invoice"), [model], Company, Now);

        Assert.Equal(3, html.Split("<iframe").Length - 1);
        // Theo thứ tự chuẩn: Bill A4 → Invoice → nhãn A6
        Assert.True(html.IndexOf("In Vận đơn", StringComparison.Ordinal) < html.IndexOf("In Commercial invoice", StringComparison.Ordinal));
        Assert.True(html.IndexOf("In Commercial invoice", StringComparison.Ordinal) < html.IndexOf("In Shipping mark", StringComparison.Ordinal));
        Assert.Contains("In lần lượt tất cả", html);
        Assert.Contains("afterprint", html);
        // Trang con không tự in / không có thanh công cụ riêng (trang gộp điều khiển)
        Assert.DoesNotContain("setTimeout(function () { window.focus(); window.print(); }", html);
    }

    [Fact]
    public void In_1_loai_van_ra_trang_in_cu()
    {
        var html = OrderDocumentRenderer.RenderMany(PrintDocs.Parse("invoice"), [new OrderPrintModel(Order(), [])], Company, Now);

        Assert.DoesNotContain("<iframe", html);
        Assert.Contains("window.print()", html);
    }

    [Theory]
    [InlineData("bill-a4,invoice", true)]
    [InlineData(" invoice , invoice ", true)]
    [InlineData("bill-a4,abc", false)]
    [InlineData("", false)]
    public void Validator_nhan_nhieu_loai_ngan_dau_phay(string doc, bool valid)
    {
        var result = new VietAnExpress.Shipments.Application.Validators.PrintOrdersQueryValidator().Validate(new PrintOrdersQuery(["1"], doc));
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void CVCK_theo_mau_he_thong_cu()
    {
        var items = new[] { new PrintItem("Dried Mango", 2, "Bag", 5, null, "Vietnam", "Xoài sấy dẻo") };
        var html = System.Net.WebUtility.HtmlDecode(Render(PrintDocs.Cvck, new OrderPrintModel(Order(), items)));

        Assert.Contains("CÔNG VĂN CAM KẾT NỘI DUNG HÀNG XUẤT", html);
        Assert.Contains("TP.HCM Ngày 29 tháng 9 năm 2026", html);
        Assert.Contains("Chi cục Hải Quan cửa khẩu Tân Sơn Nhất", html);
        Assert.Contains("Dried Mango / Xoài sấy dẻo", html);
        Assert.Contains("2.00 Bag", html);
        Assert.Contains("Grosweight</i>: 24.00", html);
        Assert.Contains("United Kingdom", html);
    }

    [Fact]
    public void Invoice_don_cu_khong_co_chi_tiet_hang_thi_lay_1_dong_tu_gia_tri_tong()
    {
        var lines = new OrderPrintModel(Order(pieces: 2), []).InvoiceLines;

        var line = Assert.Single(lines);
        Assert.Equal("Women dress", line.Description);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(20, line.UnitPrice);
        Assert.Equal(40, line.Amount);
    }

    [Fact]
    public void Invoice_don_portal_in_du_dong_hang()
    {
        var items = new[] { new PrintItem("Dress", 2, "PCS", 20, "6204", "Vietnam"), new PrintItem("Scarf", 3, "PCS", 5, null, "Vietnam") };
        var html = Render(PrintDocs.Invoice, new OrderPrintModel(Order(), items));

        Assert.Contains("Scarf", html);
        Assert.Contains("6204", html);
        Assert.Contains("55.00", html); // 40 + 15
    }

    [Fact]
    public void Invoice_rieng_tung_kien_tach_hang_can_kich_thuoc_theo_kien()
    {
        var order = Order(pieces: 3);
        order.ShippingFee = 10;
        var items = new[]
        {
            new PrintItem("Dress", 2, "PCS", 20, null, "Vietnam", Package: 1),
            new PrintItem("Shirt", 1, "PCS", 15, null, "Vietnam", Package: 2),
            new PrintItem("Hat", 3, "PCS", 5, null, "Vietnam", Package: 3)
        };
        // 1 dòng kiện SL 2 (5 kg / kiện) + 1 dòng SL 1 (3 kg) → 3 kiện.
        var model = new OrderPrintModel(order, items, [new PrintPackage(2, 50, 40, 30, 5), new PrintPackage(1, 20, 20, 20, 3)]);

        var scopes = CommercialInvoice.InvoiceScope.PerPackage(model);

        Assert.True(model.InvoicePerPackage);
        Assert.Equal(3, scopes.Count);
        Assert.Equal(["Dress"], scopes[0].Lines.Select(l => l.Description));
        Assert.Equal(["Hat"], scopes[2].Lines.Select(l => l.Description));
        Assert.Equal([5m, 5m, 3m], scopes.Select(s => s.GrossKg));
        Assert.Equal(20m, scopes[2].Packages.Single().Length);
        Assert.Equal([10m, 0m, 0m], scopes.Select(s => s.ShippingFee)); // phí ship của đơn chỉ in ở invoice kiện 1

        var html = System.Net.WebUtility.HtmlDecode(Render(PrintDocs.Invoice, model));
        Assert.Equal(3 * CommercialInvoice.Copies, html.Split("class=\"page a4 invoice\"").Length - 1);
        Assert.Contains("1 (Package 2/3)", html);
    }

    [Fact]
    public void Invoice_chung_khi_khong_dong_nao_mang_so_kien()
    {
        var model = new OrderPrintModel(Order(), [new PrintItem("Dress", 2, "PCS", 20, null, "Vietnam")]);
        Assert.False(model.InvoicePerPackage);
        var html = Render(PrintDocs.Invoice, model);
        Assert.Equal(CommercialInvoice.Copies, html.Split("class=\"page a4 invoice\"").Length - 1);
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData(" 1 ", 1)]
    [InlineData("", null)]
    [InlineData("0", null)]
    [InlineData("-1", null)]
    [InlineData("1.5", null)]
    public void So_kien_cua_dong_hang(string value, int? expected) => Assert.Equal(expected, LegacyOrderFactory.PackageNumber(value));
}

public class OrderSheetTests
{
    [Fact]
    public void File_Excel_co_tieu_de_dong_du_lieu_va_dong_tong()
    {
        var orders = new List<LegacyOrder>
        {
            new() { Id = 1, OrderNumber = 6010839, BillConnect = "4681242285", ConsigneeName = "A", ConsigneeCountry = "NZ", Pieces = 1, WeightKg = 1.5m,
                Pod = "09/01/2026 08:46 Delivered", CreateDate = new DateTime(2026, 1, 5) },
            new() { Id = 2, OrderNumber = 90000001, ConsigneeName = "B", ConsigneeCountry = "UK", Pieces = 2, WeightKg = 24, CreateDate = new DateTime(2026, 9, 29) }
        };
        var labels = new Dictionary<string, string> { ["ok"] = "Đã phát", ["wait"] = "Chưa đi" };

        var bytes = OrderSheet.Build(orders, new DateTime(2026, 9, 29), "SCS CO., LTD", null, null, new DateTime(2026, 9, 29, 14, 0, 0), labels);

        using var book = new XLWorkbook(new MemoryStream(bytes));
        var ws = book.Worksheet(1);
        Assert.Equal("Bảng kê gửi hàng", ws.Cell(1, 1).GetString());
        Assert.Contains("SCS CO., LTD", ws.Cell(2, 1).GetString());
        Assert.Equal("Số vận đơn", ws.Cell(4, 2).GetString());
        Assert.Equal("6010839", ws.Cell(5, 2).GetString());
        Assert.Equal("Đã phát", ws.Cell(5, 19).GetString());
        Assert.Equal("Chưa đi", ws.Cell(6, 19).GetString());
        Assert.Equal("Tổng cộng", ws.Cell(7, 13).GetString());
        Assert.Equal("SUM(O5:O6)", ws.Cell(7, 15).FormulaA1);
    }
}
