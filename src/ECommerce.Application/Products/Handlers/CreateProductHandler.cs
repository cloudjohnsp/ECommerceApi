using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Results;
using ECommerce.Shared.Messaging;
using MediatR;

namespace ECommerce.Application.Products.Handlers;

public sealed class CreateProductHandler(
    IProductRepository repository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    IProductCache productCache,
    ICategoryRepository categoryRepository)
    : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (request.CategoryId.HasValue &&
            await categoryRepository.GetByIdAsync(request.CategoryId.Value, cancellationToken) is null)
        {
            return Result<ProductDto>.Failure("Category not found.");
        }

        var result = Product.Create(
            request.Name,
            request.Description,
            request.Price,
            request.Stock,
            request.CategoryId);
        if (result.IsFailure) return Result<ProductDto>.Failure([.. result.Errors]);

        var product = result.Value!;
        await repository.AddAsync(product, cancellationToken);
        await outboxMessageRepository.AddAsync(
            StockIntegrationEventFactory.Create(product, StockUpdateReasons.Created),
            cancellationToken);
        await unitOfWork.Commit(cancellationToken);
        var productDto = product.ToDto();
        await productCache.SetAsync(productDto, CancellationToken.None);
        return Result<ProductDto>.Success(productDto);
    }
}
