using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace VietAnExpress.SharedKernel.Web;

/// <summary>
/// MVC mặc định chỉ nhận controller public. Controller của module để internal (quy tắc đóng gói module)
/// nên host thay provider mặc định bằng provider này.
/// </summary>
public sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) =>
        typeInfo is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
        && typeof(ControllerBase).IsAssignableFrom(typeInfo)
        && typeInfo.Name.EndsWith("Controller", StringComparison.Ordinal)
        && !typeInfo.IsDefined(typeof(NonControllerAttribute));
}

/// <summary>Tên policy giới hạn tần suất gọi — endpoint công khai / đăng nhập.</summary>
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string PublicTracking = "public-tracking";
    /// <summary>Form "Gửi lời nhắn" ở trang chủ (không cần đăng nhập) — chống spam.</summary>
    public const string PublicContact = "public-contact";
}
