using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace VietAnExpress.Identity.Auth;

/// <summary>
/// Mã OTP 6 số đăng nhập (nhân viên Việt An, khách) giữ trong bộ nhớ — không dùng bảng DB; khởi động lại ứng dụng thì mã cũ hết hiệu lực.
/// Mã sống <see cref="Lifetime"/>, sai <see cref="MaxFailures"/> lần thì hủy; mỗi chủ tài khoản (<c>owner</c>) chỉ 1 mã còn hiệu lực
/// (gửi lại = hủy mã cũ). <typeparamref name="T"/> = thông tin cần để cấp phiên sau khi nhập đúng mã.
/// </summary>
internal class OtpStore<T>(TimeProvider clock) where T : class
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public const int MaxFailures = 5;
    private const int MaxEntries = 10_000;

    /// <summary>Mã lưu dạng SHA-256 — đọc bộ nhớ cũng không ra mã.</summary>
    private sealed record Entry(string Owner, T Payload, byte[] CodeHash, DateTimeOffset Expires, int Failures);

    public enum Outcome { Ok, Wrong, Expired }

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>Cấp mã mới cho <paramref name="owner"/> (hủy mã cũ của người đó) → (mã thử thách gửi trình duyệt, mã OTP gửi email).</summary>
    protected (string ChallengeId, string Code) IssueFor(string owner, T payload)
    {
        var now = clock.GetUtcNow();
        if (_entries.Count > MaxEntries) Prune(now);
        foreach (var old in _entries.Where(e => e.Value.Owner == owner).Select(e => e.Key).ToList()) _entries.TryRemove(old, out _);
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var challengeId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _entries[challengeId] = new Entry(owner, payload, Hash(code), now + Lifetime, 0);
        return (challengeId, code);
    }

    /// <summary>Thông tin của mã còn hiệu lực (chưa kiểm tra mã) — bước đăng ký email nhận OTP; null = hết hạn / không có.</summary>
    public T? Peek(string challengeId) =>
        !string.IsNullOrEmpty(challengeId) && _entries.TryGetValue(challengeId, out var e) && e.Expires > clock.GetUtcNow() ? e.Payload : null;

    /// <summary>Kiểm tra mã. Đúng → xóa mã (dùng 1 lần). Sai → đếm, sai quá <see cref="MaxFailures"/> lần thì hủy (Expired).</summary>
    public (Outcome Outcome, T? Payload) Verify(string challengeId, string code)
    {
        var now = clock.GetUtcNow();
        if (string.IsNullOrEmpty(challengeId) || !_entries.TryGetValue(challengeId, out var e) || e.Expires <= now)
        {
            _entries.TryRemove(challengeId ?? "", out _);
            return (Outcome.Expired, null);
        }
        if (CryptographicOperations.FixedTimeEquals(e.CodeHash, Hash((code ?? "").Trim())))
            return _entries.TryRemove(challengeId, out _) ? (Outcome.Ok, e.Payload) : (Outcome.Expired, null);
        var failed = e with { Failures = e.Failures + 1 };
        if (failed.Failures >= MaxFailures) _entries.TryRemove(challengeId, out _);
        else _entries.TryUpdate(challengeId, failed, e);
        return (Outcome.Wrong, e.Payload);
    }

    private static byte[] Hash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, e) in _entries)
            if (e.Expires <= now) _entries.TryRemove(key, out _);
    }
}
