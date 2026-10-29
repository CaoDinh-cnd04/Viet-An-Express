using ClosedXML.Excel;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Import;
using VietAnExpress.Shipments.OrderData;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class ExcelImportTests
{
    // Tiêu đề đúng như file mẫu Mau_Excel_Tao_Don.xlsx (kể cả các cột ghi chú "KẾT THÚC"…).
    private static readonly string[] TemplateHeaders =
    [
        "STT", "Ref_No", "Shipper_att", "Shipper_Tel", "Cnee_country_Code", "Cnee_company", "Cnee_contact_name", "Cnee_Tel", "Cnee_Email",
        "Cnee_TaxID", "Cnee_Postalcode", "Cnee_City", "Cnee_State", "Add1", "Add2", "Add3", "Type", "Description", "Currency", "Export_Type",
        "Invoice_Value", "Shipping_fee", "Qty_Pack_1", "Pack_Type_1", "L_1", "W_1", "H_1", "GW_1", "Product_en_1", "Product_vn_1",
        "Manufacturer_1", "Org_Country_1", "HS_Code_1", "Qty_1", "Unit_1", "Unit_Price_1", "KẾT THÚC", "Product_en_2", "Product_vn_2",
        "Manufacturer_2", "Org_Country_2", "HS_Code_2", "Qty_2", "Unit_2", "Unit_Price_2"
    ];

    private static readonly ImportContext Ctx = new(
        new LegacyCustomerRef(42, "SCS CO., LTD", "MR A", "0909000000", "a@scs.vn"), "", "", "TP.HCM",
        new Dictionary<string, ImportCountry>(StringComparer.OrdinalIgnoreCase)
        {
            ["US"] = new("US", "United States", "+1"),
            ["VN"] = new("VN", "Vietnam", "+84"),
            ["SG"] = new("SG", "Singapore", "+65")
        });

    /// <summary>Dòng 3 của file mẫu.</summary>
    private static Dictionary<string, string> Sample() => new()
    {
        ["stt"] = "1", ["ref_no"] = "ABC12345", ["shipper_att"] = "MR HIẾU", ["shipper_tel"] = "915514144", ["cnee_country_code"] = "US",
        ["cnee_company"] = "ABC LOGISTICS US", ["cnee_contact_name"] = "MS MARY", ["cnee_tel"] = "9258875", ["cnee_email"] = "MARY@GMAIL.COM",
        ["cnee_postalcode"] = "76904", ["cnee_city"] = "SAN ANGELO", ["cnee_state"] = "TX", ["add1"] = "6114 LYDIAN CT,",
        ["add2"] = "SAN ANGELO, TX 76904, USA", ["type"] = "P", ["description"] = "DRESS (100% COTTON)", ["currency"] = "USD",
        ["export_type"] = "GIFT", ["qty_pack_1"] = "1", ["pack_type_1"] = "CARTON", ["l_1"] = "40", ["w_1"] = "30", ["h_1"] = "30",
        ["gw_1"] = "5", ["product_en_1"] = "DRESS (100% COTTON)", ["product_vn_1"] = "ĐẦM", ["manufacturer_1"] = "LE GA CO., LTD",
        ["org_country_1"] = "VN", ["hs_code_1"] = "111111", ["qty_1"] = "10", ["unit_1"] = "PCS", ["unit_price_1"] = "10"
    };

    private static ImportedRow Parse(Action<Dictionary<string, string>>? change = null, ImportContext? ctx = null)
    {
        var cells = Sample();
        change?.Invoke(cells);
        return ExcelOrderParser.Parse(new SheetRow(3, cells.Where(kv => kv.Value.Length > 0).ToDictionary()), ctx ?? Ctx);
    }

    [Fact]
    public void Preview_Excel_tinh_tung_kien_va_khop_can_ghi_vao_don()
    {
        var r = Parse(c =>
        {
            c["l_1"] = "20"; c["w_1"] = "30"; c["h_1"] = "40"; c["gw_1"] = "6";
            c["qty_pack_2"] = "1"; c["pack_type_2"] = "CARTON";
            c["l_2"] = "30"; c["w_2"] = "50"; c["h_2"] = "10"; c["gw_2"] = "2";
        });
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        Assert.Equal(8m, r.GrossKg);
        Assert.Equal(9m, r.ChargeableKg);
        Assert.Equal(r.ChargeableKg, LegacyOrderFactory.FromPayload(r.Payload, Ctx.Customer, 1, new DateTime(2026, 10, 13)).WeightKg);
    }

    [Theory]
    [InlineData("UPS", "UPS - VN Saver", 4.5)]
    [InlineData("UPS", "UPS - VN Expedited", 6)]
    [InlineData("Fedex", "Fedex - VN", 3.5)]
    public void Preview_Excel_GW_la_can_ca_dong_lam_tron_dung_theo_hub(string carrier, string hub, double expected)
    {
        var r = Parse(c => { c["qty_pack_1"] = "3"; c["gw_1"] = "3.3"; c["l_1"] = "1"; c["w_1"] = "1"; c["h_1"] = "1"; }, Ctx with { Service = carrier, Hub = hub });
        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        Assert.Equal((decimal)expected, r.ChargeableKg);
        Assert.Equal(r.ChargeableKg, LegacyOrderFactory.FromPayload(r.Payload, Ctx.Customer, 1, new DateTime(2026, 10, 13)).WeightKg);
    }

    [Fact]
    public void Doc_file_mau_bo_dong_chu_thich_va_dong_trong_so_giu_nguyen_dang_bat_bien()
    {
        using var book = new XLWorkbook();
        book.AddWorksheet("HƯỚNG DẪN").Cell(1, 1).Value = "DIỄN DÃI CÁC TRƯỜNG DỮ LIỆU:";
        var ws = book.AddWorksheet("DATA");
        for (var i = 0; i < TemplateHeaders.Length; i++) ws.Cell(1, i + 1).Value = TemplateHeaders[i];
        ws.Cell(2, 3).Value = "Người liên hệ\n(bắt buộc Điền)";
        ws.Cell(3, 1).Value = 1;
        ws.Cell(3, 4).Value = 915514144;   // SĐT lưu dạng số
        ws.Cell(3, 5).Value = "US";
        ws.Cell(3, 28).Value = 2.5;        // GW_1
        ws.Cell(5, 1).Value = 2;           // dòng chỉ có STT → bỏ
        using var ms = new MemoryStream();
        book.SaveAs(ms);
        ms.Position = 0;

        var sheet = ExcelSheetReader.Read(ms);

        var row = Assert.Single(sheet.Rows);
        Assert.Equal(3, row.Line);
        Assert.Equal("915514144", row["shipper_tel"]);
        Assert.Equal("2.5", row["gw_1"]);
        Assert.Contains("hs_code_1", sheet.Headers);
        Assert.Empty(ExcelOrderParser.MissingHeaders(sheet.Headers));
    }

    [Fact]
    public void File_khong_phai_excel_bao_loi_de_hieu()
    {
        using var ms = new MemoryStream("a,b,c"u8.ToArray());
        var e = Assert.Throws<InvalidDataException>(() => ExcelSheetReader.Read(ms));
        Assert.Contains(".xlsx", e.Message);
    }

    [Fact]
    public void Thieu_cot_bat_buoc_tra_dung_ten_cot_trong_mau()
    {
        var missing = ExcelOrderParser.MissingHeaders(["cnee_company", "cnee_city"]);
        Assert.Contains("Cnee_country_Code", missing);
        Assert.Contains("Qty_Pack_1", missing);
        Assert.Contains("GW_1", missing);
        Assert.DoesNotContain("Cnee_company", missing);
    }

    [Fact]
    public void Dong_mau_hop_le_dung_du_form_don()
    {
        var r = Parse();

        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        var p = r.Payload;
        Assert.Equal("SCS CO., LTD", p.Shipper.Company);        // theo tài khoản
        Assert.Equal("MR HIẾU", p.Shipper.Contact);
        Assert.Equal("United States", p.Receiver.Country);
        Assert.Equal("+1", p.Receiver.PhoneCode);
        Assert.Equal("PACK", p.Shipment.Type);
        Assert.Equal("ABC12345", p.Service.Reference);
        var pack = Assert.Single(p.Packages);
        Assert.Equal(("1", "40", "30", "30", "5"), (pack.Qty, pack.Length, pack.Width, pack.Height, pack.Weight));
        var item = Assert.Single(p.Invoice.Items);
        Assert.Equal(("DRESS (100% COTTON)", "ĐẦM", "111111", "VN", "10", "PCS", "10"), (item.DescEn, item.DescVi, item.Hs, item.Origin, item.Qty, item.Unit, item.Price));
        Assert.Equal(100, r.Value);                             // Invoice_Value trống → tổng sản phẩm
        Assert.Equal(7.2m, r.ChargeableKg);                     // max(5, 40×30×30/5000)
        Assert.Equal(5, r.GrossKg);
    }

    [Fact]
    public void Nhieu_dong_kien_va_san_pham_GW_la_tong_cua_dong_kien()
    {
        var r = Parse(c =>
        {
            c["qty_pack_1"] = "2"; c["gw_1"] = "10";
            c["qty_pack_2"] = "1"; c["pack_type_2"] = "BAG"; c["l_2"] = "20"; c["w_2"] = "20"; c["h_2"] = "20"; c["gw_2"] = "1,5";
            c["product_en_2"] = "SCARF"; c["qty_2"] = "3"; c["unit_price_2"] = "5"; c["unit_2"] = "pcs";
        });

        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        Assert.Equal(2, r.Payload.Packages.Count);
        Assert.Equal("5", r.Payload.Packages[0].Weight);       // 10 kg / 2 kiện
        Assert.Equal("3", r.Payload.Shipment.Pieces);
        Assert.Equal(11.5m, r.GrossKg);
        Assert.Equal(115, r.Value);
        Assert.Equal("PCS", r.Payload.Invoice.Items[1].Unit);
        Assert.Equal("VN", r.Payload.Invoice.Items[1].Origin); // xuất xứ trống → VN
    }

    [Fact]
    public void Kiem_tra_theo_huong_dan_cua_file_mau()
    {
        var r = Parse(c =>
        {
            c["cnee_state"] = "";
            c["add1"] = new string('x', 31);
            c["hs_code_1"] = "12345";
            c["type"] = "X";
            c["currency"] = "US";
            c["gw_1"] = "abc";
            c["cnee_tel"] = "";
        });

        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.StartsWith("Thiếu Cnee_State"));
        Assert.Contains(r.Errors, e => e.StartsWith("Add1 dài 31"));
        Assert.Contains(r.Errors, e => e.StartsWith("HS_Code_1"));
        Assert.Contains(r.Errors, e => e.StartsWith("Type \"X\""));
        Assert.Contains(r.Errors, e => e.StartsWith("Currency"));
        Assert.Contains(r.Errors, e => e.StartsWith("GW_1 \"abc\""));
        Assert.Contains("Thiếu Cnee_Tel", r.Errors);
    }

    [Fact]
    public void Ma_nuoc_sai_va_hang_hoa_khong_co_san_pham_bi_bao_loi()
    {
        var r = Parse(c =>
        {
            c["cnee_country_code"] = "USA";
            foreach (var k in c.Keys.Where(k => k.EndsWith("_1") && !k.Contains("pack") && k is not ("l_1" or "w_1" or "h_1" or "gw_1")).ToList()) c[k] = "";
        });

        Assert.Contains(r.Errors, e => e.StartsWith("Cnee_country_Code \"USA\""));
        Assert.Contains(r.Errors, e => e.StartsWith("Hàng hoá (Type P) cần ít nhất 1 sản phẩm"));
    }

    [Fact]
    public void Chung_tu_nhe_khong_can_kich_thuoc_va_san_pham()
    {
        var r = Parse(c =>
        {
            c["type"] = "D"; c["gw_1"] = "0.5"; c["l_1"] = c["w_1"] = c["h_1"] = "";
            foreach (var k in c.Keys.Where(k => k.StartsWith("product_") || k is "qty_1" or "unit_1" or "unit_price_1" or "hs_code_1" or "org_country_1" or "manufacturer_1").ToList()) c[k] = "";
            c["invoice_value"] = "20";
        });

        Assert.True(r.IsValid, string.Join("; ", r.Errors));
        Assert.Equal("DOC", r.Payload.Shipment.Type);
        Assert.Equal("DRESS (100% COTTON)", r.Payload.Goods.DocContent);
        Assert.Equal("20", r.Payload.Invoice.DeclaredValue);
    }

    [Theory]
    [InlineData("PCS", "1.5", false)]
    [InlineData("box", "2.5", false)]
    [InlineData("PCS", "3", true)]
    [InlineData("KG", "1.5", true)]
    public void So_luong_nguyen_voi_don_vi_co_san_so_thuc_voi_don_vi_khac(string unit, string qty, bool ok)
    {
        var r = Parse(c => { c["unit_1"] = unit; c["qty_1"] = qty; });
        Assert.Equal(ok, !r.Errors.Any(e => e.Contains("Qty_1") && e.Contains("số nguyên")));
    }

    [Theory]
    [InlineData("12.345", false)]
    [InlineData("1.0000000000000000000003", false)]
    [InlineData("12.34", true)]
    [InlineData("12", true)]
    public void Don_gia_invoice_toi_da_2_so_le(string price, bool ok)
    {
        var r = Parse(c => c["unit_price_1"] = price);
        Assert.Equal(ok, !r.Errors.Any(e => e.Contains("tối đa 2 số lẻ")));
    }

    [Fact]
    public void Chung_tu_tren_2kg_tu_chuyen_sang_hang_hoa()
    {
        var r = Parse(c => c["type"] = "D");  // GW 5 kg

        Assert.Equal("PACK", r.Payload.Shipment.Type);
        Assert.Contains(r.Warnings, w => w.Contains("tự chuyển sang hàng hoá"));
    }

    [Fact]
    public void Invoice_Value_lech_tong_san_pham_thi_canh_bao()
    {
        var r = Parse(c => c["invoice_value"] = "200");

        Assert.True(r.IsValid);
        Assert.Equal(100, r.Value);
        Assert.Contains(r.Warnings, w => w.StartsWith("Invoice_Value 200"));
    }

    [Fact]
    public void Dich_vu_chon_tren_trang_uu_tien_hon_cot_trong_file()
    {
        var withPage = Parse(c => { c["service"] = "UPS"; c["hub"] = "UPS - US"; }, Ctx with { Service = "DHL", Hub = "DHL - Singapore" });
        var fromFile = Parse(c => { c["service"] = "UPS"; c["hub"] = "UPS - US"; });

        Assert.Equal(("DHL", "DHL - Singapore"), (withPage.Payload.Service.Carrier, withPage.Payload.Service.Hub));
        Assert.Equal(("UPS", "UPS - US"), (fromFile.Payload.Service.Carrier, fromFile.Payload.Service.Hub));
    }

    [Fact]
    public void Don_dung_tu_excel_ghi_MaVanDon_dung_cot()
    {
        var o = LegacyOrderFactory.FromPayload(Parse().Payload, Ctx.Customer, 90000010, new DateTime(2026, 9, 29));

        Assert.Equal("United States", o.ConsigneeCountry);
        Assert.Equal("1", o.ConsigneePhoneCode);
        Assert.Equal("TX", o.ConsigneeState);
        Assert.Equal(1, o.Pieces);
        Assert.Equal(7.2m, o.WeightKg);
        Assert.Equal(100, o.GoodsValue);
        Assert.Equal("GIFT", o.ExportReason);
        Assert.Equal("ABC12345", o.CustomerBill);
    }

    [Theory]
    [InlineData("2,5", 2.5)]
    [InlineData("1,000.50", 1000.5)]
    [InlineData(" 12 ", 12)]
    public void Doc_so_kieu_viet_va_kieu_anh(string text, double expected)
    {
        Assert.True(ExcelOrderParser.TryNumber(text, out var n));
        Assert.Equal((decimal)expected, n);
    }

    [Fact]
    public void Danh_muc_nuoc_du_phong_khi_API_loi()
    {
        var countries = ImportCountries.Build([]);
        Assert.Equal("Singapore", countries["SG"].Name);
        Assert.Equal("United States", countries["us"].Name);
    }
}
