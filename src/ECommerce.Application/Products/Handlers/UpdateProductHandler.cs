using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products.Handlers;

public sealed class UpdateProductHandler(
    IProductRepository repository,
    IUnitOfWork unitOfWork,
    IProductCache productCache)
    : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var product = await repository.GetByIdForUpdateAsync(request.ProductId, cancellationToken);
            if (product is null) return Result<ProductDto>.Failure("Product not found.");

            var result = product.Update(request.Name, request.Description, request.Price, request.Stock);
            if (result.IsFailure) return Result<ProductDto>.Failure([.. result.Errors]);

            repository.Update(product);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            var productDto = product.ToDto();
            await productCache.SetAsync(productDto, CancellationToken.None);
            return Result<ProductDto>.Success(productDto);
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}
