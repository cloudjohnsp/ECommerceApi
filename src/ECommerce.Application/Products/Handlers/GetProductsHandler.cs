using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Application.Products.Specifications;
using ECommerce.Shared.Pagination;

namespace ECommerce.Application.Products.Handlers;

public sealed class GetProductsHandler(IProductRepository repository)
    : IRequestHandler<GetProductsQuery, Result<PagedResult<ProductDto>>>
{
    public async Task<Result<PagedResult<ProductDto>>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        var products = await repository.SearchAsync(
            new ProductCatalogSpecification(request), cancellationToken);
        var response = new PagedResult<ProductDto>(
            [.. products.Items.Select(product => product.ToDto())],
            request.Page,
            request.PageSize,
            products.TotalCount);
        return Result<PagedResult<ProductDto>>.Success(response);
    }
}
