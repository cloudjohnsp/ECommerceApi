using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using ECommerce.Shared.Results;
using System.Text.RegularExpressions;

namespace ECommerce.Domain.Entities;

public sealed class User : Entity
{
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    private readonly List<RefreshToken> _refreshTokens = [];
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    private User()
    {
    }

    private User(
        string firstName,
        string lastName,
        Email email,
        string passwordHash,
        UserRole role = UserRole.Customer)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<User> Create(string firstName, string lastName, Email email, string passwordHash, UserRole role = UserRole.Customer)
    {
        var validation = Validate(firstName, lastName, email, passwordHash);
        return validation.IsFailure
            ? Result<User>.Failure([.. validation.Errors])
            : Result<User>.Success(new User(firstName.Trim(), lastName.Trim(), email, passwordHash, role));
    }

    public Result UpdateProfile(string firstName, string lastName, Email email)
    {
        var validation = Validate(firstName, lastName, email, PasswordHash);
        if (validation.IsFailure)
        {
            return validation;
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result ChangePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure("Password hash is required.");
        }

        PasswordHash = passwordHash;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result ChangeRole(UserRole role)
    {
        Role = role;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Success();
        }

        IsActive = false;
        DeactivatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DeactivatedAt;
        return Result.Success();
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        DeactivatedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static Result Validate(string firstName, string lastName, Email? email, string passwordHash)
    {
        var errors = new List<string>();
        ValidateName(firstName, "First name", errors);
        ValidateName(lastName, "Last name", errors);
        if (email is null) errors.Add("E-mail is required.");
        if (string.IsNullOrWhiteSpace(passwordHash)) errors.Add("Password hash is required.");
        return errors.Count == 0 ? Result.Success() : Result.Failure([.. errors]);
    }

    private static void ValidateName(string? value, string fieldName, ICollection<string> errors)
    {
        var normalizedValue = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedValue) || normalizedValue.Length > 100)
        {
            errors.Add($"{fieldName} must contain between 1 and 100 characters.");
        }
        else
        {
            // Allow Unicode letters, spaces, hyphens and apostrophes only
            if (!Regex.IsMatch(normalizedValue, "^[\\p{L} '\\-]+$"))
            {
                errors.Add($"{fieldName} contains invalid characters.");
            }
        }
    }
}
