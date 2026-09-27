using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;

namespace ECommerce.Domain.Entities;

public sealed class InventoryReservation : Entity
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(30);

    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid InventoryId { get; private set; }
    public int Quantity { get; private set; }
    public InventoryReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private InventoryReservation() { }

    private InventoryReservation(
        Guid orderId,
        Guid productId,
        Guid inventoryId,
        int quantity,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        OrderId = orderId;
        ProductId = productId;
        InventoryId = inventoryId;
        Quantity = quantity;
        Status = InventoryReservationStatus.Active;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public static Result<InventoryReservation> Create(
        Guid orderId,
        Guid productId,
        Guid inventoryId,
        int quantity,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        var errors = new List<string>();
        if (orderId == Guid.Empty) errors.Add("Order id is required.");
        if (productId == Guid.Empty) errors.Add("Product id is required.");
        if (inventoryId == Guid.Empty) errors.Add("Inventory id is required.");
        if (quantity <= 0) errors.Add("Reservation quantity must be greater than zero.");
        if (expiresAt <= createdAt) errors.Add("Reservation expiration must be after its creation.");

        return errors.Count == 0
            ? Result<InventoryReservation>.Success(
                new InventoryReservation(
                    orderId,
                    productId,
                    inventoryId,
                    quantity,
                    createdAt,
                    expiresAt))
            : Result<InventoryReservation>.Failure([.. errors]);
    }

    public Result Consume(DateTimeOffset completedAt) =>
        Complete(InventoryReservationStatus.Consumed, completedAt);

    public Result Release(DateTimeOffset completedAt) =>
        Complete(InventoryReservationStatus.Released, completedAt);

    public Result Expire(DateTimeOffset completedAt)
    {
        if (Status == InventoryReservationStatus.Expired) return Result.Success();
        if (completedAt < ExpiresAt)
            return Result.Failure("An active reservation cannot expire before its deadline.");

        return Complete(InventoryReservationStatus.Expired, completedAt);
    }

    private Result Complete(InventoryReservationStatus targetStatus, DateTimeOffset completedAt)
    {
        if (Status == targetStatus) return Result.Success();
        if (Status != InventoryReservationStatus.Active)
            return Result.Failure($"A {Status} reservation cannot transition to {targetStatus}.");
        if (completedAt < CreatedAt)
            return Result.Failure("Reservation completion cannot precede its creation.");

        Status = targetStatus;
        CompletedAt = completedAt;
        return Result.Success();
    }
}
