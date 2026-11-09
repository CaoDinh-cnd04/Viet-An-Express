using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using VietAnExpress.SharedKernel.Authorization;

namespace VietAnExpress.Identity.Support;

/// <summary>
/// Hỗ trợ &amp; góp ý thời gian thực (SignalR): máy chủ chỉ phát tín hiệu "yêu cầu TRB-… vừa đổi" (event <see cref="Event"/>, tham số = mã),
/// không gửi nội dung — trình duyệt nhận tín hiệu thì tải lại danh sách qua API cũ (<c>GET /troubles</c>, <c>/troubles/manage</c>) nên
/// quyền xem / phạm vi khách vẫn do API kiểm tra. Kết nối dùng cookie đăng nhập như API (đăng nhập mới vào được).
/// Nhóm: <c>cust:{id}</c> = mọi phiên của khách đó (tài khoản chính, tài khoản con, nhân viên Việt An đang làm thay); <c>vietan</c> = mọi nhân viên Việt An.
/// Hệ thống cũ / CMS sửa thẳng dbo.TContact thì không có tín hiệu — trang vẫn tự tải lại mỗi phút.
/// </summary>
[Authorize]
internal sealed class SupportLiveHub : Hub
{
    /// <summary>Đường dẫn hub (cùng tiền tố API).</summary>
    public const string Path = "/api/v1/live/support";
    public const string Event = "support";
    public const string VietAnGroup = "vietan";

    public static string CustomerGroup(long customerId) => "cust:" + customerId.ToString(CultureInfo.InvariantCulture);

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (long.TryParse(user?.FindFirst(VaClaimTypes.CustomerId)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var customerId) && customerId > 0)
            await Groups.AddToGroupAsync(Context.ConnectionId, CustomerGroup(customerId));
        if (!string.IsNullOrEmpty(user?.FindFirst(VaClaimTypes.VietAnUserId)?.Value))
            await Groups.AddToGroupAsync(Context.ConnectionId, VietAnGroup);
        await base.OnConnectedAsync();
    }
}

/// <summary>Phát tín hiệu yêu cầu đổi tới khách của yêu cầu + nhân viên Việt An. Lỗi chỉ ghi log — không làm hỏng thao tác đã lưu.</summary>
internal sealed class SupportLive(IHubContext<SupportLiveHub> hub, ILogger<SupportLive> logger)
{
    public async Task ChangedAsync(SupportRequest ticket, CancellationToken ct)
    {
        try
        {
            IReadOnlyList<string> groups = ticket.CustomerId > 0
                ? [SupportLiveHub.CustomerGroup(ticket.CustomerId), SupportLiveHub.VietAnGroup]
                : [SupportLiveHub.VietAnGroup];
            await hub.Clients.Groups(groups).SendAsync(SupportLiveHub.Event, ticket.Code, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Không phát được tín hiệu thời gian thực cho yêu cầu {Code}", ticket.Code);
        }
    }
}
