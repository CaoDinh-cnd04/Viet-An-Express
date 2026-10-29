using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.Geo.Geoapify;
using VietAnExpress.Shipments.Geo.GeoNames;
using Xunit;

namespace VietAnExpress.Shipments.Tests.Application;

public class GeoParsersTests
{
    [Fact]
    public void Doc_danh_sach_quoc_gia_va_ma_dien_thoai()
    {
        // Rút gọn đúng định dạng REST Countries v3.1
        const string json = """
            [
              { "name": { "common": "Vietnam" }, "cca2": "VN", "idd": { "root": "+8", "suffixes": ["4"] } },
              { "name": { "common": "United States" }, "cca2": "US", "idd": { "root": "+1", "suffixes": ["201", "202", "203"] } },
              { "name": { "common": "Antarctica" }, "cca2": "AQ", "idd": {} },
              { "name": { "common": "" }, "cca2": "XX", "idd": { "root": "+9" } }
            ]
            """;

        var countries = GeoParsers.ParseCountries(JsonDocument.Parse(json));

        Assert.Equal(["AQ", "US", "VN"], countries.Select(c => c.Code));   // sắp theo tên, bỏ dòng thiếu tên
        Assert.Equal("+84", countries.Single(c => c.Code == "VN").DialCode); // 1 suffix → mã đầy đủ
        Assert.Equal("+1", countries.Single(c => c.Code == "US").DialCode);  // nhiều suffix (mã vùng) → chỉ root
        Assert.Null(countries.Single(c => c.Code == "AQ").DialCode);
    }

    [Theory]
    [InlineData("us", " sw1a   1aa ", "US", "SW1A 1AA")]
    [InlineData(" gb ", "h3z2y7", "GB", "H3Z2Y7")]
    public void Chuan_hoa_ma_buu_chinh(string country, string postal, string cc, string expected) =>
        Assert.Equal((cc, expected), GeoParsers.NormalizePostal(country, postal));

    [Theory]
    [InlineData("USA")]
    [InlineData("U1")]
    public void Ma_nuoc_sai_thi_bo(string country) => Assert.Null(GeoParsers.NormalizePostal(country, "10002"));

    [Theory]
    [InlineData("US", "10002", new[] { "10002" })]
    [InlineData("GB", "SW1A 1AA", new[] { "SW1A 1AA", "SW1A" })]
    [InlineData("GB", "SW1A1AA", new[] { "SW1A1AA", "SW1A" })]
    [InlineData("CA", "H0H0H0", new[] { "H0H0H0", "H0H" })]
    [InlineData("CA", "H0H 0H0", new[] { "H0H 0H0", "H0H" })]
    [InlineData("JP", "100-0001", new[] { "100-0001" })]
    public void Thu_ma_day_du_roi_phan_dau(string country, string postal, string[] expected) =>
        Assert.Equal(expected, GeoParsers.PostalCandidates(country, postal));
}

public class GeoNamesParserTests
{
    [Fact]
    public void Doc_ket_qua_postalCodeLookupJSON()
    {
        // Rút gọn đúng phản hồi thật của GeoNames cho US/10002
        const string json = """
            { "postalcodes": [ { "adminCode2": "061", "adminCode1": "NY", "adminName2": "New York", "lng": -73.98,
                                 "countryCode": "US", "postalcode": "10002", "adminName1": "New York", "placeName": "New York", "lat": 40.71 } ] }
            """;

        var info = GeoNamesParser.Postal(JsonDocument.Parse(json), "US", "10002")!;
        Assert.Equal(new PostalInfo("US", "10002", "New York", "New York", "NY") { Places = info.Places }, info);
        Assert.Equal([new PostalPlace("New York", "New York", "NY", null)], info.Places);
        Assert.Null(GeoNamesParser.Postal(JsonDocument.Parse("""{ "postalcodes": [] }"""), "US", "00000"));
        Assert.Null(GeoNamesParser.Postal(JsonDocument.Parse("[]"), "US", "00000"));
    }

