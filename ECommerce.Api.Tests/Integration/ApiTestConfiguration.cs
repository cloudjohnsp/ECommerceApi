using Microsoft.AspNetCore.Hosting;

namespace ECommerce.Api.Tests.Integration;

internal static class ApiTestConfiguration
{
    public static IWebHostBuilder UseApiTestConfiguration(this IWebHostBuilder builder) =>
        builder
            .UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Port=5432;Database=ecommerce_tests;Username=test;Password=test-password")
            .UseSetting("Jwt:SecretKey", "test-only-signing-key-with-at-least-32-characters")
            .UseSetting("RabbitMq:UserName", "test")
            .UseSetting("RabbitMq:Password", "test-password")
            .UseSetting("PaymentGateway:WebhookSecret", "test-webhook-secret-with-32-bytes")
            .UseSetting("Redis:Enabled", "false")
            .UseSetting("ProductImageStorage:Enabled", "false")
            .UseSetting("Email:Enabled", "false")
            .UseSetting("ApiProtection:UseHttpsRedirection", "false")
            .UseSetting("OutboxProcessor:Enabled", "false");
}
