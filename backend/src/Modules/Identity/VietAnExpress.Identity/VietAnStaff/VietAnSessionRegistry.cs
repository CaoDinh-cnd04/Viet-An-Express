using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Nhân viên Việt An chỉ 1 phiên đăng nhập tại một thời điểm (người dùng chốt 04/11/2026: máy mới đá máy cũ ra).
/// Mỗi nhân viên có 1 mã phiên hiện hành; đăng nhập xong (đúng mật khẩu + OTP) thì cấp mã mới. Mã phiên ghép vào "dấu mật khẩu" của
/// refresh token (<see cref="Auth.SessionService.VietAnStamp"/>) → token của máy cũ không làm mới / chọn khách được nữa
/// (máy cũ bị đăng xuất khi access token 5 phút hết hạn hoặc khi mở lại trang).
/// Không dùng bảng DB: bộ nhớ + file <c>App_Data/vietan-sessions.json</c> (khởi động lại không mất). Mất file chỉ làm các máy đang đăng nhập
/// cùng lúc còn dùng được tới lần đăng nhập kế tiếp.
/// </summary>
internal sealed class VietAnSessionRegistry
{
    private readonly ConcurrentDictionary<int, string> _current = new();
    private readonly Lock _fileLock = new();
    private readonly string _file;
    private readonly ILogger<VietAnSessionRegistry> _logger;

    public VietAnSessionRegistry(IHostEnvironment env, ILogger<VietAnSessionRegistry> logger)
    {
        _file = Path.Combine(env.ContentRootPath, "App_Data", "vietan-sessions.json");
        _logger = logger;
        Load();
    }

    /// <summary>Mã phiên hiện hành; "" = chưa đăng nhập lần nào từ khi có quy tắc (token cũ vẫn dùng tới lần đăng nhập kế tiếp).</summary>
    public string Current(int agentId) => _current.TryGetValue(agentId, out var sid) ? sid : "";

    /// <summary>Đăng nhập mới → mã phiên mới, mọi phiên trước của nhân viên này hết hiệu lực.</summary>
    public string Renew(int agentId)
    {
        var sid = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        _current[agentId] = sid;
        Save();
        return sid;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            foreach (var (id, sid) in JsonSerializer.Deserialize<Dictionary<int, string>>(File.ReadAllText(_file)) ?? [])
                _current[id] = sid;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Không đọc được sổ phiên nhân viên Việt An {File}", _file);
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
                File.WriteAllText(temp, JsonSerializer.Serialize(_current));
                File.Move(temp, _file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Không ghi được sổ phiên nhân viên Việt An {File} — chỉ còn trong bộ nhớ tới khi khởi động lại", _file);
            }
        }
    }
}