    [Fact]
    public void Singapore_khong_co_bang_van_doc_duoc()
    {
        var info = GeoNamesParser.Postal(JsonDocument.Parse("""{ "postalcodes": [ { "countryCode": "SG", "postalcode": "238859", "placeName": "Orchard Road" } ] }"""), "SG", "238859")!;
        Assert.Equal(("Orchard Road", (string?)null, (string?)null), (info.City, info.State, info.StateCode));
    }

    [Fact]
    public void Mexico_lay_dung_truong_GeoNames_colonia_va_quan_thanh_khu_vuc()
    {
        // Rút gọn phản hồi thật của GeoNames cho MX/16090: 4 colonia cùng thuộc Ciudad de México (quận Xochimilco)
        const string json = """
            { "postalcodes": [
              { "placeName": "Barrio San Pedro", "adminName1": "Distrito Federal", "adminCode1": "DIF", "adminName2": "Xochimilco", "adminName3": "Ciudad de México" },
              { "placeName": "Barrio Xaltocan", "adminName1": "Distrito Federal", "adminCode1": "DIF", "adminName2": "Xochimilco", "adminName3": "Ciudad de México" },
              { "placeName": "Caltongo", "adminName1": "Distrito Federal", "adminCode1": "DIF", "adminName2": "Xochimilco", "adminName3": "Ciudad de México" } ] }
            """;
        var info = GeoNamesParser.Postal(JsonDocument.Parse(json), "MX", "16090")!;

        // Đúng nguyên trường GeoNames: adminName2 (municipio) / adminName1 / placeName (colonia)
        Assert.Equal(("Xochimilco", "Distrito Federal"), (info.City, info.State));
        Assert.Equal(["Barrio San Pedro", "Barrio Xaltocan", "Caltongo"], info.Places.Select(p => p.Area));
        Assert.All(info.Places, p => Assert.Equal("Xochimilco", p.City));
    }

    [Fact]
    public void Mexico_bang_khac_giu_thanh_pho_kem_quan_vao_khu_vuc()
    {
        const string json = """
            { "postalcodes": [ { "placeName": "Ciudad Granja", "adminName1": "Jalisco", "adminName2": "Zapopan", "adminName3": "Zapopan" },
                               { "placeName": "Americana", "adminName1": "Jalisco", "adminName2": "Guadalajara", "adminName3": "Guadalajara" } ] }
            """;
        var info = GeoNamesParser.Postal(JsonDocument.Parse(json), "MX", "45010")!;
        Assert.Equal(["Zapopan", "Guadalajara"], info.Places.Select(p => p.City));
        Assert.Equal(["Ciudad Granja", "Americana"], info.Places.Select(p => p.Area));
    }

    [Fact]
    public void An_Do_khong_co_adminName3_thi_lay_adminName2()
    {
        const string json = """
            { "postalcodes": [ { "placeName": "Connaught Place", "adminName1": "Delhi", "adminName2": "Central Delhi" } ] }
            """;
        Assert.Equal(new PostalPlace("Central Delhi", "Delhi", null, "Connaught Place"), GeoNamesParser.Postal(JsonDocument.Parse(json), "IN", "110001")!.Places.Single());
    }

    [Fact]
    public void Ma_gom_nhieu_thanh_pho_tra_du_danh_sach_bo_dong_trung()
    {
        // FR/01000 gồm 2 xã khác nhau; GeoNames hay trả trùng dòng (CA/M5V: 30 dòng "Toronto")
        const string json = """
            { "postalcodes": [
              { "placeName": "Bourg-en-Bresse", "adminName1": "Auvergne-Rhône-Alpes", "adminCode1": "84" },
              { "placeName": "Saint-Denis-lès-Bourg", "adminName1": "Auvergne-Rhône-Alpes", "adminCode1": "84" },
              { "placeName": "Bourg-en-Bresse", "adminName1": "Auvergne-Rhône-Alpes", "adminCode1": "84" } ] }
            """;
        var info = GeoNamesParser.Postal(JsonDocument.Parse(json), "FR", "01000")!;

        Assert.Equal("Bourg-en-Bresse", info.City);
        Assert.Equal(["Bourg-en-Bresse", "Saint-Denis-lès-Bourg"], info.Places.Select(p => p.City));
        Assert.All(info.Places, p => Assert.Null(p.Area));
    }

