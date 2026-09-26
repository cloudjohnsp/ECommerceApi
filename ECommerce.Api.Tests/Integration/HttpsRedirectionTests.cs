using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ECommerce.Api.Tests.Integration;

public sealed class HttpsRedirectionTests
{
    [Fact]
    public async Task HttpRequest_WhenRedirectionIsDisabled_IsHandledWithoutRedirect()
    {
        await using var factory = new HttpsPolicyApiFactory(useHttpsRedirection: false);
        using var client = CreateHttpClient(factory);

        using var response = await client.GetAsync("/api/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task HttpRequest_WhenRedirectionIsEnabled_RedirectsToConfiguredHttpsPort()
    {
        await using var factory = new HttpsPolicyApiFactory(useHttpsRedirection: true);
        using var client = CreateHttpClient(factory);

        using var response = await client.GetAsync("/api/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);
        response.Headers.Location.Should().Be(new Uri("https://localhost/api/health/live"));
    }

    [Fact]
    public async Task ForwardedHttpsRequest_WhenProxyHeadersAreEnabled_IsNotRedirected()
    {
        await using var factory = new HttpsPolicyApiFactory(
            useHttpsRedirection: true,
            useForwardedHeaders: true);
        using var client = CreateHttpClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/live");
        request.Headers.Add("X-Forwarded-For", "203.0.113.10");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Location.Should().BeNull();
    }

    private static HttpClient CreateHttpClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false
        });

    private sealed class HttpsPolicyApiFactory(
        bool useHttpsRedirection,
        bool useForwardedHeaders = false)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseApiTestConfiguration();
            builder.UseSetting(
                "ApiProtection:UseHttpsRedirection",
                useHttpsRedirection.ToString());
            builder.UseSetting(
                "ApiProtection:UseForwardedHeaders",
                useForwardedHeaders.ToString());
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
                services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443));
        }
    }
}
