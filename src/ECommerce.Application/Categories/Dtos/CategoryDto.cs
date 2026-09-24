namespace ECommerce.Application.Categories.Dtos;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
