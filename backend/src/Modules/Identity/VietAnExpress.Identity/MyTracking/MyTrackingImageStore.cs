using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VietAnExpress.Identity.Staff;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>Ảnh lưu thành tệp trong Upload, SQL chỉ lưu đường dẫn như hệ thống cũ.</summary>
internal sealed partial class MyTrackingImageStore(IHostEnvironment environment, IConfiguration configuration, IHttpContextAccessor http)
{
    public const int MaxImageBytes = 200 * 1024;
    public const string ApiPath = "/api/v1/public/mytracking/images/";
    private string UploadDirectory => configuration["MyTracking:UploadDirectory"]
        ?? Path.Combine(environment.ContentRootPath, "Upload");
    private string ApiBaseUrl => (configuration["MyTracking:PublicApiBaseUrl"]
        ?? (http.HttpContext is { } context ? $"{context.Request.Scheme}://{context.Request.Host}" : "")).TrimEnd('/');

    public async Task<string> PrepareAsync(string json, CancellationToken ct)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        foreach (var (item, key, folder) in Images(root))
        {
            if (item[key] is not JsonValue value || !value.TryGetValue<string>(out var source)) continue;
            source = CanonicalSource(source);
            if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) { item[key] = source; continue; }
            var comma = source.IndexOf(',');
            var extension = comma < 0 ? null : source[..comma].ToLowerInvariant() switch
            {
                "data:image/jpeg;base64" => ".jpg",
                "data:image/png;base64" => ".png",
                "data:image/webp;base64" => ".webp",
                _ => null
            };
            if (extension is null) throw new FormatException("Chỉ nhận ảnh JPG, PNG hoặc WebP");
            var bytes = Convert.FromBase64String(source[(comma + 1)..]);
            if (bytes.Length is 0 or > MaxImageBytes || !HasSignature(bytes, extension))
                throw new FormatException("Ảnh không hợp lệ hoặc vượt quá 200 KB");
            var name = Convert.ToHexStringLower(SHA256.HashData(bytes)) + extension;
            var directory = Path.Combine(UploadDirectory, folder);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
            {
                var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    await File.WriteAllBytesAsync(temporary, bytes, ct);
                    try { File.Move(temporary, path); }
                    catch (IOException) when (File.Exists(path)) { }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            item[key] = $"/Upload/{folder}/{name}";
        }
        return root.ToJsonString();
    }

    /// <summary>Ảnh do API giữ được phục vụ từ API; đường dẫn ảnh cũ vẫn để frontend ghép với tên miền cũ.</summary>
    public string ForDisplay(string json)
    {
        if (string.IsNullOrEmpty(ApiBaseUrl)) return json;
        var root = JsonNode.Parse(json)!.AsObject();
        foreach (var (item, key, _) in Images(root))
            if (item[key] is JsonValue value && value.TryGetValue<string>(out var source)
                && StoredPath().Match(source) is { Success: true } match && Find(match.Groups[1].Value, match.Groups[2].Value) is not null)
                item[key] = ApiBaseUrl + ApiPath + match.Groups[1].Value + "/" + match.Groups[2].Value;
        return root.ToJsonString();
    }

    public (string Path, string ContentType)? Find(string folder, string name)
    {
        if (!StoredPath().IsMatch($"/Upload/{folder}/{name}")) return null;
        var path = Path.GetFullPath(Path.Combine(UploadDirectory, folder, name));
        var contentType = Path.GetExtension(name).ToLowerInvariant() switch
        { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => null };
        return contentType is not null && File.Exists(path) ? (path, contentType) : null;
    }

    private string CanonicalSource(string source)
    {
        source = source.Trim();
        if (!Uri.TryCreate(source, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https")) return source;
        if (url.AbsolutePath.StartsWith(ApiPath, StringComparison.Ordinal)
            && Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var api) && url.Authority.Equals(api.Authority, StringComparison.OrdinalIgnoreCase))
        {
            var stored = "/Upload/" + url.AbsolutePath[ApiPath.Length..];
            if (StoredPath().IsMatch(stored)) return stored;
        }
        var legacy = new Uri(configuration["MyTracking:LegacyAssetBaseUrl"] ?? "https://mytracking.top");
        return url.Authority.Equals(legacy.Authority, StringComparison.OrdinalIgnoreCase)
            && url.AbsolutePath.StartsWith("/Upload/", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(url.Query)
            ? url.AbsolutePath : source;
    }

    private static IEnumerable<(JsonObject Item, string Key, string Folder)> Images(JsonObject root)
    {
        yield return (root, "background", "Banner");
        if (root["brand"] is JsonObject brand) yield return (brand, "logo", "Logo_ShippingMark");
        if (root["images"] is JsonArray images)
            foreach (var image in images.OfType<JsonObject>()) yield return (image, "src", "Banner");
    }

    private static bool HasSignature(byte[] bytes, string extension) => extension switch
    {
        ".jpg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        ".png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    [GeneratedRegex(@"^/Upload/(Banner|Logo_ShippingMark)/([a-zA-Z0-9_-]+\.(?:jpg|jpeg|png|webp))$", RegexOptions.IgnoreCase)]
    private static partial Regex StoredPath();
}
