using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;

namespace ECommerce.Domain.Entities;

public sealed class Order : Entity
{
    private readonly List<OrderItem> _items = [];

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items;
    public decimal Total => _items.Sum(item => item.Subtotal);
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    private Order() { }

    private Order(Guid customerId)
    {
        CustomerId = customerId;
        Status = OrderStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<Order> Create(Guid customerId)
    {
        return customerId == Guid.Empty
            ? Result<Order>.Failure("Customer id is required.")
            : Result<Order>.Success(new Order(customerId));
    }

    public Result AddItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (Status != OrderStatus.Pending) return Result.Failure("Items can only be added to pending orders.");
        if (productId == Guid.Empty) return Result.Failure("Product id is required.");
        if (string.IsNullOrWhiteSpace(productName)) return Result.Failure("Product name is required.");
        if (unitPrice <= 0) return Result.Failure("Unit price must be greater than zero.");
        if (quantity <= 0) return Result.Failure("Quantity must be greater than zero.");

        var existing = _items.FirstOrDefault(item => item.ProductId == productId);
        if (existing is not null) return Result.Failure("Product is already included in the order.");

        _items.Add(new OrderItem(productId, productName.Trim(), unitPrice, quantity));
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result MarkAsPaid()
    {
        if (Status == OrderStatus.Cancelled) return Result.Failure("A cancelled order cannot be paid.");
        if (Status == OrderStatus.Paid) return Result.Success();
        if (_items.Count == 0) return Result.Failure("An empty order cannot be paid.");

        Status = OrderStatus.Paid;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == OrderStatus.Paid) return Result.Failure("A paid order cannot be cancelled.");
        if (Status == OrderStatus.Cancelled) return Result.Success();

        Status = OrderStatus.Cancelled;
        CancelledAt = DateTimeOffset.UtcNow;
        UpdatedAt = CancelledAt;
        return Result.Success();
    }
}
