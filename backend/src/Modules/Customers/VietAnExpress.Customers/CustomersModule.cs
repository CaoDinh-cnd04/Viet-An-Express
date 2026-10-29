using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Customers.Shared;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Customers;

/// <summary>Điểm vào DUY NHẤT (public) của module Customers — đọc hồ sơ khách ở dbo.TCustomer.</summary>
public static class CustomersModule
{
    public static Assembly Assembly => typeof(CustomersModule).Assembly;

    public static IServiceCollection AddCustomersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLegacyDbContext<CustomersDbContext>(configuration, "customers");
        services.AddScoped<ICustomersApi, CustomersApi>();
        return services;
    }
}
