using ECommerce.Shared.Results;

namespace ECommerce.Application.Abstractions.Storage;

public sealed record StoredProductImage(string StorageKey, string Url);

public interface IProductImageStorage
{
    Task<Result<StoredProductImage>> UploadAsync(
        Guid productId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
