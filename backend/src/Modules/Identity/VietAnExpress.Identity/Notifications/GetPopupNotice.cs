using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Notifications;

/// <summary>
/// Thông báo popup của hệ thống cũ — CRM (màn "Thông báo popup") ghi 2 dòng bảng cũ <c>dbo.TConfig</c>: <c>ThongBao_NoiDung</c> (HTML)
/// và <c>ThongBao_Show</c> ("1" hiện / "0" ẩn). Portal chỉ đọc; <c>html</c> là HTML gốc nhân viên soạn — frontend lọc trước khi hiện.
/// </summary>
internal sealed record PopupNoticeDto(bool Show, string? Html);

internal sealed record GetPopupNoticeQuery : IRequest<Result<PopupNoticeDto>>;

internal sealed class GetPopupNoticeHandler(IdentityDbContext db, ICurrentUser user)
    : IRequestHandler<GetPopupNoticeQuery, Result<PopupNoticeDto>>
{
    private const string ContentKey = "ThongBao_NoiDung";
    private const string ShowKey = "ThongBao_Show";

    public async Task<Result<PopupNoticeDto>> Handle(GetPopupNoticeQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        // Câu lệnh có IF nên EF chạy nguyên văn; DB không có TConfig (thử nghiệm) → không có dòng = không hiện.
        var rows = await db.Database.SqlQuery<ConfigRow>($"""
            IF OBJECT_ID(N'dbo.TConfig', N'U') IS NOT NULL
                SELECT ConfigName AS Name, ConfigValue AS Value FROM dbo.TConfig
                WHERE ConfigName IN ({ContentKey}, {ShowKey}) ORDER BY ConfigID;
            ELSE
                SELECT CAST(NULL AS nvarchar(150)) AS Name, CAST(NULL AS nvarchar(max)) AS Value WHERE 1 = 0;
            """).ToListAsync(ct);
        var show = rows.FirstOrDefault(r => r.Name == ShowKey)?.Value?.Trim() == "1";
        var html = rows.FirstOrDefault(r => r.Name == ContentKey)?.Value?.Trim();
        return show && !string.IsNullOrEmpty(html) ? new PopupNoticeDto(true, html) : new PopupNoticeDto(false, null);
    }

    private sealed class ConfigRow
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }
}
