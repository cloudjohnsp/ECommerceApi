using System.Net;
using System.Text;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Infrastructure.Options;
using ECommerce.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Tests.Payments;

public sealed class PaymentGatewayClientTests
{
    [Fact]
    public async Task CreateAsync_SendsGatewayContractAndParsesResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":\"pay_123\",\"status\":\"pending\"}", Encoding.UTF8, "application/json")
            };
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://gateway/") };
        var sut = new PaymentGatewayClient(client, CreateOptions());
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var result = await sut.CreateAsync(
            new CreateGatewayPayment(paymentId, orderId, 199.90m, "BRL"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExternalPaymentId.Should().Be("pay_123");
        capturedRequest!.Headers.GetValues("Idempotency-Key").Should().ContainSingle(paymentId.ToString());
        capturedBody.Should().Contain($"\"reference\":\"{orderId}\"");
        capturedBody.Should().Contain("\"callbackUrl\":\"http://api/api/webhooks/payments\"");
    }

    [Fact]
    public async Task CreateAsync_WhenGatewayIsUnavailable_ReturnsFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException());
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://gateway/") };
        var sut = new PaymentGatewayClient(client, CreateOptions());

        var result = await sut.CreateAsync(
            new CreateGatewayPayment(Guid.NewGuid(), Guid.NewGuid(), 10m, "BRL"));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Payment gateway is unavailable.");
    }

    private static IOptions<PaymentGatewayOptions> CreateOptions() =>
        Microsoft.Extensions.Options.Options.Create(new PaymentGatewayOptions
    {
        BaseUrl = "http://gateway",
        CallbackUrl = "http://api/api/webhooks/payments",
        WebhookSecret = "test-secret",
        TimeoutSeconds = 5
        });

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request);
    }
}
