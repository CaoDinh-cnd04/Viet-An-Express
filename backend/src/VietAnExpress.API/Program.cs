using Serilog;
using VietAnExpress.API.Startup;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = HostSetup.CreateBuilder(args);
    builder.Services
        .AddModules(builder.Configuration)
        .AddWebApi(builder.Configuration);

    var app = builder.Build();

    // Thứ tự middleware có ý nghĩa — đổi thứ tự cần kiểm tra lại PathBase IIS, CORS và cookie đăng nhập.
    app.UseIisApiPathBase();
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
        app.UseHttpsRedirection();
    }

    var webUi = app.UseWebUi();

    // Gọi tường minh: để chọn route sau khi đã gộp PathBase "/api" ở trên (mặc định UseRouting chạy đầu pipeline).
    app.UseRouting();
    app.UseCors();
    app.UseSecurityHeaders();
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapControllers();
    if (webUi is not null) app.MapWebUiFallback(webUi);
    app.MapHealthChecks("/health").AllowAnonymous();

    // Migrate + seed khi khởi động. Tắt bằng Database:RunStartupTasks=false (vd kiểm tra container không có DB trong CI).
    if (app.Configuration.GetValue("Database:RunStartupTasks", true))
        await app.Services.RunStartupTasksAsync(app.Lifetime.ApplicationStopping);
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

/// <summary>Cho test tích hợp (WebApplicationFactory) truy cập.</summary>
public partial class Program;
