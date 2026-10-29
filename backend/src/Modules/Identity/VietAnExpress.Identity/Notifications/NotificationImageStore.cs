using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VietAnExpress.Identity.Staff;

namespace VietAnExpress.Identity.Notifications;

/// <summary>
/// Ảnh của tin trong thông báo: tệp trong <c>Upload/ThongBao</c> (cùng thư mục Upload với MyTracking), tên = mã băm SHA-256
/// nên tệp bất biến; SQL (Hinh_Anh trong cột JSON dbo.TThongBao.Tin) chỉ giữ đường dẫn "/Upload/ThongBao/{tên}". Không xóa tệp khi sửa / xóa thông báo
/// (email đã gửi vẫn trỏ tới ảnh).
/// </summary>
internal sealed partial class NotificationImageStore(IHostEnvironment environment, IConfiguration configuration)
{
    public const int MaxImageBytes = 2 * 1024 * 1024;
    public const string Folder = "ThongBao";
    /// <summary>Đường dẫn API công khai đọc ảnh (sau tiền tố /api/v1).</summary>
    public const string PublicPath = "/public/notifications/images/";

    private string Directory => Path.Combine(
        configuration["Notifications:UploadDirectory"] ?? configuration["MyTracking:UploadDirectory"] ?? Path.Combine(environment.ContentRootPath, "Upload"),
        Folder);

    /// <summary>Ghi ảnh JPG / PNG / WebP (kiểm tra chữ ký tệp) → đường dẫn lưu SQL; sai định dạng / quá lớn → <see cref="FormatException"/>.</summary>
    public async Task<string> SaveAsync(byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length is 0 or > MaxImageBytes) throw new FormatException("Ảnh trống hoặc lớn hơn 2 MB");
        var extension = Extension(bytes) ?? throw new FormatException("Chỉ nhận ảnh JPG, PNG hoặc WebP");
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes)) + extension;
        System.IO.Directory.CreateDirectory(Directory);
        var path = Path.Combine(Directory, name);
        if (!File.Exists(path))
        {
            var temporary = Path.Combine(Directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, bytes, ct);
                try { File.Move(temporary, path); }
                catch (IOException) when (File.Exists(path)) { }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return StoredPath(name);
    }

    /// <summary>Đường dẫn SQL hợp lệ và tệp có trên đĩa.</summary>
    public bool Exists(string storedPath) => NameOf(storedPath) is { } name && Find(name) is not null;

    public (string Path, string ContentType)? Find(string name)
    {
        if (!FileName().IsMatch(name)) return null;
        var path = Path.Combine(Directory, name);
        var contentType = Path.GetExtension(name).ToLowerInvariant() switch
        { ".jpg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => null };
        return contentType is not null && File.Exists(path) ? (path, contentType) : null;
    }

    public static string StoredPath(string name) => $"/Upload/{Folder}/{name}";

    /// <summary>"/Upload/ThongBao/{tên}" → tên tệp; đường dẫn khác → null.</summary>
    public static string? NameOf(string storedPath) =>
        storedPath.StartsWith($"/Upload/{Folder}/", StringComparison.Ordinal) && storedPath[($"/Upload/{Folder}/".Length)..] is var name && FileName().IsMatch(name)
            ? name : null;

    private static string? Extension(byte[] b) =>
        b.Length >= 3 && b[0] == 0xff && b[1] == 0xd8 && b[2] == 0xff ? ".jpg"
        : b.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? ".png"
        : b.Length >= 12 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? ".webp"
        : null;

    [GeneratedRegex(@"^[a-f0-9]{64}\.(?:jpg|png|webp)$")]
    private static partial Regex FileName();
}
