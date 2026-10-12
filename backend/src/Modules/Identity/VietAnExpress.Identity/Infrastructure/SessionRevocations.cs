using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Infrastructure;

/// <summary>
/// Danh sách phiên đã đăng xuất: giữ trong bộ nhớ để kiểm tra mỗi request không tốn truy vấn, ghi dbo.PhienThuHoi để còn
/// hiệu lực sau khi khởi động lại. Nạp từ DB lần đầu dùng. Chỉ đúng khi API chạy 1 instance (Render hiện tại) —
/// chạy nhiều instance thì phải đọc DB mỗi lần (hoặc cache dùng chung).
/// </summary>
internal sealed class SessionRevocations(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SessionRevocations> logger)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _revoked = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _loaded;

    public async ValueTask<bool> IsRevokedAsync(string sessionId, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        return _revoked.TryGetValue(sessionId, out var expires) && expires > clock.GetUtcNow();
    }

    /// <summary>Thu hồi phiên tới <paramref name="expires"/> (hết hạn token dài nhất của phiên).</summary>
    public async Task RevokeAsync(string sessionId, DateTimeOffset expires, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        var now = clock.GetUtcNow();
        if (expires <= now) return;
        _revoked[sessionId] = expires; // có hiệu lực ngay, kể cả khi ghi DB chậm

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (!await db.RevokedSessions.AnyAsync(r => r.SessionId == sessionId, ct))
            db.RevokedSessions.Add(new RevokedSession(sessionId, expires.UtcDateTime, now.UtcDateTime));
        // Dọn dòng đã hết hạn (token tự hết hiệu lực rồi).
        db.RevokedSessions.RemoveRange(await db.RevokedSessions.Where(r => r.ExpiresAt < now.UtcDateTime).Take(500).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        foreach (var (id, exp) in _revoked)
            if (exp <= now) _revoked.TryRemove(id, out _);
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_loaded) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_loaded) return;
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var now = clock.GetUtcNow().UtcDateTime;
            var rows = await db.RevokedSessions.AsNoTracking().Where(r => r.ExpiresAt > now)
                .Select(r => new { r.SessionId, r.ExpiresAt }).ToListAsync(ct);
            foreach (var r in rows) _revoked[r.SessionId] = new DateTimeOffset(r.ExpiresAt, TimeSpan.Zero);
            _loaded = true;
            logger.LogInformation("Đã nạp {Count} phiên đã đăng xuất còn hạn", rows.Count);
        }
        finally
        {
            _gate.Release();
        }
    }
}
