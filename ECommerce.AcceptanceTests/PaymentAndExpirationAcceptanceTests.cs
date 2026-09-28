using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ECommerce.AcceptanceTests;

[Collection(CrossServiceCollection.Name)]
public sealed class PaymentAndExpirationAcceptanceTests(CrossServiceFixture fixture)
{
    [CrossServiceFact]
    public async Task DeclinedAttempt_KeepsOrderPending_AllowsRetry_AndApprovedWebhookCompletesFlow()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var customerId = await fixture.CreateCustomerAsync($"payment-{suffix}");
        var productId = await fixture.CreateProductAsync(discriminator: $"payment-{suffix}");
        var orderId = await fixture.CreateOrderAsync(customerId, productId);

        var firstPayment = await CreatePaymentAsync(orderId, $"declined-{suffix}");
        var firstExternalId = firstPayment.GetProperty("externalPaymentId").GetString();
        firstExternalId.Should().NotBeNullOrWhiteSpace();

        using var decline = await fixture.Payment.PostAsJsonAsync(
            $"/payments/{Uri.EscapeDataString(firstExternalId!)}/decline",
            new { reason = "card_declined" });
        var declineBody = await decline.Content.ReadAsStringAsync();
        decline.StatusCode.Should().Be(HttpStatusCode.OK, declineBody);
        using (var declineJson = JsonDocument.Parse(declineBody))
        {
            declineJson.RootElement.GetProperty("status").GetString().Should().Be("declined");
            declineJson.RootElement.GetProperty("webhook").GetProperty("delivered")
                .GetBoolean().Should().BeTrue();
        }

        await fixture.EventuallyAsync(async () =>
            await fixture.SqlAsync($$"""
                SELECT status || ':' ||
                       (SELECT status FROM orders WHERE "Id" = '{{orderId}}') || ':' ||
                       (SELECT status FROM inventory_reservations WHERE order_id = '{{orderId}}')
                FROM payments
                WHERE "Id" = '{{firstPayment.GetProperty("id").GetGuid()}}';
                """) == "3:1:1",
            "the signed declined webhook to fail only the payment attempt");

        var secondPayment = await CreatePaymentAsync(orderId, $"approved-{suffix}");
        var secondExternalId = secondPayment.GetProperty("externalPaymentId").GetString();
        secondExternalId.Should().NotBeNullOrWhiteSpace();
        (await fixture.SqlAsync($$"""
            SELECT count(*) FROM payments WHERE order_id = '{{orderId}}';
            """)).Should().Be("2");

        using var approve = await fixture.Payment.PostAsync(
            $"/payments/{Uri.EscapeDataString(secondExternalId!)}/approve",
            content: null);
        var approveBody = await approve.Content.ReadAsStringAsync();
        approve.StatusCode.Should().Be(HttpStatusCode.OK, approveBody);
        using (var approveJson = JsonDocument.Parse(approveBody))
        {
            approveJson.RootElement.GetProperty("webhook").GetProperty("delivered")
                .GetBoolean().Should().BeTrue();
        }

        await fixture.EventuallyAsync(async () =>
            await fixture.SqlAsync($$"""
                SELECT o.status || ':' || r.status || ':' || i.stock || ':' || i.reserved_stock
                FROM orders o
                JOIN inventory_reservations r ON r.order_id = o."Id"
                JOIN inventories i ON i."Id" = r.inventory_id
                WHERE o."Id" = '{{orderId}}';
                """) == "2:2:0:0",
            "the approved simulator webhook to pay the order and consume its reservation");
    }

    [CrossServiceFact]
    public async Task ExpiredOrder_ReleasesReservation_AndPublishesCancellation()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var customerId = await fixture.CreateCustomerAsync($"expiration-{suffix}");
        var productId = await fixture.CreateProductAsync(discriminator: $"expiration-{suffix}");
        var orderId = await fixture.CreateOrderAsync(customerId, productId);

        await fixture.SqlAsync($$"""
            UPDATE orders
            SET created_at = now() - interval '3 minutes',
                expires_at = now() - interval '2 minutes'
            WHERE "Id" = '{{orderId}}';
            UPDATE inventory_reservations
            SET created_at = now() - interval '3 minutes',
                expires_at = now() - interval '2 minutes'
            WHERE order_id = '{{orderId}}';
            """);

        await fixture.EventuallyAsync(async () =>
            await fixture.SqlAsync($$"""
                SELECT o.status || ':' || r.status || ':' || i.stock || ':' || i.reserved_stock
                FROM orders o
                JOIN inventory_reservations r ON r.order_id = o."Id"
                JOIN inventories i ON i."Id" = r.inventory_id
                WHERE o."Id" = '{{orderId}}';
                """) == "3:4:1:0",
            "the expiration job to cancel the order and release reserved stock",
            TimeSpan.FromSeconds(90));

        await fixture.EventuallyAsync(async () =>
        {
            var state = await fixture.SqlAsync($$"""
                SELECT count(*)
                FROM "OutboxMessages" o
                JOIN worker.consumed_integration_events c ON c."MessageId" = o."Id"
                WHERE o."Type" = 10
                  AND o."Payload"::jsonb ->> 'OrderId' = '{{orderId}}'
                  AND o."ProcessedAt" IS NOT NULL;
                """);
            return state == "1";
        }, "the cancellation event to be published and consumed", TimeSpan.FromSeconds(45));
    }

    private async Task<JsonElement> CreatePaymentAsync(Guid orderId, string idempotencyKey)
    {
        using var request = fixture.CreateApiRequest(HttpMethod.Post, "/api/payments", new
        {
            orderId,
            currency = "BRL",
            idempotencyKey
        });
        using var response = await fixture.Api.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
