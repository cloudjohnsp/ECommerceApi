using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products.Handlers;

public sealed class DeleteProductHandler(
    IProductRepository repository,
    IUnitOfWork unitOfWork,
    IProductCache productCache)
    : IRequestHandler<DeleteProductCommand, Result>
{
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var product = await repository.GetByIdForUpdateAsync(
                request.ProductId,
                cancellationToken);
            if (product is null) return Result.Failure("Product not found.");

            var result = product.Deactivate();
            if (result.IsFailure) return result;

            repository.Update(product);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            await productCache.RemoveAsync(product.Id, CancellationToken.None);
            return Result.Success();
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}
