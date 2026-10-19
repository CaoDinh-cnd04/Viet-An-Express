using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Application.Orders.Documents;
using VietAnExpress.Shipments.Infrastructure.Legacy;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class LegacyOrderFactoryTests
{
    private static readonly LegacyCustomerRef Customer = new(201008, "SGB EXPRESS HN", "Đức Anh", "0977714964", null);
    private static readonly DateTime Today = new(2026, 9, 29);

    private static OrderPayload Payload(string type = "PACK", params OrderPayload.PackagePart[] packages) => new()
    {
        Shipper = new() { Company = "SGB EXPRESS HN", Contact = "Ms Hong Nga", Tel = "0395553421", Address = "241 Tân Xuân, Bắc Từ Liêm, HN", Country = "Vietnam" },
        Service = new() { Carrier = "DHL", Hub = "DHL - Singapore", Reference = "PO-1001" },
        Shipment = new() { Type = type, Pieces = "2", GrossWeight = "1.5" },
        Receiver = new() { Company = "Jason Teo", Contact = "Jason Teo", Tel = "65 98531664", Country = "Singapore", City = "SINGAPORE", Postal = "329640", Addr1 = "8 Bhamo Road", Addr2 = "#02-02 Nova 88" },
        Goods = new() { Description = "WATCH STRAP", DocContent = "Hồ sơ" },
        Packages = [.. packages],
        Invoice = new() { ExportType = "GIFT", Currency = "SGD", Items = [new() { DescEn = "Watch strap", Qty = "2", Price = "5" }] }
    };

    [Fact]
    public void Ghi_dung_dinh_dang_cot_he_thong_cu()
    {
        var o = LegacyOrderFactory.FromPayload(Payload("PACK", new OrderPayload.PackagePart { Qty = "2", Weight = "3", Length = "50", Width = "40", Height = "30" }),
            Customer, 90000001, Today, consigneeCountryId: 191);

        Assert.Equal(201008, o.CustomerId);
        Assert.Equal(90000001, o.OrderNumber);
        Assert.Equal(231, o.SenderCountryId); // Việt Nam theo danh mục nước của hệ thống cũ, không để NULL
        Assert.Equal("DHL|Singapore", o.ServiceName);
        Assert.Equal("Jason Teo", o.ConsigneeName);
        Assert.Equal(191, o.ConsigneeCountryId);
        Assert.Equal("Singapore", o.ConsigneeCountry);
        Assert.Equal("", o.ConsigneeEmail); // cột NOT NULL
        Assert.Equal(24m, o.WeightKg);      // max(2×3 = 6, 2×50×40×30/5000 = 24)
        Assert.Equal(10m, o.GoodsValue);    // 2 × 5
        Assert.Equal("SGD", o.Currency);
        Assert.Equal("PO-1001", o.CustomerBill);
        Assert.Equal("WATCH STRAP", o.GoodsName);
        Assert.Equal(1, o.Service);
        Assert.Equal(1, o.Status);
        Assert.Equal(Today, o.CreateDate);
        Assert.Null(o.SentDate);
    }

    [Fact]
    public void Chung_tu_lay_can_tong_va_noi_dung_chung_tu()
    {
        var o = LegacyOrderFactory.FromPayload(Payload("DOC"), Customer, 90000002, Today);

        Assert.Equal(1.5m, o.WeightKg);
        Assert.Equal("Hồ sơ", o.GoodsName);
    }

    [Theory]
    [InlineData("Quần áo, giày dép", "", "Quần áo, giày dép")]          // nhóm thường: tên nhóm, không cần mô tả
    [InlineData("Quần áo, giày dép", "váy cũ còn sót", "Quần áo, giày dép")]
    [InlineData("Nhiều loại hàng", "Quần áo + mỹ phẩm", "Quần áo + mỹ phẩm")] // nhiều loại: mô tả mặt hàng chính
    [InlineData("", "WATCH STRAP", "WATCH STRAP")]                         // nháp cũ chưa có nhóm
    public void Ten_hang_theo_nhom_hang(string category, string description, string expected) =>
        Assert.Equal(expected, LegacyOrderFactory.GoodsName(category, description));

    [Fact]
    public void Ten_hang_lay_theo_tung_dong_kien_bo_trung()
    {
        var p = new OrderPayload
        {
            Goods = new(),
            Packages =
            [
                new() { Category = "Quần áo, giày dép" },
                new() { Category = "Nhiều loại hàng", Description = "Mỹ phẩm + đồ chơi" },
                new() { Category = "Quần áo, giày dép" }
            ]
        };

        Assert.Equal("Quần áo, giày dép, Mỹ phẩm + đồ chơi", LegacyOrderFactory.GoodsName(p));
        // Có mô tả tổng quan (content) ở cấp đơn → dùng làm tên hàng
        Assert.Equal("Clothes and cosmetics", LegacyOrderFactory.GoodsName(new OrderPayload { Goods = new() { Description = " Clothes and cosmetics " }, Packages = p.Packages }));
        // Nháp cũ / Excel: dòng kiện không có nhóm → lấy khai báo cấp đơn
        Assert.Equal("WATCH STRAP", LegacyOrderFactory.GoodsName(new OrderPayload { Goods = new() { Description = "WATCH STRAP" }, Packages = [new()] }));
    }

    [Fact]
    public void Shipper_goc_cua_forwarder_ghi_vao_Ten_Khach_Cua_FWD()
    {
        var p = Payload();
        var withFwd = new OrderPayload { Shipper = new() { Company = "SGB EXPRESS HN", OriginalShipper = "  CÔNG TY MAY ABC  " }, Service = p.Service,
            Shipment = p.Shipment, Receiver = p.Receiver, Goods = p.Goods, Invoice = p.Invoice };

        Assert.Equal("CÔNG TY MAY ABC", LegacyOrderFactory.FromPayload(withFwd, Customer, 1, Today).ForwarderShipperName);
        Assert.Null(LegacyOrderFactory.FromPayload(p, Customer, 2, Today).ForwarderShipperName);
    }

    [Fact]
    public void Cat_chuoi_theo_do_dai_cot()
    {
        var p = Payload();
        p = new OrderPayload { Shipper = p.Shipper, Service = p.Service, Shipment = p.Shipment, Goods = p.Goods, Invoice = p.Invoice,
            Receiver = new() { Company = new string('A', 400), City = new string('C', 80) } };

        var o = LegacyOrderFactory.FromPayload(p, Customer, 1, Today);

        Assert.Equal(250, o.ConsigneeName!.Length);
        Assert.Equal(50, o.ConsigneeCity!.Length);
    }

    [Theory]
    [InlineData("DHL - Singapore", "DHL", "DHL|Singapore")]
    [InlineData("Chuyên tuyến - AU_Toll Vip", "Chuyên tuyến", "Chuyên tuyến|AU_Toll Vip")]
    [InlineData("", "UPS", "UPS")]
    public void Dich_vu_dang_hang_gach_dung_hub(string hub, string carrier, string expected) =>
        Assert.Equal(expected, LegacyOrderFactory.ServiceName(hub, carrier));

    [Fact]
    public void Dong_kien_ghi_MaVanDon_PCS_DIM_can_la_tong_cua_dong()
    {
        var p = Payload("PACK",
            new OrderPayload.PackagePart { Type = "bag", Qty = "2", Weight = "5", Length = "40", Width = "30", Height = "20.5" },
            new OrderPayload.PackagePart { Qty = "1", Weight = "1", Length = "10", Width = "10", Height = "10" },
            new OrderPayload.PackagePart { Qty = "", Weight = "", Length = "", Width = "", Height = "" }); // dòng trống bị bỏ

        var lines = LegacyOrderFactory.PackageLines(p, 850976);

        Assert.Equal(2, lines.Count);
        var a = lines[0];
        Assert.Equal((850976, 2, "BAG", 40, 30, 21), (a.OrderId, a.Quantity, a.PackType, a.LengthCm, a.WidthCm, a.HeightCm));
        Assert.Equal(10m, a.WeightKg);          // 2 × 5
        Assert.Equal(9.84m, a.VolumetricKg);    // 2 × 40×30×20.5 / 5000
        Assert.Equal(10m, a.ChargeableKg);
        Assert.Equal(LegacyOrderFactory.DefaultPackType, lines[1].PackType);
    }

    [Theory]
    [InlineData("", "Thùng carton", "CARTON")]
    [InlineData("", "Bao / túi", "BAG")]
    [InlineData("", "Kiện gỗ", "WOODEN CASE")]
    [InlineData("pallet", "Thùng carton", "PALLET")] // mã từ Excel được ưu tiên
    [InlineData("", "", "CARTON")]
    public void Loai_bao_bi_ghi_ma_vao_PCS_DIM(string type, string packaging, string expected) =>
        Assert.Equal(expected, LegacyOrderFactory.PackTypeCode(new OrderPayload.PackagePart { Type = type, Packaging = packaging }));

    [Fact]
    public void Chung_tu_khong_ghi_dong_kien()
    {
        var p = Payload("DOC", new OrderPayload.PackagePart { Qty = "1", Weight = "0.5", Length = "30", Width = "20", Height = "1" });
        Assert.Empty(LegacyOrderFactory.PackageLines(p, 1));
    }

    [Fact]
    public void Dong_hang_ghi_MaVanDon_ChiTietHang()
    {
        var p = Payload();
        p.Invoice.Items.Add(new OrderPayload.InvoiceItemPart
        {
            DescEn = "Dress", DescVi = "Đầm", Manufacturer = "LE GA CO., LTD", Origin = "VN", Hs = "62044220", Qty = "3", Unit = "", Price = "12.5"
        });
        p.Invoice.Items.Add(new OrderPayload.InvoiceItemPart { DescEn = " ", Qty = "1", Price = "1" }); // không tên hàng → bỏ

        var lines = LegacyOrderFactory.InvoiceLines(p, 7);

        Assert.Equal(2, lines.Count);
        var d = lines[1];
        Assert.Equal((7, "Dress", "Đầm", "LE GA CO., LTD", "VN", "62044220"), (d.OrderId!.Value, d.DescriptionEn, d.DescriptionVi, d.Manufacturer, d.Origin, d.HsCode));
        Assert.Equal((3m, "PCS", 12.5m, 37.5m, "SGD"), (d.Quantity!.Value, d.Unit, d.UnitPrice!.Value, d.Amount!.Value, d.Currency));
    }

    [Fact]
    public void Doc_lai_2_bang_de_in_can_1_kien_va_ten_hang()
    {
        var pkg = LegacyOrderLinesReader.ToPrint(new LegacyPackageLine { Quantity = 2, LengthCm = 40, WidthCm = 30, HeightCm = 21, WeightKg = 10 });
        Assert.Equal(new PrintPackage(2, 40, 30, 21, 5), pkg);

        var item = LegacyOrderLinesReader.ToPrint(new LegacyInvoiceLine { DescriptionEn = "Dress", DescriptionVi = "Đầm", Quantity = 3, UnitPrice = 12.5m });
        Assert.Equal(("Dress", "Đầm", "Dress / Đầm", "PCS", 37.5m), (item.Description, item.DescriptionVi, item.Name(), item.Unit, item.Amount));
    }
}

