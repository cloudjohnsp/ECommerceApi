using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ECommerce.Shared.Results;

namespace ECommerce.Domain.Entities;

public sealed partial class Category : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    private Category() { }

    private Category(string name, string slug)
    {
        Name = name;
        Slug = slug;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<Category> Create(string? name)
    {
        var validation = Validate(name);
        if (validation.IsFailure)
            return Result<Category>.Failure([.. validation.Errors]);

        var normalizedName = name!.Trim();
        return Result<Category>.Success(new Category(normalizedName, CreateSlug(normalizedName)));
    }

    public Result Update(string? name)
    {
        if (!IsActive)
            return Result.Failure("Inactive categories cannot be updated.");

        var validation = Validate(name);
        if (validation.IsFailure)
            return validation;

        Name = name!.Trim();
        Slug = CreateSlug(Name);
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Success();

        IsActive = false;
        DeactivatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DeactivatedAt;
        return Result.Success();
    }

    private static Result Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return Result.Failure("Category name must contain between 1 and 100 characters.");

        return string.IsNullOrEmpty(CreateSlug(name.Trim()))
            ? Result.Failure("Category name must contain at least one letter or number.")
            : Result.Success();
    }

    private static string CreateSlug(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return InvalidSlugCharacters().Replace(withoutDiacritics.ToLowerInvariant(), "-")
            .Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidSlugCharacters();
}
