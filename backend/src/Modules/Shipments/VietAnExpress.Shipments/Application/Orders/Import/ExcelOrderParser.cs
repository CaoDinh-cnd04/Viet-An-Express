using System.Globalization;
using System.Text.RegularExpressions;
using VietAnExpress.Shipments.Domain;

namespace VietAnExpress.Shipments.Application.Orders.Import;

/// <summary>Nước đến theo mã ISO 2 ký tự: tên tiếng Anh (lưu vào đơn như hệ thống cũ) và mã điện thoại.</summary>
internal sealed record ImportCountry(string Code, string Name, string? DialCode);

/// <summary>Thông tin dùng chung cho cả file: khách đang đăng nhập, dịch vụ / hub chọn trên trang, danh mục nước.</summary>
internal sealed record ImportContext(
    LegacyCustomerRef Customer, string Service, string Hub, string Branch, IReadOnlyDictionary<string, ImportCountry> Countries);

/// <summary>Kết quả kiểm tra 1 dòng: đơn dựng được (khi không có lỗi) + lỗi / cảnh báo + số liệu để xem trước.</summary>
internal sealed record ImportedRow(int Line, OrderPayload Payload, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
    public decimal GrossKg { get; init; }
    public decimal ChargeableKg { get; init; }
    public decimal Value { get; init; }
    public string CountryCode { get; init; } = "";
}

/// <summary>
/// Chuyển 1 dòng file mẫu Mau_Excel_Tao_Don.xlsx thành form đơn (<see cref="OrderPayload"/>, giống trang Tạo đơn)
/// và kiểm tra theo sheet "HƯỚNG DẪN". Hàm thuần — có test.
/// Nhóm cột lặp: kiện Qty_Pack_n, Pack_Type_n, L_n, W_n, H_n, GW_n; hàng Product_en_n … Unit_Price_n (n = 1, 2, 3…).
/// </summary>
internal static partial class ExcelOrderParser
{
    public const int AddressMax = 30;
    public const int DescriptionMax = 50;
    public const int MaxProducts = 50;

    /// <summary>ĐVT có sẵn của portal (đếm được) — số lượng phải là số nguyên; khớp PRESET_UNITS ở frontend (lib/units.ts).</summary>
    private static readonly IReadOnlySet<string> CountableUnits = new HashSet<string>(StringComparer.Ordinal) { "PCS", "SET", "BOX", "BAG" };
    public const int MaxPackageLines = 20;

    /// <summary>Cột phải có trong dòng tiêu đề — thiếu thì báo file sai mẫu, không xét từng dòng.</summary>
    public static readonly string[] RequiredHeaders =
    [
        "cnee_country_code", "cnee_company", "cnee_contact_name", "cnee_tel", "cnee_city", "add1", "add2",
        "type", "description", "currency", "export_type", "qty_pack_1", "gw_1"
    ];

    /// <summary>Nước bắt buộc có bang / tỉnh.</summary>
    private static readonly HashSet<string> StateRequired = ["US", "CA", "AU"];

    public static IReadOnlyList<string> MissingHeaders(IEnumerable<string> headers)
    {
        var have = headers.ToHashSet();
        return RequiredHeaders.Where(h => !have.Contains(h)).Select(DisplayName).ToList();
    }

    public static ImportedRow Parse(SheetRow row, ImportContext ctx)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        void Required(string column, string value)
        {
            if (value.Length == 0) errors.Add($"Thiếu {DisplayName(column)}");
        }

        // ---------- Người gửi: công ty theo tài khoản; người liên hệ / SĐT lấy trong file, trống thì theo tài khoản ----------
        var shipperContact = Coalesce(row["shipper_att"], ctx.Customer.ContactName);
        var shipperTel = Coalesce(row["shipper_tel"], ctx.Customer.Phone);
        if (shipperContact.Length == 0) errors.Add("Thiếu Shipper_att");
        if (shipperTel.Length == 0) errors.Add("Thiếu Shipper_Tel");

        // ---------- Người nhận ----------
        var code = row["cnee_country_code"].ToUpperInvariant();
        ImportCountry? country = null;
        if (code.Length == 0) errors.Add("Thiếu Cnee_country_Code");
        else if (!CountryCodePattern().IsMatch(code) || !ctx.Countries.TryGetValue(code, out country))
            errors.Add($"Cnee_country_Code \"{row["cnee_country_code"]}\" không hợp lệ (mã nước 2 ký tự, vd US, AU, SG)");

