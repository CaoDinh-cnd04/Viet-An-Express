using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using VietAnExpress.API.Infrastructure;
using VietAnExpress.Customers;
using VietAnExpress.Identity;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments;
using VietAnExpress.Ecommerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting.WindowsServices;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    // Thư mục gốc = thư mục chứa file chạy khi: là Windows Service (thư mục hiện tại là System32 của Windows),
    // hoặc chạy .exe từ thư mục khác. `dotnet run` khi dev vẫn dùng thư mục project như cũ.
    var useInstallDir = WindowsServiceHelpers.IsWindowsService()
        || !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = useInstallDir ? AppContext.BaseDirectory : null
    });
    builder.Host.UseWindowsService(o => o.ServiceName = "VietAnExpressApi"); // không phải service thì không làm gì

    // Render / Docker: nền tảng cấp cổng qua biến PORT và tự lo HTTPS ở proxy phía trước.
    var platformPort = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(platformPort)) builder.WebHost.UseUrls($"http://0.0.0.0:{platformPort}");

    // Sau proxy (Vercel → Render/ngrok): lấy IP thật của khách và scheme HTTPS (cookie Secure).
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear(); // proxy của nền tảng không có IP cố định
        o.KnownProxies.Clear();
        o.ForwardLimit = 2;        // Vercel + proxy nền tảng
    });

    builder.Host.UseSerilog((context, services, logger) =>
    {
        logger.ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();
        // Máy dev và Windows Service: ghi thêm file logs/ (service không có console). Docker chỉ ghi console.
        if (context.HostingEnvironment.IsDevelopment() || WindowsServiceHelpers.IsWindowsService())
            logger.WriteTo.File(Path.Combine(context.HostingEnvironment.ContentRootPath, "logs", "vietan-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30);
    });

    // ---------- Module ----------
    // Thêm module mới: 1 dòng AddXxxModule + thêm assembly vào mảng dưới.
    Assembly[] moduleAssemblies = [IdentityModule.Assembly, CustomersModule.Assembly, ShipmentsModule.Assembly, EcommerceModule.Assembly];

    builder.Services
        .AddIdentityModule(builder.Configuration)
        .AddCustomersModule(builder.Configuration)
        .AddShipmentsModule(builder.Configuration)
        .AddEcommerceModule(builder.Configuration);

    // ---------- Dùng chung ----------
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
    builder.Services.AddScoped<AuditableEntityInterceptor>();
    builder.Services.AddScoped<DomainEventsInterceptor>();

    // Email (Gmail SMTP — Email:UserName / Email:Password trong user-secrets) + việc nền (gửi email sau khi trả request).
    builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
    builder.Services.AddSingleton<BackgroundJobQueue>();
    builder.Services.AddSingleton<IBackgroundJobQueue>(sp => sp.GetRequiredService<BackgroundJobQueue>());
    builder.Services.AddHostedService<BackgroundJobWorker>();

    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssemblies(moduleAssemblies);
        cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    });

    // ---------- Phân quyền theo permission ----------
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddAuthorization();

    // ---------- Web API ----------
    builder.Services
        .AddControllers()
        .ConfigureApplicationPartManager(parts =>
        {
            foreach (var assembly in moduleAssemblies)
                parts.ApplicationParts.Add(new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(assembly));
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
        .ConfigureApiBehaviorOptions(o =>
        {
            // Lỗi đọc body (JSON sai kiểu…) cũng trả cùng định dạng ProblemDetails có "error" + "message".
            o.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Giá trị không hợp lệ" : x.ErrorMessage).ToArray());
                var problem = ProblemDetailsMapper.Create(context.HttpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST",
                    errors.Values.SelectMany(v => v).FirstOrDefault() ?? "Dữ liệu gửi lên không hợp lệ");
                problem.Extensions["errors"] = errors;
                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            };
        });

    builder.Services
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

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddRateLimiter(o =>
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
    });

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

    var app = builder.Build();

    // ---------- Pipeline HTTP ----------
    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
    {
        app.UseSwagger();
        app.UseSwaggerUI(o =>
        {
            foreach (var description in app.DescribeApiVersions())
                o.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", $"Việt An Express API {description.GroupName}");
            o.DocumentTitle = "Việt An Express API";
        });
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        // Chạy sau proxy của nền tảng (có PORT) thì proxy đã ép HTTPS; tự chuyển hướng sẽ làm hỏng health check nội bộ.
        if (string.IsNullOrEmpty(platformPort)) app.UseHttpsRedirection();
    }

    app.UseCors();
    // Dữ liệu API là dữ liệu riêng của khách: không cho trình duyệt / proxy lưu đệm (bấm Back sau đăng xuất không còn dữ liệu cũ),
    // không cho đoán loại nội dung. Endpoint tự đặt Cache-Control (nếu có) được giữ nguyên.
    app.Use((context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            if (!headers.ContainsKey("Cache-Control")) headers.CacheControl = "no-store";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            return Task.CompletedTask;
        });
        return next(context);
    });
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapControllers();
    // Identify the deployed revision when checking Render after a push.
    var deployedRevision = app.Configuration["RENDER_GIT_COMMIT"];
    app.MapHealthChecks("/health").AllowAnonymous().Add(endpoint =>
    {
        var next = endpoint.RequestDelegate!;
        endpoint.RequestDelegate = async context =>
        {
            if (!string.IsNullOrEmpty(deployedRevision))
                context.Response.Headers["X-Vietan-Revision"] = deployedRevision;
            await next(context);
        };
    });

    // Migrate + seed khi khởi động. Tắt bằng Database:RunStartupTasks=false (vd kiểm tra container không có DB trong CI).
    if (app.Configuration.GetValue("Database:RunStartupTasks", true))
        await RunStartupTasksAsync(app.Services, app.Lifetime.ApplicationStopping);
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Ứng dụng dừng do lỗi khi khởi động");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Giới hạn theo IP trong cửa sổ 1 phút.
static RateLimitPartition<string> FixedWindowByIp(HttpContext http, int permitLimit) =>
    RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });

// Migrate (nếu bật) rồi seed — chạy theo thứ tự Order của từng module.
static async Task RunStartupTasksAsync(IServiceProvider services, CancellationToken ct)
{
    await using var scope = services.CreateAsyncScope();
    foreach (var task in scope.ServiceProvider.GetServices<IModuleStartupTask>().OrderBy(t => t.Order))
        await task.RunAsync(ct);
}

/// <summary>Cho test tích hợp (WebApplicationFactory) truy cập.</summary>
public partial class Program;
