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

    private static IConfiguration CreateConfiguration(string webhookSecret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
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
                ["OutboxProcessor:Enabled"] = "false"
            })
            .Build();
}
