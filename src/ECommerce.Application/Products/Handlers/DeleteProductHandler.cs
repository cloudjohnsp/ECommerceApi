using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products.Handlers;

public sealed class DeleteProductHandler(IProductRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteProductCommand, Result>
{
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null) return Result.Failure("Product not found.");

        var result = product.Deactivate();
        if (result.IsFailure) return result;

        repository.Update(product);
        await unitOfWork.Commit(cancellationToken);
        return Result.Success();
    }
}