        foreach (var c in new[] { "cnee_company", "cnee_contact_name", "cnee_tel", "cnee_city", "add1", "add2" })
            Required(c, row[c]);
        if (country is not null && StateRequired.Contains(country.Code) && row["cnee_state"].Length == 0)
            errors.Add($"Thiếu Cnee_State (bắt buộc với {country.Name})");
        foreach (var c in new[] { "add1", "add2" })
            if (row[c].Length > AddressMax) errors.Add($"{DisplayName(c)} dài {row[c].Length} ký tự, tối đa {AddressMax}");
        var email = row["cnee_email"];
        if (email.Length > 0 && !EmailPattern().IsMatch(email)) errors.Add($"Cnee_Email \"{email}\" không hợp lệ");

        // ---------- Vận chuyển ----------
        var typeText = row["type"].ToUpperInvariant();
        var isDoc = typeText is "D" or "DOC";
        if (typeText.Length == 0) errors.Add("Thiếu Type (D = chứng từ, P = hàng hoá)");
        else if (!isDoc && typeText is not ("P" or "PACK")) errors.Add($"Type \"{row["type"]}\" không hợp lệ — chỉ nhận D hoặc P");

        var description = row["description"];
        Required("description", description);
        if (description.Length > DescriptionMax) errors.Add($"Description dài {description.Length} ký tự, tối đa {DescriptionMax}");

        var currency = row["currency"].ToUpperInvariant();
        Required("currency", currency);
        if (currency.Length > 0 && !CurrencyPattern().IsMatch(currency)) errors.Add($"Currency \"{row["currency"]}\" không hợp lệ (3 chữ cái, vd USD)");
        Required("export_type", row["export_type"]);

        var invoiceValue = OptionalNumber(row, "invoice_value", errors);
        var shippingFee = OptionalNumber(row, "shipping_fee", errors);

        // Dịch vụ: chọn trên trang ưu tiên; không chọn thì theo cột Service / HUB trong file (nếu có).
        var service = Coalesce(ctx.Service, row["service"]);
        var hub = ctx.Service.Length > 0 ? ctx.Hub : row["hub"];

        // ---------- Kiện hàng ----------
        var packages = new List<OrderPayload.PackagePart>();
        decimal gross = 0;
        var pieces = 0;
        foreach (var n in Indexes(row, "qty_pack_", "pack_type_", "l_", "w_", "h_", "gw_"))
        {
            if (n > MaxPackageLines) { errors.Add($"Tối đa {MaxPackageLines} dòng kiện"); break; }
            var qty = Number(row, $"qty_pack_{n}", errors, required: true, integer: true);
            if (row[$"pack_type_{n}"].Length == 0) errors.Add($"Thiếu Pack_Type_{n}");
            var gw = Number(row, $"gw_{n}", errors, required: true);
            // Chứng từ không bắt buộc kích thước.
            var l = Number(row, $"l_{n}", errors, required: !isDoc);
            var w = Number(row, $"w_{n}", errors, required: !isDoc);
            var h = Number(row, $"h_{n}", errors, required: !isDoc);

            var count = (int)qty;
            pieces += count;
            gross += gw;
            // GW_n là tổng cân của dòng kiện; form đơn lưu cân 1 kiện.
            packages.Add(new OrderPayload.PackagePart
            {
                Type = row[$"pack_type_{n}"].ToUpperInvariant(), Qty = Text(count), Length = Text(l), Width = Text(w), Height = Text(h),
                Weight = Text(count > 0 ? decimal.Round(gw / count, 3, MidpointRounding.AwayFromZero) : gw)
            });
        }
        if (packages.Count == 0 && !errors.Any(e => e.Contains("Qty_Pack_1"))) errors.Add("Thiếu thông tin kiện hàng (Qty_Pack_1, GW_1)");

        // Chứng từ > 2kg → hãng không nhận DOC, chuyển thành hàng hoá (giống trang Tạo đơn).
        if (isDoc && gross > ShippingRules.DocumentMaxWeightKg)
        {
            isDoc = false;
            warnings.Add($"Chứng từ nặng {Text(gross)} kg (> {Text(ShippingRules.DocumentMaxWeightKg)} kg) — tự chuyển sang hàng hoá");
        }

