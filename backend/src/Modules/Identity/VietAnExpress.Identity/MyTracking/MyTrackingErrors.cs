using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>Lỗi trang MyTracking.</summary>
internal static class MyTrackingErrors
{
    public static readonly Error MyTrackingNotAllowed =
        Error.Forbidden("MYTRACKING_NOT_ALLOWED", "Tài khoản chưa được mở chức năng MyTracking — vui lòng liên hệ Việt An");

    public static readonly Error MyTrackingChanged =
        Error.Conflict("MYTRACKING_CHANGED", "Trang MyTracking vừa được lưu ở nơi khác — vui lòng tải lại trang rồi lưu lại");

    public static readonly Error InvalidTrackingConfig =
        Error.Validation("INVALID_MYTRACKING_CONFIG", "Cấu hình MyTracking không hợp lệ: mỗi giá trị tối đa 250 ký tự, ảnh tối đa 200 KB, tổng cấu hình tối đa 2 MB");

    public static readonly Error MyTrackingNotFound =
        Error.NotFound("MYTRACKING_NOT_FOUND", "Trang tra cứu không tồn tại");
}
