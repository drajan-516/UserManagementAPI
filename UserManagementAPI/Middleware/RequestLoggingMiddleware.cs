using System.Diagnostics;

namespace UserManagementAPI.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(ctx);
        }
        catch
        {
            // Исключение дойдёт до ExceptionHandlingMiddleware, там StatusCode станет 500.
            // Здесь он ещё 200, поэтому логируем 500 явно.
            sw.Stop();
            logger.LogError("HTTP {Method} {Path} -> {StatusCode} за {ElapsedMs} мс (исключение)",
                ctx.Request.Method, ctx.Request.Path.Value, 500, sw.ElapsedMilliseconds);
            throw;
        }

        sw.Stop();
        var status = ctx.Response.StatusCode;
        var level = status >= 500 ? LogLevel.Error
            : status >= 400 ? LogLevel.Warning
            : LogLevel.Information;

        logger.Log(level, "HTTP {Method} {Path} -> {StatusCode} за {ElapsedMs} мс",
            ctx.Request.Method, ctx.Request.Path.Value, status, sw.ElapsedMilliseconds);
    }
}