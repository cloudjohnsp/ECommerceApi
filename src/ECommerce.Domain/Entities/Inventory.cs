using ECommerce.Shared.Results;

namespace ECommerce.Domain.Entities;

public sealed class Inventory : Entity
{
    private int _stock;
    private int _reservedStock;

    public Guid ProductId { get; private set; }
    public int AvailableStock => _stock - _reservedStock;

    private Inventory() { }

    internal Inventory(Guid productId, int stock)
    {
        ProductId = productId;
        _stock = stock;
    }

    internal Result SetStock(int stock)
    {
        if (stock < 0) return Result.Failure("Product stock cannot be negative.");
        if (stock < _reservedStock)
            return Result.Failure("Product stock cannot be less than reserved stock.");

        _stock = stock;
        return Result.Success();
    }

    internal Result Reserve(int quantity)
    {
        if (quantity <= 0) return Result.Failure("Quantity must be greater than zero.");
        if (quantity > AvailableStock) return Result.Failure("Insufficient available stock.");

        _reservedStock += quantity;
        return Result.Success();
    }

    internal Result ConsumeReservation(int quantity)
    {
        if (quantity <= 0) return Result.Failure("Quantity must be greater than zero.");
        if (quantity > _reservedStock) return Result.Failure("Insufficient reserved stock.");

        _reservedStock -= quantity;
        _stock -= quantity;
        return Result.Success();
    }

    internal Result ReleaseReservation(int quantity)
    {
        if (quantity <= 0) return Result.Failure("Quantity must be greater than zero.");
        if (quantity > _reservedStock) return Result.Failure("Insufficient reserved stock.");

        _reservedStock -= quantity;
        return Result.Success();
    }

    internal Result Restore(int quantity)
    {
        if (quantity <= 0) return Result.Failure("Quantity must be greater than zero.");
        _stock = checked(_stock + quantity);
        return Result.Success();
    }
}
