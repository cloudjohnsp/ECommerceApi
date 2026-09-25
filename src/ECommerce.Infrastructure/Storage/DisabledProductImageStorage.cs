using ECommerce.Application.Abstractions.Storage;
using ECommerce.Shared.Results;

namespace ECommerce.Infrastructure.Storage;

public sealed class DisabledProductImageStorage : IProductImageStorage
{
    public Task<Result<StoredProductImage>> UploadAsync(
        Guid productId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<StoredProductImage>.Failure("Product image storage is disabled."));

    public Task<Result> DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}
