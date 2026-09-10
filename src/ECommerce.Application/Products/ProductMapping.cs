using ECommerce.Application.Products.Dtos;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Products;

internal static class ProductMapping
{
    internal static ProductDto ToDto(this Product product) => new(
        product.Id, product.Name, product.Description, product.Price, product.Stock,
        product.IsActive, product.CreatedAt, product.UpdatedAt);
}