    [Fact]
    public void Doc_goi_y_postalCodeSearchJSON()
    {
        const string json = """
            { "postalCodes": [
              { "postalCode": "16090", "placeName": "Caltongo", "adminName1": "Distrito Federal", "adminName2": "Xochimilco", "adminName3": "Ciudad de México" },
              { "postalCode": "16095", "placeName": "Delegación Política Xochimilco", "adminName1": "Distrito Federal", "adminName2": "Xochimilco", "adminName3": "Ciudad de México" },
              { "postalCode": "16090", "placeName": "Caltongo", "adminName1": "Distrito Federal", "adminName2": "Xochimilco", "adminName3": "Ciudad de México" } ] }
            """;
        Assert.Equal(
            [new PostalSuggestion("16090", "Xochimilco", "Distrito Federal", null, "Caltongo"),
             new PostalSuggestion("16095", "Xochimilco", "Distrito Federal", null, "Delegación Política Xochimilco")],
            GeoNamesParser.Suggestions(JsonDocument.Parse(json), "MX"));
    }

    [Fact]
    public void Doc_loi_GeoNames()
    {
        Assert.Equal("user does not exist.", GeoNamesParser.Error(JsonDocument.Parse("""{ "status": { "message": "user does not exist.", "value": 10 } }""")));
        Assert.Null(GeoNamesParser.Error(JsonDocument.Parse("""{ "postalcodes": [] }""")));
    }
}

internal sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(respond(request));
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

public class GeoNamesPostalCodeProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GeoNamesPostalCodeProvider Provider(FakeHandler handler, string username = "vietanexpress") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://secure.geonames.org/") },
            Options.Create(new GeoNamesOptions { Username = username }), NullLogger<GeoNamesPostalCodeProvider>.Instance);

    [Fact]
    public async Task Goi_dung_endpoint_va_tham_so()
    {
        var handler = new FakeHandler(req =>
        {
            Assert.Equal("https://secure.geonames.org/postalCodeLookupJSON?postalcode=SW1A%201AA&country=GB&maxRows=30&username=vietanexpress",
                req.RequestUri!.AbsoluteUri);
            return FakeHandler.Json("""{ "postalcodes": [ { "placeName": "London", "adminName1": "England", "adminCode1": "ENG" } ] }""");
        });

        var result = await Provider(handler).LookupAsync("GB", "SW1A 1AA", Ct);

        Assert.Equal(PostalLookupStatus.Found, result.Status);
        Assert.Equal("London", result.Info!.City);
    }

    [Fact]
    public async Task Khong_co_du_lieu_la_NotFound() =>
        Assert.Equal(PostalLookupStatus.NotFound,
            (await Provider(new FakeHandler(_ => FakeHandler.Json("""{ "postalcodes": [] }"""))).LookupAsync("VN", "700000", Ct)).Status);

    [Theory]
    [InlineData("""{ "status": { "message": "the daily limit of 20000 credits has been exceeded", "value": 18 } }""", HttpStatusCode.OK)]
    [InlineData("{}", HttpStatusCode.ServiceUnavailable)]
    [InlineData("not json", HttpStatusCode.OK)]
    public async Task Loi_tai_khoan_het_luot_hoac_may_chu_loi_la_Unavailable(string body, HttpStatusCode status) =>
        Assert.Equal(PostalLookupStatus.Unavailable,
            (await Provider(new FakeHandler(_ => FakeHandler.Json(body, status))).LookupAsync("US", "10002", Ct)).Status);

    [Fact]
    public async Task Chua_cau_hinh_tai_khoan_thi_khong_goi_ra_ngoai()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException("không được gọi"));

        Assert.Equal(PostalLookupStatus.Unavailable, (await Provider(handler, username: " ").LookupAsync("US", "10002", Ct)).Status);
        Assert.Equal(0, handler.Calls);
    }
}