public class LegacyOrderViewTests
{
    private static readonly DateTime Today = new(2026, 9, 29);

    private static LegacyOrder Order(string? pod = null, DateTime? sent = null, DateTime? eta = null) => new()
    {
        Id = 850139, OrderNumber = 6008435, BillConnect = "6008435", CustomerBill = "SHOP-9",
        ConsigneeName = "Jason Teo", ConsigneeCountry = "Singapore", ServiceName = "Chuyên tuyến|Singapore",
        CreateDate = new DateTime(2025, 12, 30), SentDate = sent, PodEstimate = eta, Pod = pod,
        GoodsName = "WATCH STRAP", Pieces = 1, WeightKg = 0.10m
    };

    [Theory]
    [InlineData("08/01/2026 17:53, DELIVERED Jason Teo", "2026-01-03", null, "ok")]
    [InlineData("08/01/2026 17:53 Consignee not available", null, null, "ok")] // có thông tin POD = đã phát
    [InlineData(null, "2026-09-20", "2026-09-25", "fly")]                     // quá ngày dự kiến vẫn là đã đi (bỏ "vượt ngày")
    [InlineData(null, null, null, "wait")]
    public void Nhan_trang_thai_suy_ra_tu_POD_va_ngay_gui(string? pod, string? sent, string? eta, string expected) =>
        Assert.Equal(expected, LegacyOrderStatus.Of(Order(pod, Parse(sent), Parse(eta)), Today));

