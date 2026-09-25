using ECommerce.Application.Products.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products;

public sealed record UploadProductImageCommand(
    Guid ProductId,
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes) : IRequest<Result<ProductImageDto>>;

public sealed record GetProductImagesQuery(Guid ProductId)
    : IRequest<Result<IReadOnlyCollection<ProductImageDto>>>;
