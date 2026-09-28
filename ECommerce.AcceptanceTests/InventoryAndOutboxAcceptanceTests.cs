using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ECommerce.AcceptanceTests;

[Collection(CrossServiceCollection.Name)]
public sealed class InventoryAndOutboxAcceptanceTests(CrossServiceFixture fixture)
{
    [CrossServiceFact]
    public async Task ConcurrentOrders_ForLastUnit_ReserveOnceAndReachWorkerExactlyOnce()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var productRequest = fixture.CreateApiRequest(
            HttpMethod.Post,
            "/api/products",
            new
            {
                name = $"Last unit {suffix}",
                description = "Cross-service acceptance product",
                price = 19.90m,
                stock = 1
            });
        using var productResponse = await fixture.Api.SendAsync(productRequest);
        var productBody = await productResponse.Content.ReadAsStringAsync();
        productResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"product creation returned {productBody}");
        using var productJson = JsonDocument.Parse(productBody);
        var productId = productJson.RootElement.GetProperty("id").GetGuid();

        var firstCustomer = await fixture.CreateCustomerAsync($"first-{suffix}");
        var secondCustomer = await fixture.CreateCustomerAsync($"second-{suffix}");
        var first = CreateOrderAsync(firstCustomer, productId);
        var second = CreateOrderAsync(secondCustomer, productId);
        var responses = await Task.WhenAll(first, second);

        var responseDetails = string.Join(
            Environment.NewLine,
            await Task.WhenAll(responses.Select(async response =>
                $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}")));
        responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(
            1,
            $"the competing responses were:{Environment.NewLine}{responseDetails}");
        responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest).Should().Be(
            1,
            $"the competing responses were:{Environment.NewLine}{responseDetails}");
        var winner = responses.Single(response => response.StatusCode == HttpStatusCode.Created);
        var winnerBody = await winner.Content.ReadAsStringAsync();
        using var winnerJson = JsonDocument.Parse(winnerBody);
        var orderId = winnerJson.RootElement.GetProperty("id").GetGuid();

        var activeReservations = await fixture.SqlAsync($$"""
            SELECT count(*)
            FROM inventory_reservations
            WHERE product_id = '{{productId}}' AND status = 1;
            """);
        activeReservations.Should().Be("1");
        var inventory = await fixture.SqlAsync($$"""
            SELECT stock || ':' || reserved_stock
            FROM inventories
            WHERE product_id = '{{productId}}';
            """);
        inventory.Should().Be("1:1");

        string? outboxId = null;
        await fixture.EventuallyAsync(async () =>
        {
            outboxId = await fixture.SqlAsync($$"""
                SELECT "Id"
                FROM "OutboxMessages"
                WHERE "Type" = 1
                  AND "Payload"::jsonb ->> 'OrderId' = '{{orderId}}'
                  AND "Status" = 2
                  AND "ProcessedAt" IS NOT NULL;
                """);
            return Guid.TryParse(outboxId, out _);
        }, "the API outbox message to be confirmed and marked processed");

        await fixture.EventuallyAsync(async () =>
        {
            var consumed = await fixture.SqlAsync($$"""
                SELECT count(*)
                FROM worker.consumed_integration_events
                WHERE "MessageId" = '{{outboxId}}';
                """);
            return consumed == "1";
        }, "the independently competing Workers to consume the event exactly once");

        foreach (var response in responses) response.Dispose();
    }

    private async Task<HttpResponseMessage> CreateOrderAsync(Guid customerId, Guid productId)
    {
        using var request = fixture.CreateApiRequest(
            HttpMethod.Post,
            "/api/orders",
            new
            {
                customerId,
                items = new[] { new { productId, quantity = 1 } }
            });
        return await fixture.Api.SendAsync(request);
    }
}
