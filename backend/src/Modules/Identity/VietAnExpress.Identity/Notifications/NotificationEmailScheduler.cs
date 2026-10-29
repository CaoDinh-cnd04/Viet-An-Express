using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Identity.Notifications;

/// <summary>Mỗi phút: thông báo hẹn giờ có chọn gửi email đã tới giờ đăng → xếp việc gửi email (khách thấy thông báo theo giờ đăng, không cần job).</summary>
internal sealed class NotificationEmailScheduler(IServiceScopeFactory scopes, TimeProvider clock, ILogger<NotificationEmailScheduler> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var started = await NotificationEmailJob.StartDueAsync(
                    scope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
                    scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>(),
                    clock, stoppingToken);
                if (started > 0) logger.LogInformation("Đã xếp gửi email cho {Count} thông báo hẹn giờ", started);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Lỗi kiểm tra thông báo hẹn giờ");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
