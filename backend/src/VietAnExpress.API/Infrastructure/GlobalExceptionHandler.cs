using Microsoft.AspNetCore.Diagnostics;
using VietAnExpress.SharedKernel.Exceptions;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.API.Infrastructure;

/// <summary>
/// Bắt mọi exception chưa xử lý → trả ProblemDetails thống nhất (có "error" + "message" cho frontend).
/// Lỗi nghiệp vụ ghi log mức Warning, lỗi hệ thống ghi Error kèm stack trace (không trả stack trace ra ngoài).
/// Errors:ShowDetails = true (chỉ bật tạm khi tra lỗi trên máy chủ không xem được log) → lỗi 500 kèm loại + nội dung exception.
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IConfiguration config) : IExceptionHandler
{
    /// <summary>Mã nginx "client closed request" — trình duyệt đã huỷ request (tải lại / chuyển trang), không phải lỗi máy chủ.</summary>
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken cancellationToken)
    {
        // Trình duyệt huỷ giữa chừng: SqlClient báo "Operation cancelled by user" (SqlException, không phải OperationCanceledException).
        // Không ai nhận phản hồi — ghi Information thay vì Error 500 để log không báo lỗi giả.
        if (http.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("{Method} {Path}: trình duyệt đã huỷ request", http.Request.Method, http.Request.Path);
            http.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problem = ProblemDetailsMapper.FromException(http, exception);

        if (exception is AppException or OperationCanceledException)
            logger.LogWarning("{Method} {Path} → {Status}: {Message}", http.Request.Method, http.Request.Path, problem.Status, exception.Message);
        else
        {
            logger.LogError(exception, "Lỗi chưa xử lý tại {Method} {Path}", http.Request.Method, http.Request.Path);
            if (config.GetValue("Errors:ShowDetails", false))
                problem.Extensions["exception"] = string.Join(" → ",
                    Chain(exception).Select(e => $"{e.GetType().Name}: {e.Message}"));
        }

        http.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await http.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
        return true;
    }

    private static IEnumerable<Exception> Chain(Exception e)
    {
        for (Exception? x = e; x is not null; x = x.InnerException) yield return x;
    }
}
