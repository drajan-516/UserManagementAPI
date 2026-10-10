using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Models;

public record User
{
    public int Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public string Department { get; init; } = "";
    public string? Role { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record UserRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string FirstName { get; init; } = "";

    [Required, StringLength(50, MinimumLength = 1)]
    public string LastName { get; init; } = "";

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; init; } = "";

    [Required, StringLength(50, MinimumLength = 1)]
    public string Department { get; init; } = "";

    [StringLength(50)]
    public string? Role { get; init; }

    // Защита от null и пробелов; вызывать ДО валидации
    public UserRequest Normalized() => this with
    {
        FirstName = FirstName?.Trim() ?? "",
        LastName = LastName?.Trim() ?? "",
        Email = Email?.Trim().ToLowerInvariant() ?? "",
        Department = Department?.Trim() ?? "",
        Role = string.IsNullOrWhiteSpace(Role) ? null : Role.Trim()
    };
}