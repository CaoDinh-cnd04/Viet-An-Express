using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using VietAnExpress.Identity.Admin;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.MyTracking;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Auth;

/// <param name="UserName">Tên đăng nhập của khách (dbo.TCustomer.Login_UserName — frontend gửi trường "username").</param>
internal sealed record LoginRequest(string UserName, string Password, bool Remember = false);

internal sealed record RefreshRequest(string? RefreshToken);

internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

internal sealed record TokenResponse(
    SessionUserDto User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
