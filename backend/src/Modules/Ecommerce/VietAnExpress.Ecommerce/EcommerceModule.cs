using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VietAnExpress.Ecommerce.Contracts;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Ecommerce;

/// <summary>Điểm vào DUY NHẤT (public) của module Ecommerce — đơn E-commerce và shop trên các kênh bán (sau này TikTok Shop, Amazon…).</summary>
public static class EcommerceModule
{
    public static Assembly Assembly => typeof(EcommerceModule).Assembly;

    public static IServiceCollection AddEcommerceModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<EcommerceDbContext>(configuration, EcommerceDbContext.Schema);
        services.AddSingleton<IPermissionProvider, EcommercePermissionProvider>();
        return services;
    }
}

internal sealed class EcommercePermissionProvider : IPermissionProvider
{
    private const string Customer = SystemRoles.Customer;

    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(EcommercePermissions.View, "Xem đơn & cửa hàng E-commerce", Customer),
        new(EcommercePermissions.Orders, "Tạo & xử lý đơn E-commerce", Customer),
        new(EcommercePermissions.Connect, "Kết nối / ngắt kết nối cửa hàng", Customer),
        new(EcommercePermissions.ViewAll, "Xem toàn bộ đơn E-commerce của công ty", Customer)
    ];
}
