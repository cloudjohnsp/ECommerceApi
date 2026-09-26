using ECommerce.Domain.Enums;

namespace ECommerce.Api.Contracts.Orders;

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);
public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyCollection<CreateOrderItemRequest> Items);
public sealed record AddOrderItemRequest(Guid ProductId, int Quantity);
public sealed record UpdateOrderRequest(OrderStatus Status);

public sealed class OrderSearchRequest
{
    public OrderStatus? Status { get; init; }
    public DateTimeOffset? CreatedFromUtc { get; init; }
    public DateTimeOffset? CreatedToUtc { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public bool Descending { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
