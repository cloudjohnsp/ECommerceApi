using System.Net;
using ECommerce.Api.Middlewares;
using ECommerce.Infrastructure.Messaging;
using ECommerce.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerce.Api.Tests.Integration;

public sealed class ObservabilityTests
{
    [Fact]
    public async Task Request_WithValidCorrelationId_PreservesItInResponse()
    {
        await using var factory = new ObservableApiFactory();
        using var client = factory.CreateClient(CreateClientOptions());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "checkout-123");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)
            .Should().ContainSingle("checkout-123");
    }

    [Fact]
    public async Task Request_WithUnsafeCorrelationId_ReplacesItWithGeneratedValue()
    {
        await using var factory = new ObservableApiFactory();
        using var client = factory.CreateClient(CreateClientOptions());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "invalid value");

        using var response = await client.SendAsync(request);

        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        correlationId.Should().NotBe("invalid value");
        Guid.TryParseExact(correlationId, "N", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Metrics_WhenPrometheusIsEnabled_ExposesScrapingEndpoint()
    {
        await using var factory = new ObservableApiFactory();
        using var client = factory.CreateClient(CreateClientOptions());

        using var response = await client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("target_info");
        body.Should().Contain("service_name=\"ECommerce.Api\"");
    }

    private static WebApplicationFactoryClientOptions CreateClientOptions() => new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    };

    private sealed class ObservableApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ApiProtection:AllowedOrigins:0", "https://frontend.example.com");
            builder.UseSetting("ApiProtection:GlobalPermitLimit", "100");
            builder.UseSetting("ApiProtection:AuthenticationPermitLimit", "10");
            builder.UseSetting("ApiProtection:WindowSeconds", "300");
            builder.UseSetting("Observability:EnablePrometheus", "true");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                RemoveHostedService<PaymentOutboxWorker>(services);
                RemoveHostedService<IntegrationEventOutboxWorker>(services);
            });
        }

        private static void RemoveHostedService<TService>(IServiceCollection services)
            where TService : class, IHostedService
        {
            var descriptor = services.FirstOrDefault(service =>
                service.ServiceType == typeof(IHostedService) &&
                service.ImplementationType == typeof(TService));
            if (descriptor is not null)
                services.Remove(descriptor);
        }
    }
}
