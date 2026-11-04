using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VietAnExpress.Identity.CustomerOtp;

/// <summary>
/// Tài khoản đã xác nhận email nhận OTP (tài khoản → email đã xác nhận + lúc đồng ý điều khoản). Không dùng bảng / cột DB mới (người dùng chọn
/// ghi đè cột Email có sẵn 04/11/2026): giữ trong bộ nhớ và file <c>App_Data/otp-email-confirmed.json</c> như danh sách token thu hồi.
/// Chưa có trong sổ, hoặc email hồ sơ đã đổi (CMS sửa) → lần đăng nhập tới phải nhập / xác nhận lại email. Mất file chỉ làm khách xác nhận lại.
/// </summary>
internal sealed class OtpEmailRegistry
{
    /// <summary>Email đã xác nhận (chữ thường) + lúc khách tick đồng ý xử lý dữ liệu cá nhân và phiên bản nội dung đã đồng ý (bằng chứng đồng ý).</summary>
    internal sealed record Entry(string Email, DateTimeOffset ConsentAt, string ConsentVersion);

    private readonly ConcurrentDictionary<string, Entry> _confirmed = new(StringComparer.Ordinal);
    private readonly Lock _fileLock = new();
    private readonly string _file;
    private readonly ILogger<OtpEmailRegistry> _logger;

    public OtpEmailRegistry(IHostEnvironment env, ILogger<OtpEmailRegistry> logger)
    {
        _file = Path.Combine(env.ContentRootPath, "App_Data", "otp-email-confirmed.json");
        _logger = logger;
        Load();
    }

    /// <summary>Khóa tài khoản: "c:{CustomerID}" tài khoản chính, "s:{ID}" tài khoản con.</summary>
    public static string Key(long customerId, long? staffId) => staffId is { } sid ? $"s:{sid}" : $"c:{customerId}";

    /// <summary>Đã xác nhận đúng email đang lưu ở hồ sơ.</summary>
    public bool IsConfirmed(string key, string? currentEmail) =>
        currentEmail is not null && _confirmed.TryGetValue(key, out var e) && string.Equals(e.Email, currentEmail.Trim(), StringComparison.OrdinalIgnoreCase);

    public void Confirm(string key, string email, DateTimeOffset consentAt, string consentVersion)
    {
        _confirmed[key] = new Entry(email.Trim().ToLowerInvariant(), consentAt, consentVersion);
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            foreach (var (key, entry) in JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(_file)) ?? [])
                _confirmed[key] = entry;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Không đọc được sổ email nhận OTP {File}", _file);
        }
    }

    private void Save()
    {
        lock (_fileLock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
                var temp = _file + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(_confirmed));
                File.Move(temp, _file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Không ghi được sổ email nhận OTP {File} — chỉ còn trong bộ nhớ tới khi khởi động lại", _file);
            }
        }
    }
}
