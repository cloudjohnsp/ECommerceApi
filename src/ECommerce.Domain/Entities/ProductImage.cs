using ECommerce.Shared.Results;

namespace ECommerce.Domain.Entities;

public sealed class ProductImage : Entity
{
    public const long MaximumSizeBytes = 5 * 1024 * 1024;

    public Guid ProductId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ProductImage() { }

    private ProductImage(
        Guid productId,
        string storageKey,
        string url,
        string fileName,
        string contentType,
        long sizeBytes)
    {
        ProductId = productId;
        StorageKey = storageKey;
        Url = url;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<ProductImage> Create(
        Guid productId,
        string? storageKey,
        string? url,
        string? fileName,
        string? contentType,
        long sizeBytes)
    {
        var errors = new List<string>();
        if (productId == Guid.Empty)
            errors.Add("Product id is required.");
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Trim().Length > 500)
            errors.Add("Image storage key must contain between 1 and 500 characters.");
        if (string.IsNullOrWhiteSpace(url) || url.Trim().Length > 2048 ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
        {
            errors.Add("Image URL must be an absolute URL with at most 2048 characters.");
        }
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Trim().Length > 255)
            errors.Add("Image file name must contain between 1 and 255 characters.");
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Trim().Length > 100)
            errors.Add("Image content type must contain between 1 and 100 characters.");
        if (sizeBytes <= 0 || sizeBytes > MaximumSizeBytes)
            errors.Add($"Image size must be between 1 and {MaximumSizeBytes} bytes.");

        return errors.Count > 0
            ? Result<ProductImage>.Failure([.. errors])
            : Result<ProductImage>.Success(new ProductImage(
                productId,
                storageKey!.Trim(),
                url!.Trim(),
                fileName!.Trim(),
                contentType!.Trim().ToLowerInvariant(),
                sizeBytes));
    }
}