        // ---------- Hàng hoá (invoice) ----------
        var items = new List<OrderPayload.InvoiceItemPart>();
        foreach (var n in Indexes(row, "product_en_", "product_vn_", "manufacturer_", "org_country_", "hs_code_", "qty_", "unit_", "unit_price_"))
        {
            if (n > MaxProducts) { errors.Add($"Tối đa {MaxProducts} mặt hàng"); break; }
            if (row[$"product_en_{n}"].Length == 0) errors.Add($"Thiếu Product_en_{n}");
            // ĐVT có sẵn (đếm được) → số lượng phải là số nguyên; ĐVT khác (KG, M…) được số thực.
            var unit = Coalesce(row[$"unit_{n}"], "PCS").ToUpperInvariant();
            var qty = Number(row, $"qty_{n}", errors, required: true, integer: CountableUnits.Contains(unit));
            var price = Number(row, $"unit_price_{n}", errors, required: true, allowZero: true);
            // Đơn giá invoice quốc tế chỉ 2 số lẻ.
            if (decimal.Round(price, 2) != price) errors.Add($"Unit_Price_{n} \"{row[$"unit_price_{n}"]}\" tối đa 2 số lẻ");
            var hs = row[$"hs_code_{n}"].Replace(".", "").Replace(" ", "");
            if (hs.Length > 0 && !HsCodePattern().IsMatch(hs)) errors.Add($"HS_Code_{n} \"{row[$"hs_code_{n}"]}\" phải gồm 6, 8 hoặc 10 chữ số");
            var origin = row[$"org_country_{n}"].ToUpperInvariant();
            if (origin.Length > 0 && !ctx.Countries.ContainsKey(origin)) errors.Add($"Org_Country_{n} \"{row[$"org_country_{n}"]}\" không phải mã nước 2 ký tự");

            items.Add(new OrderPayload.InvoiceItemPart
            {
                DescEn = row[$"product_en_{n}"], DescVi = row[$"product_vn_{n}"], Manufacturer = row[$"manufacturer_{n}"],
                Origin = origin.Length > 0 ? origin : "VN", Hs = hs, Qty = Text(qty),
                Unit = unit, Price = Text(price)
            });
        }
        if (!isDoc && items.Count == 0) errors.Add("Hàng hoá (Type P) cần ít nhất 1 sản phẩm: Product_en_1, Qty_1, Unit_Price_1");

        var itemsValue = items.Sum(i => Num(i.Qty) * Num(i.Price));
        if (invoiceValue > 0 && items.Count > 0 && Math.Abs(invoiceValue - itemsValue) > 0.01m)
            warnings.Add($"Invoice_Value {Text(invoiceValue)} khác tổng tiền sản phẩm {Text(itemsValue)} — dùng tổng theo sản phẩm");
        var value = items.Count > 0 ? itemsValue : invoiceValue;

        var payload = new OrderPayload
        {
            Shipper = new OrderPayload.ShipperPart
            {
                Company = ctx.Customer.CompanyName, Contact = shipperContact, Tel = shipperTel,
                TaxId = row["shipper_tax"], Email = Coalesce(row["shipper_email"], ctx.Customer.Email), Country = "Vietnam", Branch = ctx.Branch
            },
            Service = new OrderPayload.ServicePart { Carrier = service, Hub = hub, Reference = row["ref_no"] },
            Shipment = new OrderPayload.ShipmentPart { Type = isDoc ? "DOC" : "PACK", Pieces = Text(Math.Max(1, pieces)), GrossWeight = Text(gross) },
            Receiver = new OrderPayload.ReceiverPart
            {
                Country = country?.Name ?? code, City = row["cnee_city"], State = row["cnee_state"], Postal = row["cnee_postalcode"],
                Company = row["cnee_company"], Contact = row["cnee_contact_name"], Tel = row["cnee_tel"], PhoneCode = country?.DialCode ?? "",
                TaxId = row["cnee_taxid"], Email = email, Addr1 = row["add1"], Addr2 = row["add2"], Addr3 = row["add3"]
            },
            Goods = new OrderPayload.GoodsPart { Description = isDoc ? "" : description, DocContent = isDoc ? description : "" },
            Packages = packages,
            Invoice = new OrderPayload.InvoicePart
            {
                ExportType = row["export_type"], Currency = currency, ShippingFee = shippingFee > 0 ? Text(shippingFee) : "",
                DeclaredValue = value > 0 ? Text(value) : "", Items = items
            }
        };

