using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VietAnExpress.Identity.Api;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Application;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity;

/// <summary>
/// Điểm vào DUY NHẤT (public) của module Identity: đăng nhập khách hàng bằng tài khoản ở dbo.TCustomer
/// (admin) hoặc tài khoản con của nhân viên, xác thực JWT cho toàn hệ thống, và trang MyTracking của khách.
/// </summary>
public static class IdentityModule
{
    public static Assembly Assembly => typeof(IdentityModule).Assembly;

    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.Secret) >= 32,
                "Jwt:Secret phải dài tối thiểu 32 byte. Dev: dotnet user-secrets set \"Jwt:Secret\" \"...\"")
            .ValidateOnStart();

        // Tài khoản chính: dbo.TCustomer (Login_UserName / Login_Password, không migration);
        // tài khoản con: dbo.TCustomer_NguoiLienLac; MyTracking: dbo.TCustomer_MyTracking + dbo.TCustomer_MyTracking_Banner.
        services.AddModuleDbContext<IdentityDbContext>(configuration, IdentityDbContext.Schema);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<SessionService>();
        // Chân email (tên, địa chỉ, hotline công ty) + link portal — mục cấu hình Company.
        services.AddSingleton(sp => EmailCompany.From(sp.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IPermissionProvider, IdentityPermissionProvider>();
        services.AddValidatorsFromAssembly(Assembly, includeInternalTypes: true);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false; // giữ nguyên tên claim "sub", "permission"…
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = JwtTokenService.SigningKey(jwt),
                    NameClaimType = VaClaimTypes.Name,
                    RoleClaimType = VaClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                bearer.Events = new JwtBearerEvents
                {
                    // Web portal gửi token qua cookie HttpOnly; client khác gửi header Authorization.
                    OnMessageReceived = ctx =>
                    {
                        if (string.IsNullOrEmpty(ctx.Token) && ctx.Request.Cookies.TryGetValue(AuthCookies.Access, out var token))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    },
                    // 401 / 403 cũng trả ProblemDetails có "message" tiếng Việt như mọi lỗi khác.
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        await WriteProblemAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized,
                            "UNAUTHORIZED", "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn");
                    },
                    OnForbidden = ctx => WriteProblemAsync(ctx.HttpContext, StatusCodes.Status403Forbidden,
                        "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này")
                };
            });

        return services;
    }

    private static Task WriteProblemAsync(HttpContext http, int status, string code, string message)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(
            ProblemDetailsMapper.Create(http, status, code, message), (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}

internal sealed class IdentityPermissionProvider : IPermissionProvider
{
    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(IdentityPermissions.ManageStaff, "Quản lý tài khoản nhân viên", SystemRoles.Customer),
        // Mọi tài khoản chính của khách; tài khoản con không có (AdminOnly).
        new(IdentityPermissions.MyTracking, "Cấu hình trang MyTracking", SystemRoles.Customer),
        new(IdentityPermissions.ChangePassword, "Đổi mật khẩu", SystemRoles.Customer),
        new(IdentityPermissions.Notifications, "Xem thông báo của Việt An", SystemRoles.Customer),
        new(IdentityPermissions.Troubles, "Gửi và theo dõi yêu cầu hỗ trợ & sự cố", SystemRoles.Customer),
        new(IdentityPermissions.AdminNotifications, "Soạn và gửi thông báo cho khách (quản trị Việt An)", SystemRoles.VietAnAdmin),
        new(IdentityPermissions.AdminTroubles, "Xử lý yêu cầu hỗ trợ & sự cố của khách (quản trị Việt An)", SystemRoles.VietAnAdmin),
        new(IdentityPermissions.AdminAnalytics, "Thống kê sử dụng (quản trị Việt An)", SystemRoles.VietAnAdmin)
    ];
}
