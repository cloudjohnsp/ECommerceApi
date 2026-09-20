using System.Security.Cryptography;
using System.Text;
using ECommerce.Infrastructure.Options;
using ECommerce.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Tests.Payments;

public sealed class PaymentWebhookSignatureVerifierTests
{
    [Fact]
    public void IsValid_WithSignatureForExactPayload_ReturnsTrue()
    {
        const string secret = "test-secret";
        const string payload = "{\"event\":\"payment.approved\"}";
        var signature = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));
        var sut = CreateVerifier(secret);

        sut.IsValid(payload, signature).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenPayloadWasChanged_ReturnsFalse()
    {
        var sut = CreateVerifier("test-secret");

        sut.IsValid("changed", new string('0', 64)).Should().BeFalse();
    }

    private static PaymentWebhookSignatureVerifier CreateVerifier(string secret) => new(
        Microsoft.Extensions.Options.Options.Create(
            new PaymentGatewayOptions { WebhookSecret = secret }));
}
