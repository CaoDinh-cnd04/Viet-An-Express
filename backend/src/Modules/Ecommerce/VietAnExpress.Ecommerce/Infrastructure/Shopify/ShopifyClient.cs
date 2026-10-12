using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VietAnExpress.Ecommerce.Infrastructure.Shopify;

/// <summary>Token Shopify vừa nhận. Token có hạn (expiring=1) kèm refresh token 90 ngày.</summary>
internal sealed record ShopifyToken(string AccessToken, string Scopes, int? ExpiresIn, string? RefreshToken, int? RefreshTokenExpiresIn);

internal sealed record ShopifyShopInfo(string Name, string? Currency, string? PrimaryDomain);

/// <summary>Gọi Shopify Admin API của 1 shop (host luôn là xxx.myshopify.com đã kiểm bằng <see cref="ShopifyOAuth.NormalizeShop"/>).</summary>
internal sealed class ShopifyClient(HttpClient http, IOptions<ShopifyOptions> options, ILogger<ShopifyClient> logger)
{
    private ShopifyOptions O => options.Value;

    /// <summary>Đổi authorization code lấy token (code dùng 1 lần). Shopify từ chối → Token null, Error là error_description của Shopify.</summary>
    public async Task<(ShopifyToken? Token, string? Error)> ExchangeCodeAsync(string shop, string code, CancellationToken ct)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = O.ClientId,
            ["client_secret"] = O.ClientSecret,
            ["code"] = code,
            ["expiring"] = "1"
        });
        return await RequestTokenAsync(shop, body, ct);
    }

    private async Task<(ShopifyToken? Token, string? Error)> RequestTokenAsync(string shop, HttpContent body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{shop}/admin/oauth/access_token") { Content = body };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var res = await http.SendAsync(req, ct);
        TokenResponse? t = null;
        try { t = await res.Content.ReadFromJsonAsync<TokenResponse>(ct); }
        catch (JsonException) { } // Shopify đôi khi trả trang lỗi HTML
        if (res.IsSuccessStatusCode && t?.AccessToken is { Length: > 0 } access)
            return (new ShopifyToken(access, t.Scope ?? "", t.ExpiresIn, t.RefreshToken, t.RefreshTokenExpiresIn), null);

        var error = t?.ErrorDescription ?? t?.Error ?? $"HTTP {(int)res.StatusCode}";
        logger.LogWarning("Shopify từ chối cấp token cho {Shop}: HTTP {Status} {Error}", shop, (int)res.StatusCode, error);
        return (null, error);
    }

    /// <summary>Đổi refresh token lấy cặp token mới. Token null → refresh token hết hạn / bị thu hồi, khách phải ủy quyền lại.</summary>
    public async Task<(ShopifyToken? Token, string? Error)> RefreshAsync(string shop, string refreshToken, CancellationToken ct)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = O.ClientId,
            ["client_secret"] = O.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });
        return await RequestTokenAsync(shop, body, ct);
    }

    /// <summary>Đơn đang mở, chưa giao (tối đa <paramref name="maxPages"/> × 50 đơn). Error: lỗi HTTP / GraphQL của Shopify.</summary>
    public async Task<(IReadOnlyList<JsonElement> Orders, string? Error)> GetOpenOrdersAsync(string shop, string accessToken, int maxPages, CancellationToken ct)
    {
        var orders = new List<JsonElement>();
        string? after = null;
        for (var page = 0; page < maxPages; page++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{shop}/admin/api/{O.ApiVersion}/graphql.json")
            {
                Content = JsonContent.Create(new { query = ShopifyOrderMapper.OpenOrdersQuery, variables = new { after } })
            };
            req.Headers.Add("X-Shopify-Access-Token", accessToken);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("Shopify trả HTTP {Status} khi đọc đơn của {Shop}", (int)res.StatusCode, shop);
                return (orders, res.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "unauthorized" : $"HTTP {(int)res.StatusCode}");
            }

            using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;
            var conn = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("orders", out var o) && o.ValueKind == JsonValueKind.Object ? o : (JsonElement?)null;
            if (conn is null)
            {
                var errors = root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array
                    ? string.Join("; ", e.EnumerateArray().Select(x => x.TryGetProperty("message", out var m) ? m.GetString() : null).OfType<string>())
                    : "không đọc được danh sách đơn";
                logger.LogWarning("Shopify GraphQL lỗi khi đọc đơn của {Shop}: {Errors}", shop, errors);
                return (orders, errors);
            }

            // Có data nhưng kèm errors: thường là app chưa được duyệt "Protected customer data" → Shopify trả null cho
            // shippingAddress / email / phone (đơn về thiếu người nhận). Ghi log để thấy lý do.
            if (root.TryGetProperty("errors", out var partial) && partial.ValueKind == JsonValueKind.Array && partial.GetArrayLength() > 0)
                logger.LogWarning("Shopify GraphQL trả đơn của {Shop} kèm lỗi (có thể thiếu dữ liệu người nhận): {Errors}", shop,
                    string.Join("; ", partial.EnumerateArray().Select(x => x.TryGetProperty("message", out var m) ? m.GetString() : null).OfType<string>()));

            orders.AddRange(conn.Value.GetProperty("nodes").EnumerateArray().Select(n => n.Clone()));
            var info = conn.Value.GetProperty("pageInfo");
            if (!info.GetProperty("hasNextPage").GetBoolean()) break;
            after = info.GetProperty("endCursor").GetString();
        }
        return (orders, null);
    }

    public async Task<ShopifyShopInfo?> GetShopAsync(string shop, string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{shop}/admin/api/{O.ApiVersion}/graphql.json")
        {
            Content = JsonContent.Create(new { query = "{ shop { name currencyCode primaryDomain { host } } }" })
        };
        req.Headers.Add("X-Shopify-Access-Token", accessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;

        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("shop", out var s)) return null;
        return new ShopifyShopInfo(
            s.GetProperty("name").GetString() ?? shop,
            s.TryGetProperty("currencyCode", out var c) ? c.GetString() : null,
            s.TryGetProperty("primaryDomain", out var d) && d.ValueKind == JsonValueKind.Object ? d.GetProperty("host").GetString() : null);
    }

    /// <summary>
    /// Gỡ app khỏi shop bằng mutation GraphQL <c>appUninstall</c> (thu hồi token, Shopify tự hủy webhook).
    /// Lỗi chỉ ghi log — ngắt kết nối phía Việt An vẫn tiếp tục.
    /// </summary>
    public async Task RevokeAsync(string shop, string accessToken, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{shop}/admin/api/{O.ApiVersion}/graphql.json")
            {
                Content = JsonContent.Create(new { query = "mutation { appUninstall { userErrors { field message } } }" })
            };
            req.Headers.Add("X-Shopify-Access-Token", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode || body.Contains("\"errors\"", StringComparison.Ordinal) || body.Contains("\"message\"", StringComparison.Ordinal))
                logger.LogWarning("Không gỡ được app khỏi {Shop}: HTTP {Status} {Body}", shop, (int)res.StatusCode, body.Length > 300 ? body[..300] : body);
        }
        catch (HttpRequestException e)
        {
            logger.LogWarning(e, "Không gỡ được app khỏi {Shop}", shop);
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("refresh_token_expires_in")] int? RefreshTokenExpiresIn,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);
}
