using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Admin;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

/// <summary>
/// Yêu cầu hỗ trợ &amp; sự cố — tên trường khớp frontend (<c>features/support/types.ts</c>).
/// <c>Desc</c> = nội dung khách gửi ban đầu; <c>Thread</c> = các lần CS / khách trả lời (cũ trước).
/// </summary>
internal sealed record SupportRequestDto(
    string Id, string Kind, string Bill, string Cnee, string Ct, string Type, string? Subject, string Lv, string Desc, string Req, string Contact,
    string Date, string Status, string UpdatedAt, IReadOnlyList<SupportMessageDto> Thread);

/// <summary>1 lần trả lời: <c>from</c> = cs / customer, <c>at</c> = 'dd/MM/yyyy HH:mm'.</summary>
internal sealed record SupportMessageDto(string From, string At, string Text);

/// <summary>Yêu cầu ở trang quản trị: kèm khách gửi.</summary>
/// <param name="ManagerId">Nhân viên Việt An phụ trách khách (TCustomer.Staff_ID → USERS.ID) — null khi chưa phân công.</param>
/// <param name="Email">Email người gửi nhập (TContact.Email) — lời nhắn từ trang chủ chỉ liên hệ lại được qua đây / SĐT.</param>
internal sealed record AdminSupportRequestDto(SupportRequestDto Ticket, string CustomerCode, string CompanyName, bool IsStaff, int? ManagerId = null, string? ManagerName = null,
    string? Email = null);

/// <summary>
/// <c>kind</c>: ho_tro (hỗ trợ &amp; sự cố — mặc định; su_co cũ cũng nhận như ho_tro) / gop_y. <c>bill</c> không bắt buộc — có thì phải là
/// vận đơn của khách; <c>subject</c> không bắt buộc (trống thì tiêu đề = vấn đề).
/// </summary>
internal sealed record NewSupportRequest(
    string? Kind, string? Bill, string? Cnee, string? Ct, string? Type, string? Subject, string? Lv, string? Desc, string? Req, string? Contact);
