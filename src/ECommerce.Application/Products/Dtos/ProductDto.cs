namespace ECommerce.Application.Products.Dtos;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int AvailableStock,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
