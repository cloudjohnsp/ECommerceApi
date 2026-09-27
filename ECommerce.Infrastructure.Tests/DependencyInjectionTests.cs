using ECommerce.Infrastructure.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_ShouldRejectWebhookSecretShorterThan32Utf8Bytes()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("short-secret");

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var resolveOptions = () => provider
            .GetRequiredService<IOptions<PaymentGatewayOptions>>()
            .Value;

        resolveOptions.Should().Throw<OptionsValidationException>()
            .WithMessage("*PaymentGateway:WebhookSecret must contain at least 32 UTF-8 bytes.*");
    }

    [Fact]
    public void AddInfrastructure_ShouldAcceptWebhookSecretWith32Utf8Bytes()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new string('s', 32));

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PaymentGatewayOptions>>().Value;

        options.WebhookSecret.Should().HaveLength(32);
    }

    [Theory]
    [InlineData(
        "PaymentGateway:BaseUrl",
        "ftp://localhost:5002",
        "*PaymentGateway:BaseUrl must be an absolute HTTP or HTTPS URL without credentials, query, or fragment.*")]
    [InlineData(
        "PaymentGateway:BaseUrl",
        "http://localhost:5002?tenant=one",
        "*PaymentGateway:BaseUrl must be an absolute HTTP or HTTPS URL without credentials, query, or fragment.*")]
    [InlineData(
        "PaymentGateway:CallbackUrl",
        "http://user:password@localhost:5000/api/webhooks/payments",
        "*PaymentGateway:CallbackUrl must be an absolute HTTP or HTTPS URL without credentials or fragment.*")]
    [InlineData(
        "PaymentGateway:CallbackUrl",
        "http://localhost:5000/api/webhooks/payments#fragment",
        "*PaymentGateway:CallbackUrl must be an absolute HTTP or HTTPS URL without credentials or fragment.*")]
    public void AddInfrastructure_ShouldRejectUnsafePaymentGatewayUrl(
        string setting,
        string value,
        string expectedMessage)
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new string('s', 32), setting, value);

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var resolveOptions = () => provider
            .GetRequiredService<IOptions<PaymentGatewayOptions>>()
            .Value;

        resolveOptions.Should().Throw<OptionsValidationException>()
            .WithMessage(expectedMessage);
    }

    private static IConfiguration CreateConfiguration(
        string webhookSecret,
        string? overrideSetting = null,
        string? overrideValue = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = new string('j', 32),
            ["Jwt:Issuer"] = "ecommerce-tests",
            ["Jwt:Audience"] = "ecommerce-tests",
            ["RabbitMq:HostName"] = "localhost",
            ["RabbitMq:UserName"] = "test",
            ["RabbitMq:Password"] = "test-password",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:ExchangeName"] = "ecommerce.tests",
            ["Redis:Enabled"] = "false",
            ["ProductImageStorage:Enabled"] = "false",
            ["Email:Enabled"] = "false",
            ["PaymentGateway:BaseUrl"] = "http://localhost:5002",
            ["PaymentGateway:CallbackUrl"] = "http://localhost:5000/api/webhooks/payments",
            ["PaymentGateway:WebhookSecret"] = webhookSecret,
            ["OutboxProcessor:Enabled"] = "false",
            ["OrderExpiration:Enabled"] = "false"
        };
        if (overrideSetting is not null) values[overrideSetting] = overrideValue;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
