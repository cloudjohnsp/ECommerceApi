using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Logout_WithMissingRefreshToken_ReturnsBadRequest()
    {
        await using var factory = new ProtectedApiFactory(globalLimit: 100, authenticationLimit: 10);
        using var client = factory.CreateClient(CreateClientOptions());

        using var response = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    [Fact]
    public async Task GlobalRateLimit_WhenForwardedHeadersAreDisabled_IgnoresSpoofedClientAddress()
    {
        await using var factory = new ProtectedApiFactory(
            globalLimit: 1,
            authenticationLimit: 10,
            useForwardedHeaders: false);
        using var client = factory.CreateClient(CreateClientOptions());

        using var firstRequest = CreateForwardedRequest("203.0.113.10");
        using var secondRequest = CreateForwardedRequest("203.0.113.11");

        (await client.SendAsync(firstRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.SendAsync(secondRequest)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GlobalRateLimit_WhenForwardedHeadersAreEnabled_PartitionsByForwardedClientAddress()
    {
        await using var factory = new ProtectedApiFactory(
            globalLimit: 1,
            authenticationLimit: 10,
            useForwardedHeaders: true);
        using var client = factory.CreateClient(CreateClientOptions());

        using var firstClientRequest = CreateForwardedRequest("203.0.113.10");
        using var repeatedClientRequest = CreateForwardedRequest("203.0.113.10");
        using var otherClientRequest = CreateForwardedRequest("203.0.113.11");

        (await client.SendAsync(firstClientRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.SendAsync(repeatedClientRequest)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await client.SendAsync(otherClientRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/health/live", HttpStatusCode.OK)]
    [InlineData("/api/route-that-does-not-exist", HttpStatusCode.NotFound)]
    public async Task Responses_IncludeSecurityHeaders(string path, HttpStatusCode expectedStatusCode)
    {
        await using var factory = new ProtectedApiFactory(globalLimit: 100, authenticationLimit: 10);
        using var client = factory.CreateClient(CreateClientOptions());

        using var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(expectedStatusCode);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle("no-referrer");
        response.Headers.GetValues("Permissions-Policy")
            .Should().ContainSingle("camera=(), geolocation=(), microphone=()");
    }

    private static WebApplicationFactoryClientOptions CreateClientOptions() => new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    };

    private static HttpRequestMessage CreateForwardedRequest(string clientAddress)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/live");
        request.Headers.Add("X-Forwarded-For", clientAddress);
        request.Headers.Add("X-Forwarded-Proto", "https");
        return request;
    }

    private sealed class ProtectedApiFactory(
        int globalLimit,
        int authenticationLimit,
        bool useForwardedHeaders = false)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseApiTestConfiguration();
            builder.UseSetting("ApiProtection:AllowedOrigins:0", "https://frontend.example.com");
            builder.UseSetting("ApiProtection:GlobalPermitLimit", globalLimit.ToString());
            builder.UseSetting("ApiProtection:AuthenticationPermitLimit", authenticationLimit.ToString());
            builder.UseSetting("ApiProtection:WindowSeconds", "300");
            builder.UseSetting("ApiProtection:UseForwardedHeaders", useForwardedHeaders.ToString());
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        }
    }
}
