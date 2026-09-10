using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products.Handlers;

public sealed class GetProductsHandler(IProductRepository repository)
    : IRequestHandler<GetProductsQuery, Result<IReadOnlyCollection<ProductDto>>>
{
    public async Task<Result<IReadOnlyCollection<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await repository.GetAllAsync(cancellationToken);
        IReadOnlyCollection<ProductDto> response = [.. products.Select(product => product.ToDto())];
        return Result<IReadOnlyCollection<ProductDto>>.Success(response);
    }
}
