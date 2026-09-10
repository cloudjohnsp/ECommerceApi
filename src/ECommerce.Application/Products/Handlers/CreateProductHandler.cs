using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products.Handlers;

public sealed class CreateProductHandler(IProductRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var result = Product.Create(request.Name, request.Description, request.Price, request.Stock);
        if (result.IsFailure) return Result<ProductDto>.Failure([.. result.Errors]);

        await repository.AddAsync(result.Value!, cancellationToken);
        await unitOfWork.Commit(cancellationToken);
        return Result<ProductDto>.Success(result.Value!.ToDto());
    }
}
