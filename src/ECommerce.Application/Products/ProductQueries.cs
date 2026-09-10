using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products;

public sealed record GetProductByIdQuery(Guid ProductId) : IRequest<Result<ProductDto>>;
public sealed record GetProductsQuery : IRequest<Result<IReadOnlyCollection<ProductDto>>>;
