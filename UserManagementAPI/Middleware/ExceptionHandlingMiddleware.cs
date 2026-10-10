namespace UserManagementAPI.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);

            // Роутинг отдаёт 404/405 с пустым телом: приводим к единому формату
            if (!ctx.Response.HasStarted && ctx.Response.ContentLength is null && ctx.Response.ContentType is null)
            {
                var message = ctx.Response.StatusCode switch
                {
                    StatusCodes.Status404NotFound => "Ресурс не найден.",
                    StatusCodes.Status405MethodNotAllowed => "Метод не поддерживается.",
                    _ => null
                };
                if (message is not null) await Write(ctx, ctx.Response.StatusCode, message);
            }
        }
        catch (BadHttpRequestException ex)   // битый JSON, неверный тип данных
        {
            logger.LogWarning(ex, "Некорректный запрос {Method} {Path}", ctx.Request.Method, ctx.Request.Path.Value);
            if (ctx.Response.HasStarted) throw;
            await Write(ctx, ex.StatusCode, "Некорректный запрос: проверьте формат JSON.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Необработанное исключение {Method} {Path}", ctx.Request.Method, ctx.Request.Path.Value);
            if (ctx.Response.HasStarted) throw;
            await Write(ctx, StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера.");
        }
    }

    private static Task Write(HttpContext ctx, int status, string message)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { error = message });
    }
}