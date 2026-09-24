using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ECommerce.Infrastructure.HealthChecks;

public sealed class PaymentGatewayHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public const string HttpClientName = "PaymentGatewayHealth";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthCheckContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName)
                .GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Payment gateway is reachable.")
                : HealthCheckResult.Unhealthy(
                    $"Payment gateway returned status {(int)response.StatusCode}.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Payment gateway health check failed.", exception);
        }
    }
}
