using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.API.Startup;

internal static class StartupTasks
{
    /// <summary>Migrate (nếu bật) rồi seed — chạy theo thứ tự Order của từng module.</summary>
    public static async Task RunStartupTasksAsync(this IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        foreach (var task in scope.ServiceProvider.GetServices<IModuleStartupTask>().OrderBy(t => t.Order))
            await task.RunAsync(ct);
    }
}
