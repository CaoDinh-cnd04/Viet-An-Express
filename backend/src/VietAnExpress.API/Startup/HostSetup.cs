using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

namespace VietAnExpress.API.Startup;

/// <summary>Khởi tạo host: thư mục gốc, chạy dạng Windows Service, ghi log Serilog.</summary>
internal static class HostSetup
{
    public static WebApplicationBuilder CreateBuilder(string[] args)
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

        builder.Host.UseSerilog((context, services, logger) =>
        {
            logger.ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext();
            // Máy dev, Windows Service và IIS: ghi thêm file logs/ (không có console). Docker chỉ ghi console.
            var underIis = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_IIS_PHYSICAL_PATH"));
            if (context.HostingEnvironment.IsDevelopment() || WindowsServiceHelpers.IsWindowsService() || underIis)
                logger.WriteTo.File(Path.Combine(context.HostingEnvironment.ContentRootPath, "logs", "vietan-.log"),
                    rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30);
        });
        return builder;
    }
}
