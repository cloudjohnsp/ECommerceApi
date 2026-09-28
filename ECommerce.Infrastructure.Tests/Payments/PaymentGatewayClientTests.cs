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
    public void HttpHandler_ShouldDisableRedirectsAndCookies()
    {
        using var handler = PaymentGatewayHttpMessageHandlerFactory.Create();

        handler.Should().BeOfType<HttpClientHandler>()
            .Which.Should().Match<HttpClientHandler>(configured =>
                !configured.AllowAutoRedirect && !configured.UseCookies);
    }

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
            new CreateGatewayPayment(paymentId, orderId, 199.90m, "BRL", "checkout-123"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExternalPaymentId.Should().Be("pay_123");
        capturedRequest!.Headers.GetValues("Idempotency-Key").Should().ContainSingle(paymentId.ToString());
        capturedBody.Should().Contain($"\"reference\":\"{orderId}\"");
        capturedBody.Should().Contain("\"callbackUrl\":\"http://api/api/webhooks/payments\"");
        capturedBody.Should().Contain("\"correlationId\":\"checkout-123\"");
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

    [Fact]
    public async Task CreateAsync_WhenGatewayRedirects_ReturnsFailureWithoutTreatingItAsSuccess()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
            {
                Headers = { Location = new Uri("http://untrusted.example/payments") }
            }));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://gateway/") };
        var sut = new PaymentGatewayClient(client, CreateOptions());

        var result = await sut.CreateAsync(
            new CreateGatewayPayment(Guid.NewGuid(), Guid.NewGuid(), 10m, "BRL"));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            $"Payment gateway rejected the request with status {(int)HttpStatusCode.TemporaryRedirect}.");
    }

    [Theory]
    [InlineData("not-json", "application/json")]
    [InlineData("{\"id\":\"pay_123\",\"status\":\"pending\"}", "text/plain")]
    [InlineData("{\"id\":\"pay_123\",\"status\":\"\"}", "application/json")]
    public async Task CreateAsync_WhenSuccessResponseCannotBeParsed_ReturnsFailure(
        string responseBody,
        string mediaType)
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, mediaType)
            }));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://gateway/") };
        var sut = new PaymentGatewayClient(client, CreateOptions());

        var result = await sut.CreateAsync(
            new CreateGatewayPayment(Guid.NewGuid(), Guid.NewGuid(), 10m, "BRL"));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Payment gateway returned an invalid response.");
    }

    [Fact]
    public async Task RefundAsync_SendsIdempotentRefundRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":\"pay_123\",\"status\":\"refunded\"}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var sut = new PaymentGatewayClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://gateway/") },
            CreateOptions());
        var paymentId = Guid.NewGuid();

        var result = await sut.RefundAsync(
            new RefundGatewayPayment(paymentId, "pay_123", "customer_request"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("refunded");
        capturedRequest!.RequestUri!.ToString().Should().Be("http://gateway/payments/pay_123/refund");
        capturedRequest.Headers.GetValues("Idempotency-Key")
            .Should().ContainSingle($"{paymentId}:refund");
        capturedBody.Should().Contain("\"reason\":\"customer_request\"");
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
