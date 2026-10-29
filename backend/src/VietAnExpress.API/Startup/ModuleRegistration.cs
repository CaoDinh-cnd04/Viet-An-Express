using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using VietAnExpress.API.Infrastructure;
using VietAnExpress.Customers;
using VietAnExpress.Ecommerce;
using VietAnExpress.Identity;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.Shipments;

namespace VietAnExpress.API.Startup;

/// <summary>Đăng ký các module và dịch vụ dùng chung (thời gian, người dùng hiện tại, email, việc nền, MediatR, phân quyền).</summary>
internal static class ModuleRegistration
{
    /// <summary>Thêm module mới: 1 dòng AddXxxModule bên dưới + thêm assembly vào mảng này (MediatR, controller quét theo mảng).</summary>
    public static readonly Assembly[] ModuleAssemblies =
        [IdentityModule.Assembly, CustomersModule.Assembly, ShipmentsModule.Assembly, EcommerceModule.Assembly];

    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentityModule(configuration)
            .AddCustomersModule(configuration)
            .AddShipmentsModule(configuration)
            .AddEcommerceModule(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<DomainEventsInterceptor>();

        // Email (Gmail SMTP — Email:UserName / Email:Password trong user-secrets) + việc nền (gửi email sau khi trả request).
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.Section));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<BackgroundJobQueue>();
        services.AddSingleton<IBackgroundJobQueue>(sp => sp.GetRequiredService<BackgroundJobQueue>());
        services.AddHostedService<BackgroundJobWorker>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(ModuleAssemblies);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Phân quyền theo permission ([HasPermission(...)] trên controller).
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddAuthorization();
        return services;
    }
}
