using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Shared.Pagination;

namespace ECommerce.Application.Products;

public sealed record GetProductByIdQuery(Guid ProductId) : IRequest<Result<ProductDto>>;
public sealed record GetProductsQuery(
    string? Search = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string SortBy = "name",
    bool Descending = false,
    int Page = 1,
    int PageSize = 20,
    Guid? CategoryId = null) : IRequest<Result<PagedResult<ProductDto>>>;
