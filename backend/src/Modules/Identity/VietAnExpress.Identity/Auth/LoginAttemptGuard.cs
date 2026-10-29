using System.Collections.Concurrent;
using VietAnExpress.Identity.Staff;

namespace VietAnExpress.Identity.Auth;

/// <summary>
/// Khóa tạm theo tên đăng nhập khi nhập sai mật khẩu nhiều lần — bổ sung cho giới hạn theo IP (đổi IP liên tục vẫn bị chặn).
/// Giữ trong bộ nhớ, không dùng bảng DB: khởi động lại ứng dụng thì đếm lại từ đầu.
/// Sai <see cref="MaxFailures"/> lần trong <see cref="Window"/> → khóa <see cref="Lockout"/>; đăng nhập đúng thì xóa bộ đếm.
/// Khóa ngắn để người khác cố tình nhập sai không chặn được khách quá lâu.
/// </summary>
internal sealed class LoginAttemptGuard(TimeProvider clock)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);
    /// <summary>Chặn bộ nhớ phình khi bị dò hàng loạt tên đăng nhập; vượt thì dọn mục đã hết hạn.</summary>
    private const int MaxEntries = 20_000;

    private sealed record Entry(int Failures, DateTimeOffset WindowStart, DateTimeOffset? LockedUntil);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>Thời gian còn bị khóa; null = được thử đăng nhập.</summary>
    public TimeSpan? LockedFor(string userName)
    {
        var now = clock.GetUtcNow();
        return _entries.TryGetValue(Key(userName), out var e) && e.LockedUntil > now ? e.LockedUntil - now : null;
    }

    public void Failed(string userName)
    {
        var now = clock.GetUtcNow();
        if (_entries.Count > MaxEntries) Prune(now);
        _entries.AddOrUpdate(Key(userName),
            _ => new Entry(1, now, null),
            (_, e) =>
            {
                var current = now - e.WindowStart > Window || e.LockedUntil <= now ? new Entry(0, now, null) : e;
                var failures = current.Failures + 1;
                return current with { Failures = failures, LockedUntil = failures >= MaxFailures ? now + Lockout : null };
            });
    }

    public void Succeeded(string userName) => _entries.TryRemove(Key(userName), out _);

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, e) in _entries)
            if (e.LockedUntil <= now && now - e.WindowStart > Window) _entries.TryRemove(key, out _);
    }

    private static string Key(string userName) => userName.Trim().ToLowerInvariant();
}
