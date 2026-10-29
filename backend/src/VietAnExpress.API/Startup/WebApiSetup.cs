using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using VietAnExpress.API.Infrastructure;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.API.Startup;

/// <summary>Web API: controller của module, JSON, lỗi ProblemDetails, phiên bản API, giới hạn tần suất, CORS, Swagger.</summary>
internal static class WebApiSetup
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddControllers()
            .ConfigureApplicationPartManager(parts =>
            {
                foreach (var assembly in ModuleRegistration.ModuleAssemblies)
                    parts.ApplicationParts.Add(new AssemblyPart(assembly));
                // Controller của module là internal → thay provider mặc định (chỉ nhận public).
                var defaultProvider = parts.FeatureProviders.OfType<ControllerFeatureProvider>().FirstOrDefault();
                if (defaultProvider is not null) parts.FeatureProviders.Remove(defaultProvider);
                parts.FeatureProviders.Add(new InternalControllerFeatureProvider());
            })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            })
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = InvalidModelState);

        services
            .AddApiVersioning(o =>
            {
                o.DefaultApiVersion = new ApiVersion(1);
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.ReportApiVersions = true;
                o.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(o =>
            {
                o.GroupNameFormat = "'v'VVV";
                o.SubstituteApiVersionInUrl = true;
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (context, ct) =>
            {
                var problem = ProblemDetailsMapper.Create(context.HttpContext, StatusCodes.Status429TooManyRequests,
                    "TOO_MANY_REQUESTS", "Bạn thao tác quá nhanh, vui lòng thử lại sau ít phút");
                await context.HttpContext.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json", ct);
            };
            o.AddPolicy(RateLimitPolicies.Login, http => FixedWindowByIp(http, permitLimit: 10));
            o.AddPolicy(RateLimitPolicies.PublicTracking, http => FixedWindowByIp(http, permitLimit: 30));
            o.AddPolicy(RateLimitPolicies.PublicContact, http => FixedWindowByIp(http, permitLimit: 3));
        });

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(o => o.AddDefaultPolicy(p => p
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.ConfigureOptions<ConfigureSwaggerOptions>();
        return services;
    }

    /// <summary>Lỗi đọc body (JSON sai kiểu…) cũng trả cùng định dạng ProblemDetails có "error" + "message".</summary>
    private static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Giá trị không hợp lệ" : x.ErrorMessage).ToArray());
        var problem = ProblemDetailsMapper.Create(context.HttpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST",
            errors.Values.SelectMany(v => v).FirstOrDefault() ?? "Dữ liệu gửi lên không hợp lệ");
        problem.Extensions["errors"] = errors;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    /// <summary>Giới hạn theo IP trong cửa sổ 1 phút.</summary>
    private static RateLimitPartition<string> FixedWindowByIp(HttpContext http, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
