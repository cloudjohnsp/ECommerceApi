using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Products.Handlers;

public sealed class UpdateProductHandler(
    IProductRepository repository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    IProductCache productCache,
    ICategoryRepository categoryRepository)
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
            var previousAvailableStock = product.AvailableStock;
            if (request.CategoryId.HasValue &&
                await categoryRepository.GetByIdAsync(request.CategoryId.Value, cancellationToken) is null)
            {
                return Result<ProductDto>.Failure("Category not found.");
            }

            var result = product.Update(
                request.Name,
                request.Description,
                request.Price,
                request.Stock,
                request.CategoryId);
            if (result.IsFailure) return Result<ProductDto>.Failure([.. result.Errors]);

            repository.Update(product);
            if (product.AvailableStock != previousAvailableStock)
            {
                await outboxMessageRepository.AddAsync(
                    StockIntegrationEventFactory.Create(product, StockUpdateReasons.Adjusted),
                    cancellationToken);
            }
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
