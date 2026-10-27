using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VietAnExpress.Identity.Infrastructure;

/// <summary>
/// Token đã thu hồi khi đăng xuất (mã jti → hạn của token), không dùng bảng DB: giữ trong bộ nhớ và ghi file
/// <c>App_Data/revoked-tokens.json</c> cạnh ứng dụng để khởi động lại (IIS recycle, đẩy bản mới) không mất.
/// Chỉ giữ tới khi token hết hạn — token hết hạn tự bị từ chối nên không cần nhớ nữa.
/// Không ghi được file (thiếu quyền ghi) thì vẫn thu hồi trong bộ nhớ và ghi cảnh báo.
/// </summary>
internal sealed class RevokedTokenStore
{
    /// <summary>Chặn file phình to bất thường; vượt thì bỏ các mục sắp hết hạn nhất.</summary>
    private const int MaxEntries = 50_000;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _revoked = new(StringComparer.Ordinal);
    private readonly Lock _fileLock = new();
    private readonly string _file;
    private readonly TimeProvider _clock;
    private readonly ILogger<RevokedTokenStore> _logger;

    public RevokedTokenStore(IHostEnvironment env, TimeProvider clock, ILogger<RevokedTokenStore> logger)
    {
        _file = Path.Combine(env.ContentRootPath, "App_Data", "revoked-tokens.json");
        _clock = clock;
        _logger = logger;
        Load();
    }

    public bool IsRevoked(string? id) =>
        !string.IsNullOrEmpty(id) && _revoked.TryGetValue(id, out var expires) && expires > _clock.GetUtcNow();

    public void Revoke(string? id, DateTimeOffset expires)
    {
        if (string.IsNullOrEmpty(id) || expires <= _clock.GetUtcNow()) return;
        _revoked[id] = expires;
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var saved = JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(File.ReadAllText(_file)) ?? [];
            var now = _clock.GetUtcNow();
            foreach (var (id, expires) in saved)
                if (expires > now) _revoked[id] = expires;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Không đọc được danh sách token đã thu hồi {File}", _file);
        }
    }

    private void Save()
    {
        lock (_fileLock)
        {
            var now = _clock.GetUtcNow();
            foreach (var (id, expires) in _revoked)
                if (expires <= now) _revoked.TryRemove(id, out _);
            if (_revoked.Count > MaxEntries)
                foreach (var (id, _) in _revoked.OrderBy(x => x.Value).Take(_revoked.Count - MaxEntries))
                    _revoked.TryRemove(id, out _);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
                var temp = _file + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(_revoked));
                File.Move(temp, _file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Không ghi được danh sách token đã thu hồi {File} — chỉ còn trong bộ nhớ tới khi khởi động lại", _file);
            }
        }
    }
}
