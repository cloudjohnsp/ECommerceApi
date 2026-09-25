using System.Net.Sockets;
using ECommerce.Infrastructure.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.HealthChecks;

public sealed class SmtpHealthCheck(IOptions<EmailOptions> options) : IHealthCheck
{
    private readonly EmailOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.Host, _options.Port, cancellationToken);
            return HealthCheckResult.Healthy("SMTP server is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SMTP server is unreachable.", exception);
        }
    }
}
