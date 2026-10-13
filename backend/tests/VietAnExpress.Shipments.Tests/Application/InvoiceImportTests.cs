using VietAnExpress.Shipments.Application.Orders.Import;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class InvoiceImportTests
{
    private static readonly string[] Headers = ["stt", "product_en", "product_vn", "description", "country", "hs_code_1", "quantyti", "unit", "unit_price"];

    private static SheetRow Row(int line, params string[] values) =>
        new(line, Headers.Zip(values).Where(x => x.Second.Length > 0).ToDictionary(x => x.First, x => x.Second));

    [Fact]
    public void Doc_mau_invoice_bo_dong_chu_thich_va_chuan_hoa_so()
    {
        var sheet = new SheetData(Headers,
        [
            Row(2, "Số Thứ Tự (Tối Đa 49 dòng mặt hàng)", "Tên hàng tiếng Anh", "Tên hàng tiếng Việt", "Thông tin nhà sản xuất", "Nước Sản Xuất", "HS_Code", "Số lượng", "Đơn vị", "Đơn Giá"),
            Row(3, "1", "Artificial Stone Bracelet", "Vòng đeo tay bằng đá nhân tạo", "Vietnam Gemstone Co., Ltd (Hanoi)", "VN", "7117.90", "7.0", "pcs", "15.0"),
            Row(4, "2", "Silk scarf", "Khăn lụa", "", "", "6214.10", "2.5", "KG", "4.25")
        ]);

        var result = InvoiceExcelParser.Parse(sheet);

        Assert.Empty(result.Skipped);
        Assert.Equal(2, result.Items.Count);
        var first = result.Items[0];
        Assert.Equal("Artificial Stone Bracelet", first.DescEn);
        Assert.Equal("Vòng đeo tay bằng đá nhân tạo", first.DescVi);
        Assert.Equal("Vietnam Gemstone Co., Ltd (Hanoi)", first.Manufacturer);
        Assert.Equal("7117.90", first.Hs);
        Assert.Equal("7", first.Qty);
        Assert.Equal("PCS", first.Unit);
        Assert.Equal("15", first.Price);
        Assert.Equal("VN", result.Items[1].Origin); // trống → VN
        Assert.Equal("2.5", result.Items[1].Qty);   // KG được số lẻ
    }

    [Fact]
    public void Dong_loi_bi_bo_qua_kem_so_dong_va_ly_do()
    {
        var sheet = new SheetData(Headers,
        [
            Row(3, "1", "", "Không tên Anh", "", "VN", "", "1", "PCS", "2"),
            Row(4, "2", "Ring", "", "", "VN", "", "1.5", "PCS", "2"),
            Row(5, "3", "Bag", "", "", "VN", "", "1", "PCS", "2.555"),
            Row(6, "4", "Pen", "", "", "VN", "", "1", "PCS", "1")
        ]);

        var result = InvoiceExcelParser.Parse(sheet);

        Assert.Equal("Pen", Assert.Single(result.Items).DescEn);
        Assert.Contains(result.Skipped, s => s.StartsWith("Dòng 3:") && s.Contains("Product_en"));
        Assert.Contains(result.Skipped, s => s.StartsWith("Dòng 4:") && s.Contains("số nguyên"));
        Assert.Contains(result.Skipped, s => s.StartsWith("Dòng 5:") && s.Contains("2 số lẻ"));
    }

    [Fact]
    public void Toi_da_49_dong_mat_hang()
    {
        var rows = Enumerable.Range(1, 51).Select(i => Row(i + 2, i.ToString(), $"Item {i}", "", "", "VN", "", "1", "PCS", "1")).ToList();
        var result = InvoiceExcelParser.Parse(new SheetData(Headers, rows));

        Assert.Equal(InvoiceExcelParser.MaxItems, result.Items.Count);
        Assert.Equal(2, result.Skipped.Count);
    }

    [Fact]
    public void Doc_dung_file_mau_that_tren_web()
    {
        // Tìm web/public/templates/Mau_Import_Invoice.xlsx từ thư mục chạy test đi lên gốc repo
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "web", "public", "templates", "Mau_Import_Invoice.xlsx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        using var stream = File.OpenRead(Path.Combine(dir.FullName, "web", "public", "templates", "Mau_Import_Invoice.xlsx"));

        var sheet = ExcelSheetReader.Read(stream);
        var result = InvoiceExcelParser.Parse(sheet);

        Assert.True(InvoiceExcelParser.HasRequiredColumns(sheet.Headers));
        Assert.Empty(result.Skipped);
        Assert.Equal("Artificial Stone Bracelet", result.Items[0].DescEn);
        Assert.Equal("7117.90", result.Items[0].Hs);
    }

    [Fact]
    public void File_thieu_cot_bat_buoc_la_sai_mau()
    {
        Assert.True(InvoiceExcelParser.HasRequiredColumns(Headers));
        Assert.False(InvoiceExcelParser.HasRequiredColumns(["stt", "product_en", "unit"]));
    }
}
