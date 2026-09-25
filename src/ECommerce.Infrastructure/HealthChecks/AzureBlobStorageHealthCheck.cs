using Azure.Storage.Blobs;
using ECommerce.Infrastructure.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.HealthChecks;

public sealed class AzureBlobStorageHealthCheck(
    BlobServiceClient blobServiceClient,
    IOptions<ProductImageStorageOptions> options) : IHealthCheck
{
    private readonly ProductImageStorageOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var container = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
            await container.ExistsAsync(cancellationToken);
            return HealthCheckResult.Healthy("Product image storage is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Product image storage is unreachable.", exception);
        }
    }
}