        return new ImportedRow(row.Line, payload, errors.Distinct().ToList(), warnings)
        {
            GrossKg = ShippingRules.Round(gross),
            ChargeableKg = isDoc ? ShippingRules.Round(gross) : ShippingRules.Round(LegacyOrderFactory.ChargeableWeight(packages, gross, service, hub)),
            Value = ShippingRules.Round(value),
            CountryCode = country?.Code ?? code
        };
    }

    /// <summary>Các số thứ tự n của nhóm cột lặp có dữ liệu ở dòng này, tăng dần.</summary>
    private static IEnumerable<int> Indexes(SheetRow row, params string[] prefixes) =>
        row.Cells.Keys
            .Select(k => prefixes.Where(p => k.StartsWith(p, StringComparison.Ordinal))
                .Select(p => int.TryParse(k.AsSpan(p.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .FirstOrDefault(n => n > 0))
            .Where(n => n > 0)
            .Distinct()
            .Order();

    /// <summary>Đọc số (chấp nhận "2,5" và "1,000.50"). Sai hoặc ≤ 0 thì ghi lỗi, trả 0.</summary>
    private static decimal Number(SheetRow row, string column, List<string> errors, bool required, bool integer = false, bool allowZero = false)
    {
        var text = row[column];
        if (text.Length == 0)
        {
            if (required) errors.Add($"Thiếu {DisplayName(column)}");
            return 0;
        }
        if (!TryNumber(text, out var n) || n < 0 || (n == 0 && !allowZero))
        {
            errors.Add($"{DisplayName(column)} \"{text}\" phải là số {(allowZero ? "≥ 0" : "> 0")}");
            return 0;
        }
        if (integer && n != decimal.Truncate(n))
        {
            errors.Add($"{DisplayName(column)} \"{text}\" phải là số nguyên");
            return 0;
        }
        return n;
    }

    private static decimal OptionalNumber(SheetRow row, string column, List<string> errors) =>
        Number(row, column, errors, required: false, allowZero: true);

    public static bool TryNumber(string text, out decimal value)
    {
        var t = text.Trim().Replace(" ", "");
        t = t.Contains('.') ? t.Replace(",", "") : t.Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    private static decimal Num(string text) => TryNumber(text, out var n) ? n : 0;

    private static string Text(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Coalesce(string? first, string? fallback) =>
        string.IsNullOrWhiteSpace(first) ? fallback?.Trim() ?? "" : first.Trim();

    /// <summary>"cnee_country_code" → "Cnee_country_Code" (đúng tên cột trong file mẫu để khách dò).</summary>
    public static string DisplayName(string column) => column switch
    {
        "cnee_country_code" => "Cnee_country_Code",
        "cnee_company" => "Cnee_company",
        "cnee_contact_name" => "Cnee_contact_name",
        "cnee_tel" => "Cnee_Tel",
        "cnee_city" => "Cnee_City",
        "cnee_state" => "Cnee_State",
        "cnee_email" => "Cnee_Email",
        "add1" => "Add1",
        "add2" => "Add2",
        "type" => "Type",
        "description" => "Description",
        "currency" => "Currency",
        "export_type" => "Export_Type",
        "invoice_value" => "Invoice_Value",
        "shipping_fee" => "Shipping_fee",
        _ when column.StartsWith("hs_code_", StringComparison.Ordinal) => "HS_Code_" + column[8..],
        _ when column.StartsWith("gw_", StringComparison.Ordinal) => "GW_" + column[3..],
        _ when column.Length <= 3 && column[1] == '_' => char.ToUpperInvariant(column[0]) + column[1..],
        _ => string.Join('_', column.Split('_').Select((p, i) => i == 0 || p.Length > 3 ? char.ToUpperInvariant(p[0]) + p[1..] : p))
    };

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryCodePattern();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();

    [GeneratedRegex(@"^(\d{6}|\d{8}|\d{10})$")]
    private static partial Regex HsCodePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
