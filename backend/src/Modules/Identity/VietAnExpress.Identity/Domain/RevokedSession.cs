namespace VietAnExpress.Identity.Domain;

/// <summary>
/// Phiên đăng nhập đã đăng xuất (dbo.PhienThuHoi): mọi access / refresh token mang mã phiên này bị từ chối tới khi hết hạn.
/// Token không lưu DB nên đây là cách duy nhất huỷ phiên ở phía máy chủ; dòng hết hạn được xoá dần.
/// </summary>
internal sealed class RevokedSession
{
    public const int SessionIdMaxLength = 36;

    private RevokedSession() { } // EF Core

    public RevokedSession(string sessionId, DateTime expiresAtUtc, DateTime revokedAtUtc)
    {
        SessionId = sessionId;
        ExpiresAt = expiresAtUtc;
        RevokedAt = revokedAtUtc;
    }

    public string SessionId { get; private set; } = "";
    /// <summary>Hết hạn của token dài nhất trong phiên (UTC) — sau mốc này token tự hết hiệu lực, xoá dòng được.</summary>
    public DateTime ExpiresAt { get; private set; }
    public DateTime RevokedAt { get; private set; }
}