public class GeoLookupServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Nguồn mã bưu chính giả: trả theo bảng, ghi lại các mã đã hỏi.</summary>
    private sealed class FakeProvider(Func<string, PostalLookupResult> respond, Func<string, PostalSearchResult>? search = null) : IPostalCodeProvider
    {
        public List<string> Asked { get; } = [];
        public List<string> Searched { get; } = [];

        public Task<PostalLookupResult> LookupAsync(string countryCode, string postalCode, CancellationToken cancellationToken)
        {
            Asked.Add($"{countryCode}/{postalCode}");
            return Task.FromResult(respond(postalCode));
        }

        public Task<PostalSearchResult> SearchAsync(string countryCode, string prefix, CancellationToken cancellationToken)
        {
            Searched.Add($"{countryCode}/{prefix}");
            return Task.FromResult(search?.Invoke(prefix) ?? new PostalSearchResult(true, []));
        }
    }

    private static PostalLookupResult Found(string city) => PostalLookupResult.Found(new PostalInfo("XX", "?", city, null, null));

    /// <summary>Nguồn gợi ý địa chỉ giả: trả theo hàm, ghi lại các lần hỏi.</summary>
    private sealed class FakeAddressProvider(Func<string, AddressSuggestionResult> respond) : IAddressSuggestionProvider
    {
        public List<string> Asked { get; } = [];

        public Task<AddressSuggestionResult> SuggestAsync(string countryCode, string query, CancellationToken cancellationToken)
        {
            Asked.Add($"{countryCode}/{query}");
            return Task.FromResult(respond(query));
        }
    }

    private static GeoLookupService Service(IPostalCodeProvider provider, IAddressSuggestionProvider? addresses = null) =>
        new(new HttpClient(new FakeHandler(_ => throw new HttpRequestException("mất mạng"))), provider,
            addresses ?? new FakeAddressProvider(_ => AddressSuggestionResult.Unavailable),
            new MemoryCache(new MemoryCacheOptions()), NullLogger<GeoLookupService>.Instance);

    [Fact]
    public async Task Goi_y_ma_buu_chinh_chuan_hoa_va_cache()
    {
        var hit = new PostalSuggestion("16090", "Xochimilco", "Distrito Federal", "09", "Caltongo");
        var provider = new FakeProvider(_ => PostalLookupResult.NotFound, _ => new PostalSearchResult(true, [hit]));
        var geo = Service(provider);

        Assert.Equal([hit], await geo.SearchPostalAsync("mx", " 1609 ", Ct));
        Assert.Equal([hit], await geo.SearchPostalAsync("MX", "1609", Ct));
        Assert.Equal(["MX/1609"], provider.Searched); // lần 2 lấy từ cache
        Assert.Empty(await geo.SearchPostalAsync("MX", "1", Ct)); // quá ngắn
    }

    [Fact]
    public async Task Goi_y_khong_co_ma_bat_dau_nhu_vay_thi_tra_dung_ma()
    {
        var info = new PostalInfo("GB", "SW1A", "London", "England", "ENG") { Places = [new PostalPlace("London", "England", "ENG", null)] };
        var provider = new FakeProvider(postal => postal == "SW1A" ? PostalLookupResult.Found(info) : PostalLookupResult.NotFound);

        var items = await Service(provider).SearchPostalAsync("GB", "SW1A 1AA", Ct);

        Assert.Equal([new PostalSuggestion("SW1A 1AA", "London", "England", "ENG", null)], items);
    }

    [Fact]
    public async Task Goi_y_nguon_loi_thi_rong_khong_cache()
    {
        var provider = new FakeProvider(_ => PostalLookupResult.NotFound, _ => PostalSearchResult.Unavailable);
        var geo = Service(provider);
        Assert.Empty(await geo.SearchPostalAsync("US", "1000", Ct));
        Assert.Empty(await geo.SearchPostalAsync("US", "1000", Ct));
        Assert.Equal(2, provider.Searched.Count);
    }

    private static readonly AddressSuggestion Austin = new("123 Main St, Austin, TX 78701", "123 Main St", "Austin", "Texas", "TX", "78701", "US");

    [Fact]
    public async Task Goi_y_dia_chi_chuan_hoa_chu_go_va_cache()
    {
        var addresses = new FakeAddressProvider(_ => AddressSuggestionResult.Found([Austin]));
        var geo = Service(new FakeProvider(_ => PostalLookupResult.NotFound), addresses);

        var first = await geo.SuggestAddressesAsync("us", "  123   Main St ", Ct);
        var second = await geo.SuggestAddressesAsync("US", "123 main st", Ct);

        Assert.Equal([Austin], first);
        Assert.Equal(first, second);
        Assert.Equal(["US/123 Main St"], addresses.Asked); // lần 2 (khác hoa thường) lấy từ cache
    }

    [Theory]
    [InlineData("USA", "123 Main St")]
    [InlineData("US", "12")]
    [InlineData("US", "   ")]
    public async Task Goi_y_dia_chi_chu_qua_ngan_hoac_sai_nuoc_thi_khong_hoi(string country, string query)
    {
        var addresses = new FakeAddressProvider(_ => throw new InvalidOperationException("không được gọi"));
        Assert.Empty(await Service(new FakeProvider(_ => PostalLookupResult.NotFound), addresses).SuggestAddressesAsync(country, query, Ct));
        Assert.Empty(addresses.Asked);
    }

    [Fact]
    public async Task Goi_y_dia_chi_nguon_loi_thi_khong_cache()
    {
        var addresses = new FakeAddressProvider(_ => AddressSuggestionResult.Unavailable);
        var geo = Service(new FakeProvider(_ => PostalLookupResult.NotFound), addresses);

        Assert.Empty(await geo.SuggestAddressesAsync("US", "123 Main", Ct));
        Assert.Empty(await geo.SuggestAddressesAsync("US", "123 Main", Ct));
        Assert.Equal(2, addresses.Asked.Count);
    }

    [Fact]
    public async Task Chuan_hoa_roi_hoi_nguon_va_cache_lai()
    {
        var provider = new FakeProvider(_ => Found("New York"));
        var geo = Service(provider);

        var first = await geo.LookupPostalAsync("us", " 10002 ", Ct);
        var second = await geo.LookupPostalAsync("US", "10002", Ct);

        Assert.Equal("New York", first!.City);
        Assert.Equal("10002", first.PostalCode);
        Assert.Equal(first, second);
        Assert.Equal(["US/10002"], provider.Asked); // lần 2 lấy từ cache
    }

    [Fact]
    public async Task Ma_day_du_khong_co_thi_thu_phan_dau()
    {
        var provider = new FakeProvider(postal => postal == "SW1A" ? Found("London") : PostalLookupResult.NotFound);

        var info = await Service(provider).LookupPostalAsync("gb", "sw1a  1aa", Ct);

        Assert.Equal("London", info!.City);
        Assert.Equal("SW1A 1AA", info.PostalCode); // giữ mã khách gõ
        Assert.Equal(["GB/SW1A 1AA", "GB/SW1A"], provider.Asked);
    }

    [Fact]
    public async Task Khong_tim_thay_thi_cache_khong_hoi_lai()
    {
        var provider = new FakeProvider(_ => PostalLookupResult.NotFound);
        var geo = Service(provider);

        Assert.Null(await geo.LookupPostalAsync("VN", "700000", Ct));
        Assert.Null(await geo.LookupPostalAsync("VN", "700000", Ct));
        Assert.Single(provider.Asked);
    }

    [Fact]
    public async Task Nguon_loi_thi_khong_cache_lan_sau_thu_lai()
    {
        var provider = new FakeProvider(_ => PostalLookupResult.Unavailable);
        var geo = Service(provider);

        Assert.Null(await geo.LookupPostalAsync("US", "10002", Ct));
        Assert.Null(await geo.LookupPostalAsync("US", "10002", Ct));
        Assert.Equal(2, provider.Asked.Count);
    }

    [Theory]
    [InlineData("USA", "94526")]
    [InlineData("US", "9")]
    [InlineData("US", "94526/../x")]
    public async Task Du_lieu_nhap_sai_khong_hoi_nguon(string country, string postal)
    {
        var provider = new FakeProvider(_ => throw new InvalidOperationException("không được gọi"));

        Assert.Null(await Service(provider).LookupPostalAsync(country, postal, Ct));
        Assert.Empty(provider.Asked);
    }

    [Fact]
    public async Task Danh_sach_nuoc_loi_thi_tra_rong_khong_nem_loi() =>
        Assert.Empty(await Service(new FakeProvider(_ => PostalLookupResult.NotFound)).GetCountriesAsync(Ct));
}

