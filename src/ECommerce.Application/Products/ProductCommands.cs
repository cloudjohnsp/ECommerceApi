using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products;

public sealed record CreateProductCommand(string Name, string Description, decimal Price, int Stock)
    : IRequest<Result<ProductDto>>;

public sealed record UpdateProductCommand(Guid ProductId, string Name, string Description, decimal Price, int Stock)
    : IRequest<Result<ProductDto>>;

public sealed record DeleteProductCommand(Guid ProductId) : IRequest<Result>;
