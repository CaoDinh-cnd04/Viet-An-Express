using System.Text.Unicode;
using Microsoft.Extensions.Configuration;

namespace VietAnExpress.Identity.Shared;

/// <summary>Thông tin công ty ở chân email (mục cấu hình <c>Company</c>).</summary>
internal sealed record EmailCompany(string Name, string? Address, string? Hotline, string? Website, string? PortalUrl)
{
    public static EmailCompany From(IConfiguration c) => new(
        c["Company:Name"] is { Length: > 0 } name ? name : "Việt An Express",
        Clean(c["Company:Address"]), Clean(c["Company:Hotline"]), Clean(c["Company:Website"]), Clean(c["Company:PortalUrl"])?.TrimEnd('/'));

    /// <summary>Đường dẫn trong portal (vd "/notifications"); chưa cấu hình PortalUrl → null.</summary>
    public string? Portal(string path) => PortalUrl is null ? null : PortalUrl + path;

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
