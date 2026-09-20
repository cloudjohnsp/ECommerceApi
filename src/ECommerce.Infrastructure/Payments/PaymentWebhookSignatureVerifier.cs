using System.Security.Cryptography;
using System.Text;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

public sealed class PaymentWebhookSignatureVerifier(IOptions<PaymentGatewayOptions> options)
    : IPaymentWebhookSignatureVerifier
{
    private readonly byte[] _secret = Encoding.UTF8.GetBytes(options.Value.WebhookSecret);

    public bool IsValid(string payload, string? signature)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signature)) return false;

        var expected = Convert.ToHexStringLower(
            HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(payload)));
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var suppliedBytes = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());

        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