    [Theory]
    [InlineData(null, null, "wait,nd")]                       // chưa gửi, chưa POD
    [InlineData(null, "2026-09-20", "fly,nd")]                // đã gửi, chưa POD
    [InlineData("08/01/2026 DELIVERED", "2026-09-20", "fly,ok")] // đã gửi, có POD
    public void Bo_loc_trang_thai_co_the_trung_nhau(string? pod, string? sent, string groups)
    {
        var order = Order(pod, Parse(sent));
        var matched = LegacyOrderStatus.All.Where(s => LegacyOrderStatus.Is(s, Today).Compile()(order));
        Assert.Equal(groups, string.Join(",", matched));
        Assert.Contains(LegacyOrderStatus.Of(order, Today), matched); // nhãn dòng luôn thuộc 1 nhóm lọc khớp
    }

    [Fact]
    public void Doi_sang_dang_Order_cua_frontend()
    {
        var dto = LegacyOrderView.ToDto(Order("08/01/2026 17:53, DELIVERED Jason Teo", new DateTime(2026, 1, 3)), Today);

        Assert.Equal("6008435", dto.Bill);
        Assert.Null(dto.Connect); // trùng số VA thì không lặp lại
        Assert.Equal("Chuyên tuyến - Singapore", dto.Route);
        Assert.Equal("30/12/2025", dto.Created);
        Assert.Equal("03/01/2026", dto.Sent);
        Assert.Equal("ok", dto.St);
        Assert.Equal("1 kiện · 0.1 kg", dto.Pcs);
        Assert.Equal("PACK", dto.Type);
        Assert.Equal(new("08/01/2026", "17:53", "Jason Teo"), dto.Pod);
    }

