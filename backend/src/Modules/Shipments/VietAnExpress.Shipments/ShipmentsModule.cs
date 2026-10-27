using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Infrastructure;

namespace VietAnExpress.Shipments;

/// <summary>Điểm vào DUY NHẤT (public) của module Shipments.</summary>
public static class ShipmentsModule
{
    public static Assembly Assembly => typeof(ShipmentsModule).Assembly;

    public static IServiceCollection AddShipmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ShipmentsDbContext>(configuration, ShipmentsDbContext.Schema);
        services.AddScoped<Infrastructure.Legacy.ILegacyBillReader, Infrastructure.Legacy.LegacyBillReader>();
        services.AddScoped<ILegacyOrderNumberAllocator, LegacyOrderNumberAllocator>();
        services.AddScoped<Application.Orders.OrderAccess>();
        services.AddScoped<IShipmentsApi, Application.ShipmentsApi>();

        // Tra cứu địa lý qua API ngoài (quốc gia + mã điện thoại: world-countries; mã bưu chính: GeoNames; gợi ý địa chỉ: Geoapify)
        // — có cache, timeout ngắn.
        services.AddGeoLookup(configuration);
        services.Configure<Application.Orders.Documents.CompanyInfo>(configuration.GetSection(Application.Orders.Documents.CompanyInfo.Section));
        services.AddSingleton<IPermissionProvider, ShipmentsPermissionProvider>();
        services.AddValidatorsFromAssembly(Assembly, includeInternalTypes: true);
        return services;
    }
}

internal static class GeoRegistration
{
    private const string UserAgent = "VietAnExpress-Portal/1.0";

    /// <summary>
    /// Tra cứu địa lý: <see cref="Infrastructure.Geo.IGeoLookup"/> (điều phối, cache) + nguồn mã bưu chính GeoNames + gợi ý địa chỉ Geoapify
    /// + vùng sâu vùng xa <see cref="Infrastructure.Geo.IRemoteAreaLookup"/>.
    /// Đổi nguồn mã bưu chính: đăng ký cài đặt khác của <see cref="Infrastructure.Geo.IPostalCodeProvider"/> ở đây.
    /// </summary>
    public static IServiceCollection AddGeoLookup(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddOptions<Infrastructure.Geo.GeoNames.GeoNamesOptions>()
            .Bind(configuration.GetSection(Infrastructure.Geo.GeoNames.GeoNamesOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<Infrastructure.Geo.IPostalCodeProvider, Infrastructure.Geo.GeoNames.GeoNamesPostalCodeProvider>((sp, c) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Geo.GeoNames.GeoNamesOptions>>().Value;
            c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        services.AddOptions<Infrastructure.Geo.Geoapify.GeoapifyOptions>()
            .Bind(configuration.GetSection(Infrastructure.Geo.Geoapify.GeoapifyOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<Infrastructure.Geo.IAddressSuggestionProvider, Infrastructure.Geo.Geoapify.GeoapifyAddressProvider>((sp, c) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Geo.Geoapify.GeoapifyOptions>>().Value;
            c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        services.AddHttpClient<Infrastructure.Geo.IGeoLookup, Infrastructure.Geo.GeoLookupService>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(6);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        // Tracking HUB-EZ (chỉ đọc hành trình theo mã hãng của đơn thuộc hub đi HUB-EZ).
        services.AddOptions<Infrastructure.Carriers.HubEz.HubEzOptions>()
            .Bind(configuration.GetSection(Infrastructure.Carriers.HubEz.HubEzOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<Infrastructure.Carriers.HubEz.IHubEzTracking, Infrastructure.Carriers.HubEz.HubEzTrackingClient>((sp, c) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Infrastructure.Carriers.HubEz.HubEzOptions>>().Value;
            c.BaseAddress = new Uri(o.TrackingBaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        // VSVX: bảng dbo.VungXauVungXa của hệ thống cũ (dùng DbContext → scoped).
        services.AddScoped<Infrastructure.Geo.IRemoteAreaLookup, Infrastructure.Geo.RemoteAreaLookup>();
        return services;
    }
}

internal sealed class ShipmentsPermissionProvider : IPermissionProvider
{
    private const string Customer = SystemRoles.Customer;

    public IEnumerable<PermissionDefinition> GetPermissions() =>
    [
        new(ShipmentsPermissions.View, "Xem vận đơn", Customer),
        new(ShipmentsPermissions.Create, "Tạo đơn nháp", Customer),
        new(ShipmentsPermissions.Update, "Sửa đơn nháp", Customer),
        new(ShipmentsPermissions.IssueBill, "In & cấp mã vận đơn", Customer),
        new(ShipmentsPermissions.ViewAll, "Xem toàn bộ đơn của công ty", Customer)
    ];
}
