using System.Net;
using System.Net.Http.Json;
using ECommerce.Infrastructure.Messaging;
using ECommerce.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace ECommerce.Api.Tests.Integration;

public sealed class ApiProtectionTests
{
    [Fact]
    public async Task GlobalRateLimit_ReturnsTooManyRequestsAfterConfiguredLimit()
    {
        await using var factory = new ProtectedApiFactory(globalLimit: 2, authenticationLimit: 10);
        using var client = factory.CreateClient(CreateClientOptions());

        (await client.GetAsync("/api/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/health/live")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AuthenticationRateLimit_IsStricterThanGlobalLimit()
    {
        await using var factory = new ProtectedApiFactory(globalLimit: 100, authenticationLimit: 2);
        using var client = factory.CreateClient(CreateClientOptions());

        (await client.PostAsJsonAsync("/api/auth/login", new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/auth/login", new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/auth/login", new { })).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task CorsPreflight_ForConfiguredOrigin_ReturnsAllowOriginHeader()
    {
        await using var factory = new ProtectedApiFactory(globalLimit: 100, authenticationLimit: 10);
        using var client = factory.CreateClient(CreateClientOptions());
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/health/live");
        request.Headers.Add("Origin", "https://frontend.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle("https://frontend.example.com");
    }

    private static WebApplicationFactoryClientOptions CreateClientOptions() => new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    };

    private sealed class ProtectedApiFactory(int globalLimit, int authenticationLimit)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ApiProtection:AllowedOrigins:0", "https://frontend.example.com");
            builder.UseSetting("ApiProtection:GlobalPermitLimit", globalLimit.ToString());
            builder.UseSetting("ApiProtection:AuthenticationPermitLimit", authenticationLimit.ToString());
            builder.UseSetting("ApiProtection:WindowSeconds", "300");
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
