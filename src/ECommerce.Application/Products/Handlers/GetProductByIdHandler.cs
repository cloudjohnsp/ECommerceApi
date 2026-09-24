using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products.Handlers;

public sealed class GetProductByIdHandler(IProductRepository repository, IProductCache productCache)
    : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var cachedProduct = await productCache.GetAsync(request.ProductId, cancellationToken);
        if (cachedProduct is not null)
            return Result<ProductDto>.Success(cachedProduct);

        var product = await repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
            return Result<ProductDto>.Failure("Product not found.");

        var productDto = product.ToDto();
        await productCache.SetAsync(productDto, cancellationToken);
        return Result<ProductDto>.Success(productDto);
    }
}
