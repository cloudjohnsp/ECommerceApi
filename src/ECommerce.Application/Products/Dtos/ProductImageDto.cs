using ECommerce.Domain.Entities;

namespace ECommerce.Application.Products.Dtos;

public sealed record ProductImageDto(
    Guid Id,
    Guid ProductId,
    string Url,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt);

internal static class ProductImageMapping
{
    internal static ProductImageDto ToDto(this ProductImage image) => new(
        image.Id,
        image.ProductId,
        image.Url,
        image.FileName,
        image.ContentType,
        image.SizeBytes,
        image.CreatedAt);
}
