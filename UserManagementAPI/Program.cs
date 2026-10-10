using System.ComponentModel.DataAnnotations;
using UserManagementAPI.Middleware;
using UserManagementAPI.Models;
using UserManagementAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IUserService, UserService>();

var app = builder.Build();

// ===== Конвейер: порядок важен =====
app.UseMiddleware<ExceptionHandlingMiddleware>();      // 1. ловит всё, что упало ниже

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<TokenAuthenticationMiddleware>();    // 2. закрывает API токеном
app.UseMiddleware<RequestLoggingMiddleware>();         // 3. логирует то, что дошло до эндпоинтов
// ===================================

var users = app.MapGroup("/api/users").WithTags("Users");

users.MapGet("/", (IUserService svc, string? department, int page = 1, int pageSize = 20) =>
{
    if (page is < 1 or > 100_000 || pageSize is < 1 or > 100)
        return Results.BadRequest(new { error = "page: 1..100000, pageSize: 1..100" });
    return Results.Ok(svc.GetAll(department, page, pageSize));
});

users.MapGet("/{id:int}", (int id, IUserService svc) =>
{
    if (id < 1) return Results.BadRequest(new { error = "id должен быть положительным" });
    return svc.GetById(id) is { } u
        ? Results.Ok(u)
        : Results.NotFound(new { error = $"Пользователь {id} не найден" });
});

users.MapPost("/", (UserRequest? req, IUserService svc) =>
{
    if (req is null) return Results.BadRequest(new { error = "Тело запроса обязательно" });
    req = req.Normalized();
    if (!TryValidate(req, out var errors)) return ValidationError(errors);

    var result = svc.Create(req);
    return result.Status == UserOpStatus.EmailConflict
        ? Results.Conflict(new { error = "Пользователь с таким email уже существует" })
        : Results.Created($"/api/users/{result.User!.Id}", result.User);
});

users.MapPut("/{id:int}", (int id, UserRequest? req, IUserService svc) =>
{
    if (id < 1) return Results.BadRequest(new { error = "id должен быть положительным" });
    if (req is null) return Results.BadRequest(new { error = "Тело запроса обязательно" });
    req = req.Normalized();
    if (!TryValidate(req, out var errors)) return ValidationError(errors);

    var result = svc.Update(id, req);
    return result.Status switch
    {
        UserOpStatus.NotFound => Results.NotFound(new { error = $"Пользователь {id} не найден" }),
        UserOpStatus.EmailConflict => Results.Conflict(new { error = "Email уже используется другим пользователем" }),
        _ => Results.Ok(result.User)
    };
});

users.MapDelete("/{id:int}", (int id, IUserService svc) =>
{
    if (id < 1) return Results.BadRequest(new { error = "id должен быть положительным" });
    return svc.Delete(id) ? Results.NoContent() : Results.NotFound(new { error = $"Пользователь {id} не найден" });
});

// Только для проверки обработки ошибок (шаг 6); в продакшене не включается
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/debug/throw",
        (Func<IResult>)(() => throw new InvalidOperationException("Тестовое исключение")));
}

app.Run();

// Единый формат ошибок валидации: { "error": ..., "details": { поле: [сообщения] } }
static IResult ValidationError(Dictionary<string, string[]> errors) =>
    Results.BadRequest(new { error = "Ошибка валидации", details = errors });

static bool TryValidate(UserRequest req, out Dictionary<string, string[]> errors)
{
    var results = new List<ValidationResult>();
    var ok = Validator.TryValidateObject(req, new ValidationContext(req), results, validateAllProperties: true);
    errors = results
        .SelectMany(r => r.MemberNames.Select(m => (Member: m, Msg: r.ErrorMessage ?? "Ошибка")))
        .GroupBy(x => x.Member)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Msg).ToArray());
    return ok;
}