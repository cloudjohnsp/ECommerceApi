using System.Net;
using ECommerce.Infrastructure.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ECommerce.Infrastructure.Tests.HealthChecks;

public sealed class PaymentGatewayHealthCheckTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, HealthStatus.Healthy)]
    [InlineData(HttpStatusCode.ServiceUnavailable, HealthStatus.Unhealthy)]
    public async Task CheckHealth_MapsGatewayResponseToHealthStatus(
        HttpStatusCode statusCode,
        HealthStatus expectedStatus)
    {
        var factory = new StubHttpClientFactory(new HttpClient(new StubHandler(statusCode))
        {
            BaseAddress = new Uri("http://gateway/")
        });
        var healthCheck = new PaymentGatewayHealthCheck(factory);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(expectedStatus);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
