using System.Security.Cryptography;
using System.Text;

namespace UserManagementAPI.Middleware;

public class TokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;
    private readonly byte[][] _validTokens;

    public TokenAuthenticationMiddleware(
        RequestDelegate next, ILogger<TokenAuthenticationMiddleware> logger, IConfiguration config)
    {
        _next = next;
        _logger = logger;

        var tokens = config.GetSection("Auth:Tokens").Get<string[]>() ?? [];
        _validTokens = tokens.Where(t => !string.IsNullOrWhiteSpace(t))
                             .Select(Encoding.UTF8.GetBytes).ToArray();

        // Конструктор вызывается при старте приложения: без токенов оно не запустится
        if (_validTokens.Length == 0)
            throw new InvalidOperationException("Не настроены токены: секция Auth:Tokens пуста.");
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var header = ctx.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(header))
            { await Reject(ctx, "заголовок Authorization отсутствует"); return; }

        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            { await Reject(ctx, "схема не Bearer"); return; }

        var token = Encoding.UTF8.GetBytes(header[prefix.Length..].Trim());
        if (!IsValid(token))
            { await Reject(ctx, "недействительный токен"); return; }

        await _next(ctx);
    }

    // Перебираем все токены без раннего выхода и сравниваем за постоянное время
    private bool IsValid(byte[] candidate)
    {
        var ok = false;
        foreach (var valid in _validTokens)
            ok |= CryptographicOperations.FixedTimeEquals(valid, candidate);
        return ok;
    }

    private async Task Reject(HttpContext ctx, string reason)
    {
        // Сам токен в лог не пишем
        _logger.LogWarning("401: {Method} {Path}, причина: {Reason}",
            ctx.Request.Method, ctx.Request.Path.Value, reason);

        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        ctx.Response.Headers.WWWAuthenticate = "Bearer";
        await ctx.Response.WriteAsJsonAsync(new { error = "Требуется действительный токен доступа." });
    }
}