namespace VietAnExpress.API.Startup;

/// <summary>Các bước riêng của pipeline HTTP: gộp PathBase IIS, header bảo mật, phục vụ giao diện web (wwwroot).</summary>
internal static class HttpPipeline
{
    /// <summary>
    /// IIS: backend là IIS Application gắn ở /api (frontend ở gốc site) → IIS cắt "/api" vào PathBase.
    /// Gộp lại để route "api/v1/..." và cookie Path=/api/v1/auth giữ nguyên như khi chạy độc lập. /api/health giữ PathBase để khớp /health.
    /// </summary>
    public static IApplicationBuilder UseIisApiPathBase(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        if (context.Request.PathBase.Equals("/api", StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/health"))
        {
            context.Request.Path = context.Request.PathBase.Add(context.Request.Path);
            context.Request.PathBase = PathString.Empty;
        }
        return next(context);
    });

    /// <summary>
    /// Dữ liệu API là dữ liệu riêng của khách: không cho trình duyệt / proxy lưu đệm (bấm Back sau đăng xuất không còn dữ liệu cũ),
    /// không cho đoán loại nội dung. Endpoint tự đặt Cache-Control (nếu có) được giữ nguyên.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            if (!headers.ContainsKey("Cache-Control")) headers.CacheControl = "no-store";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            DenyFraming(headers);
            return Task.CompletedTask;
        });
        return next(context);
    });

    /// <summary>
    /// Hosting IIS (đẩy qua FTP): giao diện web/dist chép vào wwwroot, backend phục vụ luôn — chung tên miền với /api
    /// nên cookie đăng nhập SameSite=Strict vẫn dùng được. Không có wwwroot/index.html (dev, frontend tách thư mục) → null, bỏ qua.
    /// </summary>
    public static StaticFileOptions? UseWebUi(this WebApplication app)
    {
        if (app.Environment.WebRootPath is not { } webRoot || !File.Exists(Path.Combine(webRoot, "index.html"))) return null;
        var options = new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                // File trong assets/ có mã băm trong tên → lưu đệm lâu; index.html luôn hỏi lại để nhận bản build mới.
                ctx.Context.Response.Headers.CacheControl = ctx.Context.Request.Path.StartsWithSegments("/assets")
                    ? "public, max-age=31536000, immutable"
                    : "no-cache";
                DenyFraming(ctx.Context.Response.Headers);
            }
        };
        app.UseDefaultFiles();
        app.UseStaticFiles(options);
        return options;
    }

    /// <summary>Đường dẫn của React Router (vd /orders/new, refresh trang) → index.html; /api/... sai vẫn trả 404 của API.</summary>
    public static void MapWebUiFallback(this WebApplication app, StaticFileOptions webUi) =>
        app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html", webUi);

    /// <summary>Chặn trang khác nhúng portal vào iframe (lừa khách bấm) — chỉ cho chính portal nhúng (in đơn).</summary>
    private static void DenyFraming(IHeaderDictionary headers)
    {
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers.ContentSecurityPolicy = "frame-ancestors 'self'";
    }
}
