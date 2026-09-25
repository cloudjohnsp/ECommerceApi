using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Storage;
using ECommerce.Application.Products.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Products.Handlers;

public sealed class UploadProductImageHandler(
    IProductRepository productRepository,
    IProductImageRepository imageRepository,
    IProductImageStorage storage,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UploadProductImageCommand, Result<ProductImageDto>>
{
    public async Task<Result<ProductImageDto>> Handle(
        UploadProductImageCommand request,
        CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
            return Result<ProductImageDto>.Failure("Product not found.");
        if (!request.Content.CanRead || !request.Content.CanSeek)
            return Result<ProductImageDto>.Failure("Image content must be readable and seekable.");

        if (!await HasExpectedSignatureAsync(
                request.Content,
                request.ContentType,
                cancellationToken))
        {
            return Result<ProductImageDto>.Failure(
                "Image content does not match the declared content type.");
        }

        var uploadResult = await storage.UploadAsync(
            product.Id,
            request.Content,
            request.ContentType,
            cancellationToken);
        if (uploadResult.IsFailure)
            return Result<ProductImageDto>.Failure([.. uploadResult.Errors]);

        var storedImage = uploadResult.Value!;
        var imageResult = ProductImage.Create(
            product.Id,
            storedImage.StorageKey,
            storedImage.Url,
            Path.GetFileName(request.FileName),
            request.ContentType,
            request.SizeBytes);
        if (imageResult.IsFailure)
        {
            await storage.DeleteAsync(storedImage.StorageKey, CancellationToken.None);
            return Result<ProductImageDto>.Failure([.. imageResult.Errors]);
        }

        try
        {
            await imageRepository.AddAsync(imageResult.Value!, cancellationToken);
            await unitOfWork.Commit(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(storedImage.StorageKey, CancellationToken.None);
            throw;
        }

        return Result<ProductImageDto>.Success(imageResult.Value!.ToDto());
    }

    private static async Task<bool> HasExpectedSignatureAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var originalPosition = content.Position;
        var header = new byte[12];
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var read = await content.ReadAsync(
                header.AsMemory(bytesRead, header.Length - bytesRead),
                cancellationToken);
            if (read == 0)
                break;
            bytesRead += read;
        }
        content.Position = originalPosition;

        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytesRead >= 3 &&
                header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
            "image/png" => bytesRead >= 8 &&
                header.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            "image/webp" => bytesRead >= 12 &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}

public sealed class GetProductImagesHandler(
    IProductRepository productRepository,
    IProductImageRepository imageRepository)
    : IRequestHandler<GetProductImagesQuery, Result<IReadOnlyCollection<ProductImageDto>>>
{
    public async Task<Result<IReadOnlyCollection<ProductImageDto>>> Handle(
        GetProductImagesQuery request,
        CancellationToken cancellationToken)
    {
        if (await productRepository.GetByIdAsync(request.ProductId, cancellationToken) is null)
            return Result<IReadOnlyCollection<ProductImageDto>>.Failure("Product not found.");

        var images = await imageRepository.GetByProductIdAsync(request.ProductId, cancellationToken);
        return Result<IReadOnlyCollection<ProductImageDto>>.Success(
            images.Select(image => image.ToDto()).ToArray());
    }
}
