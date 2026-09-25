namespace ECommerce.Infrastructure.Options;

public sealed class ProductImageStorageOptions
{
    public const string SectionName = "ProductImageStorage";

    public bool Enabled { get; init; } = true;
    public string ConnectionString { get; init; } = "UseDevelopmentStorage=true";
    public string ContainerName { get; init; } = "product-images";
    public string? PublicBaseUrl { get; init; }
}
