using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.Geo.Geoapify;
using VietAnExpress.Shipments.Geo.GeoNames;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;
using VietAnExpress.Shipments.Tracking;

namespace VietAnExpress.Shipments;

/// <summary>Điểm vào DUY NHẤT (public) của module Shipments.</summary>
public static class ShipmentsModule
{
    public static Assembly Assembly => typeof(ShipmentsModule).Assembly;

    public static IServiceCollection AddShipmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ShipmentsDbContext>(configuration, ShipmentsDbContext.Schema);
        services.AddScoped<ILegacyBillReader, LegacyBillReader>();
        services.AddScoped<ILegacyOrderNumberAllocator, LegacyOrderNumberAllocator>();
        services.AddScoped<OrderAccess>();
        services.AddScoped<IShipmentsApi, ShipmentsApi>();

        // Tra cứu địa lý qua API ngoài (quốc gia + mã điện thoại: world-countries; mã bưu chính: GeoNames; gợi ý địa chỉ: Geoapify)
        // — có cache, timeout ngắn.
        services.AddGeoLookup(configuration);
        services.Configure<CompanyInfo>(configuration.GetSection(CompanyInfo.Section));
        services.AddSingleton<IPermissionProvider, ShipmentsPermissionProvider>();
        services.AddValidatorsFromAssembly(Assembly, includeInternalTypes: true);
        return services;
    }
}

internal static class GeoRegistration
{
    private const string UserAgent = "VietAnExpress-Portal/1.0";

    /// <summary>
    /// Tra cứu địa lý: <see cref="IGeoLookup"/> (điều phối, cache) + nguồn mã bưu chính GeoNames + gợi ý địa chỉ Geoapify
    /// + vùng sâu vùng xa <see cref="IRemoteAreaLookup"/>.
    /// Đổi nguồn mã bưu chính: đăng ký cài đặt khác của <see cref="IPostalCodeProvider"/> ở đây.
    /// </summary>
    public static IServiceCollection AddGeoLookup(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddOptions<GeoNamesOptions>()
            .Bind(configuration.GetSection(GeoNamesOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IPostalCodeProvider, GeoNamesPostalCodeProvider>((sp, c) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeoNamesOptions>>().Value;
            c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        services.AddOptions<GeoapifyOptions>()
            .Bind(configuration.GetSection(GeoapifyOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<IAddressSuggestionProvider, GeoapifyAddressProvider>((sp, c) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeoapifyOptions>>().Value;
            c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        services.AddHttpClient<IGeoLookup, GeoLookupService>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(6);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        // Tracking HUB-EZ (chỉ đọc hành trình theo mã hãng của đơn thuộc hub đi HUB-EZ).
        services.AddOptions<HubEzOptions>()
            .Bind(configuration.GetSection(HubEzOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHttpClient<IHubEzTracking, HubEzTrackingClient>((sp, c) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HubEzOptions>>().Value;
            c.BaseAddress = new Uri(o.TrackingBaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        // VSVX: bảng dbo.VungXauVungXa của hệ thống cũ (dùng DbContext → scoped).
        services.AddScoped<IRemoteAreaLookup, RemoteAreaLookup>();
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
