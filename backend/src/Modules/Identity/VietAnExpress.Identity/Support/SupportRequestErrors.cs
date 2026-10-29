using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Admin;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

internal static class SupportRequestErrors
{
    public static readonly Error Kind = Error.Validation("TROUBLE_KIND", "Chọn loại yêu cầu");
    public static readonly Error Bill = Error.Validation("TROUBLE_BILL", "Mã vận đơn không hợp lệ");
    public static readonly Error Subject = Error.Validation("TROUBLE_SUBJECT", $"Tiêu đề tối đa {SupportRequest.SubjectMaxLength} ký tự");
    public static readonly Error BillNotFound = Error.Validation("TROUBLE_BILL_NOT_FOUND", "Không tìm thấy vận đơn này trong đơn hàng của bạn");
    public static readonly Error Type = Error.Validation("TROUBLE_TYPE", "Chọn vấn đề cần hỗ trợ");
    public static readonly Error FeedbackTopic = Error.Validation("TROUBLE_FEEDBACK_TOPIC", "Chọn nội dung góp ý");
    public static readonly Error Priority = Error.Validation("TROUBLE_PRIORITY", "Mức độ không hợp lệ");
    public static readonly Error Description = Error.Validation("TROUBLE_DESC", $"Nội dung từ {SupportRequest.DescriptionMinLength} đến {SupportRequest.DescriptionMaxLength} ký tự");
    public static readonly Error Requester = Error.Validation("TROUBLE_REQUESTER", "Nhập người yêu cầu");
    public static readonly Error Contact = Error.Validation("TROUBLE_CONTACT", "Nhập SĐT hoặc email liên hệ");
    public static readonly Error FileTooLarge = Error.Validation("TROUBLE_FILE_TOO_LARGE", "Tệp đính kèm tối đa 10 MB");
    public static readonly Error FileType = Error.Validation("TROUBLE_FILE_TYPE", "Chỉ nhận ảnh, PDF, Excel, CSV hoặc Word");
    public static readonly Error Status = Error.Validation("TROUBLE_STATUS", "Trạng thái không hợp lệ");
    public static readonly Error Reply = Error.Validation("TROUBLE_NOTE", $"Nội dung trả lời từ 1 đến {SupportThread.ReplyMaxLength} ký tự");
    public static readonly Error NothingToSend = Error.Validation("TROUBLE_EMPTY", "Nhập nội dung trả lời hoặc đổi trạng thái");
    public static readonly Error ContactReply = Error.Validation("TROUBLE_CONTACT_NOTE", "Lời nhắn từ trang chủ chưa có tài khoản để xem trả lời — gọi hoặc email khách rồi đổi trạng thái");
    public static readonly Error NotWaiting = Error.Validation("TROUBLE_NOT_WAITING", "Yêu cầu này không chờ bạn phản hồi");
    public static readonly Error NotFound = Error.NotFound("TROUBLE_NOT_FOUND", "Không tìm thấy yêu cầu");
}