    [Theory]
    [InlineData("16/01/2026 12:26 DELIVERED Signed for by: W.WILLIAN", "W.WILLIAN")]
    [InlineData("09/01/2026 08:46 Delivered", "")]
    public void Tach_ten_nguoi_ky(string pod, string signer) =>
        Assert.Equal(signer, LegacyOrderView.ParsePod(pod)!.Signer);

    [Theory]
    [InlineData("DOCUMENTS", true)]
    [InlineData("Documents", true)]
    [InlineData("Document", true)]
    [InlineData("Hồ sơ chứng từ", true)]
    [InlineData("doc", true)]
    [InlineData("DOCS for visa", true)]
    [InlineData("visa doc", true)]
    [InlineData("Dockside tool", false)]
    [InlineData("WATCH STRAP", false)]
    [InlineData(null, false)]
    public void Nhan_dien_chung_tu(string? goods, bool isDoc) => Assert.Equal(isDoc, LegacyOrderView.IsDocument(goods));

    private static DateTime? Parse(string? d) => d is null ? null : DateTime.Parse(d, System.Globalization.CultureInfo.InvariantCulture);
}

public class LegacyOrderDetailTests
{
    [Fact]
    public void Chi_tiet_don_co_du_nguoi_gui_nguoi_nhan_de_nhan_ban()
    {
        var o = new LegacyOrder
        {
            Id = 1, OrderNumber = 90000001, SenderName = "SCS CO., LTD", SenderPhone = "0909", SenderAddress = "14 Sam Sơn",
            ConsigneeName = "ACME LTD", ConsigneeCountry = "United Kingdom", ConsigneeCity = "LONDON", ConsigneeAddress1 = "1 King St",
            ConsigneeAddress2 = "Floor 2", ConsigneePostalCode = "EC1A 1BB", ConsigneePhone = "+44"
        };

        var dto = LegacyOrderView.ToDetailDto(o, new DateTime(2026, 9, 29));

        Assert.Equal("SCS CO., LTD", dto.Shipper!.Company);
        Assert.Equal("1 King St", dto.Receiver!.Addr1);
        Assert.Equal("EC1A 1BB", dto.Receiver.Postal);
        Assert.Null(LegacyOrderView.ToDto(o, new DateTime(2026, 9, 29)).Receiver); // danh sách không kèm để nhẹ
    }

    [Fact]
    public void Chi_tiet_don_kem_kien_va_invoice()
    {
        var o = new LegacyOrder { Id = 7, OrderNumber = 90000006, Currency = "USD", ExportReason = "GIFT", ShippingFee = 5, GoodsValue = 40 };
        var packages = new[] { new LegacyPackageLine { OrderId = 7, Quantity = 2, PackType = "CARTON", LengthCm = 40, WidthCm = 30, HeightCm = 20, WeightKg = 9 } };
        var items = new[] { new LegacyInvoiceLine { OrderId = 7, DescriptionEn = "Dress", DescriptionVi = "Váy", Quantity = 5, Unit = null, UnitPrice = 8 } };

        var dto = LegacyOrderView.ToDetailDto(o, new DateTime(2026, 9, 30), packages, items);

        var p = Assert.Single(dto.Packages!);
        Assert.Equal((2, 40, 4.5m), (p.Qty, p.Length, p.WeightKg)); // cân 1 kiện = tổng / số kiện
        var i = Assert.Single(dto.Invoice!.Items);
        Assert.Equal(("PCS", 40m), (i.Unit, i.Amount));                 // thiếu ĐVT → PCS, thành tiền = SL × giá
        Assert.Equal(("USD", "GIFT", 5m), (dto.Invoice.Currency, dto.Invoice.ExportType, dto.Invoice.ShippingFee));
    }
}
