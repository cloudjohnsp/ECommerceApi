using System.Net.Http.Json;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Infrastructure.Options;
using ECommerce.Shared.Results;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

public sealed class PaymentGatewayClient(
    HttpClient httpClient,
    IOptions<PaymentGatewayOptions> options) : IPaymentGateway
{
    private readonly PaymentGatewayOptions _options = options.Value;

    public async Task<Result<GatewayPayment>> CreateAsync(
        CreateGatewayPayment payment,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "payments")
        {
            Content = JsonContent.Create(new
            {
                amount = payment.Amount,
                currency = payment.Currency,
                reference = payment.OrderId.ToString(),
                callbackUrl = _options.CallbackUrl
            })
        };
        request.Headers.Add("Idempotency-Key", payment.PaymentId.ToString());

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Result<GatewayPayment>.Failure(
                    $"Payment gateway rejected the request with status {(int)response.StatusCode}.");

            var gatewayPayment = await response.Content.ReadFromJsonAsync<GatewayPaymentResponse>(
                cancellationToken: cancellationToken);

            return gatewayPayment is null || string.IsNullOrWhiteSpace(gatewayPayment.Id)
                ? Result<GatewayPayment>.Failure("Payment gateway returned an invalid response.")
                : Result<GatewayPayment>.Success(new GatewayPayment(gatewayPayment.Id, gatewayPayment.Status));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<GatewayPayment>.Failure("Payment gateway request timed out.");
        }
        catch (HttpRequestException)
        {
            return Result<GatewayPayment>.Failure("Payment gateway is unavailable.");
        }
    }

    private sealed record GatewayPaymentResponse(string Id, string Status);
}
