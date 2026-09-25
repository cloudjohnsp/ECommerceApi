using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ECommerce.Application.Abstractions.Storage;
using ECommerce.Infrastructure.Options;
using ECommerce.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Storage;

public sealed class AzureBlobProductImageStorage(
    BlobServiceClient blobServiceClient,
    IOptions<ProductImageStorageOptions> options,
    ILogger<AzureBlobProductImageStorage> logger) : IProductImageStorage
{
    private readonly ProductImageStorageOptions _options = options.Value;

    public async Task<Result<StoredProductImage>> UploadAsync(
        Guid productId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var storageKey = $"products/{productId:N}/{Guid.NewGuid():N}{GetExtension(contentType)}";

        try
        {
            var container = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
            await container.CreateIfNotExistsAsync(
                PublicAccessType.Blob,
                cancellationToken: cancellationToken);
            var blob = container.GetBlobClient(storageKey);
            await blob.UploadAsync(
                content,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType.ToLowerInvariant() }
                },
                cancellationToken);

            return Result<StoredProductImage>.Success(
                new StoredProductImage(storageKey, BuildPublicUrl(blob.Uri, storageKey)));
        }
        catch (RequestFailedException exception)
        {
            logger.LogWarning(
                exception,
                "Failed to upload an image for product {ProductId} to Azure Blob Storage.",
                productId);
            return Result<StoredProductImage>.Failure("Product image storage is unavailable.");
        }
    }

    public async Task<Result> DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var container = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
            await container.DeleteBlobIfExistsAsync(
                storageKey,
                DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (RequestFailedException exception)
        {
            logger.LogWarning(exception, "Failed to delete image {StorageKey} from Azure Blob Storage.", storageKey);
            return Result.Failure("Product image storage is unavailable.");
        }
    }

    private string BuildPublicUrl(Uri blobUri, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            return blobUri.AbsoluteUri;

        var encodedKey = string.Join('/', storageKey.Split('/').Select(Uri.EscapeDataString));
        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{encodedKey}";
    }

    private static string GetExtension(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => string.Empty
    };
}
