using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Stores;

internal static class StoreErrors
{
    public static readonly Error NotLoggedIn = Error.Unauthorized("AUTH_REQUIRED", "Vui lòng đăng nhập");
    public static readonly Error NotFound = Error.NotFound("ECOM_STORE_NOT_FOUND", "Không tìm thấy cửa hàng");
    public static Error ChannelUnavailable(string name) => Error.BusinessRule("ECOM_CHANNEL_UNAVAILABLE", $"Chưa hỗ trợ kết nối {name}, Việt An đang hoàn thiện");
}
