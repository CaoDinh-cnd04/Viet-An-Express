
namespace VietAnExpress.Identity.Support;

/// <summary>
/// Trạng thái yêu cầu — khớp frontend (<c>features/support/types.ts</c>). Cột TContact.Status lưu số:
/// 0 = mới, 1 = đang xử lý, 2 = chờ khách phản hồi, 3 = đã xử lý.
/// </summary>
internal static class SupportRequestStatus
{
    public const string New = "new";
    public const string Doing = "doing";
    /// <summary>Chờ khách phản hồi.</summary>
    public const string WaitCustomer = "waitc";
    public const string Done = "done";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { New, Doing, WaitCustomer, Done };

    public static string Label(string status) => status switch
    {
        New => "Mới",
        Doing => "Đang xử lý",
        WaitCustomer => "Chờ khách phản hồi",
        Done => "Đã xử lý",
        _ => status
    };

    public static int ToNumber(string status) => status switch
    {
        Doing => 1,
        WaitCustomer => 2,
        Done => 3,
        _ => 0
    };

    public static string FromNumber(int? value) => value switch
    {
        1 => Doing,
        2 => WaitCustomer,
        3 => Done,
        _ => New
    };
}