public class GeoapifyParserTests
{
    [Fact]
    public void Doc_goi_y_dia_chi_dien_dia_chi_thanh_pho_tinh_ma_buu_chinh()
    {
        // Rút gọn đúng định dạng Geoapify autocomplete (format=json)
        const string json = """
            { "results": [
              { "country_code": "us", "housenumber": "123", "street": "Main Street", "city": "Austin", "state": "Texas", "state_code": "TX",
                "postcode": "78701", "address_line1": "123 Main Street", "formatted": "123 Main Street, Austin, TX 78701, United States of America",
                "result_type": "building" },
              { "country_code": "de", "housenumber": "5", "street": "Hauptstraße", "city": "Berlin", "state": "Berlin", "postcode": "10115",
                "address_line1": "Café Mitte", "formatted": "Café Mitte, Hauptstraße 5, 10115 Berlin, Germany", "result_type": "amenity" },
              { "country_code": "us", "city": "Austin", "state": "Texas", "state_code": "TX", "postcode": "78701",
                "address_line1": "Austin", "formatted": "Austin, TX, United States of America", "result_type": "city" },
              { "country_code": "ca", "city": "Toronto", "formatted": "Toronto, ON, Canada", "result_type": "city" },
              { "country_code": "us", "street": "Main Street", "city": "Austin", "address_line1": "Main Street",
                "result_type": "street" }
            ] }
            """;

        var us = GeoapifyParser.Suggestions(JsonDocument.Parse(json), "US");

        Assert.Equal(2, us.Count); // bỏ kết quả nước khác và kết quả thiếu formatted
        Assert.Equal(new AddressSuggestion("123 Main Street, Austin, TX 78701, United States of America", "123 Main Street", "Austin", "Texas", "TX", "78701", "US"), us[0]);
        Assert.Equal("", us[1].Address1); // gợi ý cấp thành phố không điền ô địa chỉ

        var de = GeoapifyParser.Suggestions(JsonDocument.Parse(json), "de").Single();
        Assert.Equal("Hauptstraße 5", de.Address1); // địa điểm có tên → ghép đường + số nhà theo thứ tự của nước đó
    }

    [Fact]
    public void Dia_chi_Nhat_giu_so_khoi_nha_khi_khong_co_so_nha()
    {
        // Geoapify trả địa chỉ Nhật dạng amenity: address_line1 = số khối, không có housenumber.
        const string json = """
            { "results": [ { "country_code": "jp", "street": "Atago Hitoikizaka", "address_line1": "2-1-1", "city": "Tama", "state": "Tokyo",
                             "postcode": "206-0014", "formatted": "2-1-1, Atago Hitoikizaka, Tama, TK 206-0014, Japan", "result_type": "amenity" } ] }
            """;

        var jp = GeoapifyParser.Suggestions(JsonDocument.Parse(json), "JP").Single();

        Assert.Equal(("2-1-1, Atago Hitoikizaka", "Tama", "Tokyo", "206-0014"), (jp.Address1, jp.City, jp.State, jp.PostalCode));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "results": {} }""")]
    [InlineData("[]")]
    public void Du_lieu_la_thi_tra_rong(string json) => Assert.Empty(GeoapifyParser.Suggestions(JsonDocument.Parse(json), "US"));
}
