using System.Net;
using ECommerce.Api.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

    [Fact]
    public async Task VersionedRoute_ForVersionOne_RemainsAvailableAlongsideLegacyRoute()
    {
        await using var factory = new ObservableApiFactory();
        using var client = factory.CreateClient(CreateClientOptions());

        using var versionedResponse = await client.GetAsync("/api/v1/health");
        using var legacyResponse = await client.GetAsync("/api/health");

        versionedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        legacyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        legacyResponse.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        (await legacyResponse.Content.ReadAsStringAsync()).Should().Be("Healthy");
        versionedResponse.Headers.GetValues("api-supported-versions")
            .Should().Contain("1.0");
    }

    [Fact]
    public async Task VersionedRoute_ForUnsupportedVersion_ReturnsNotFound()
    {
        await using var factory = new ObservableApiFactory();
        using var client = factory.CreateClient(CreateClientOptions());

        using var response = await client.GetAsync("/api/v2/health");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OpenApi_DescribesMultipartProductImageUpload()
    {
        await using var factory = new ObservableApiFactory();
        using var client = factory.CreateClient(CreateClientOptions());

        using var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("/api/v{version}/products/{productId}/images");
        body.Should().Contain("multipart/form-data");
        body.Should().Contain("/api/v{version}/auth/confirm-email");
        body.Should().Contain("/api/v{version}/auth/forgot-password");
        body.Should().Contain("/api/v{version}/auth/reset-password");
        body.Should().Contain("/api/v{version}/payments/{orderId}/refund");
        body.Should().Contain("/api/v{version}/user/{userId}/history");
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
            builder.UseApiTestConfiguration();
            builder.UseSetting("ApiProtection:AllowedOrigins:0", "https://frontend.example.com");
            builder.UseSetting("ApiProtection:GlobalPermitLimit", "100");
            builder.UseSetting("ApiProtection:AuthenticationPermitLimit", "10");
            builder.UseSetting("ApiProtection:WindowSeconds", "300");
            builder.UseSetting("Observability:EnablePrometheus", "true");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.PostConfigure<HealthCheckServiceOptions>(options =>
                    options.Registrations.Clear());
            });
        }
    }
}
