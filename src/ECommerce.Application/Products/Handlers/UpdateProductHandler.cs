using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products.Handlers;

public sealed class UpdateProductHandler(IProductRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null) return Result<ProductDto>.Failure("Product not found.");

        var result = product.Update(request.Name, request.Description, request.Price, request.Stock);
        if (result.IsFailure) return Result<ProductDto>.Failure([.. result.Errors]);

        repository.Update(product);
        await unitOfWork.Commit(cancellationToken);
        return Result<ProductDto>.Success(product.ToDto());
    }
}
