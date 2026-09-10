using ECommerce.Shared.Results;

namespace ECommerce.Domain.Entities;

public sealed class Product : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    private Product() { }

    private Product(string name, string description, decimal price, int stock)
    {
        Name = name;
        Description = description;
        Price = price;
        Stock = stock;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<Product> Create(string name, string description, decimal price, int stock)
    {
        var validation = Validate(name, description, price, stock);
        return validation.IsFailure
            ? Result<Product>.Failure([.. validation.Errors])
            : Result<Product>.Success(new Product(name.Trim(), description.Trim(), price, stock));
    }

    public Result Update(string name, string description, decimal price, int stock)
    {
        var validation = Validate(name, description, price, stock);
        if (validation.IsFailure) return validation;

        Name = name.Trim();
        Description = description.Trim();
        Price = price;
        Stock = stock;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive) return Result.Success();
        IsActive = false;
        DeactivatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DeactivatedAt;
        return Result.Success();
    }

    private static Result Validate(string? name, string? description, decimal price, int stock)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            errors.Add("Product name must contain between 1 and 150 characters.");
        if (description?.Trim().Length > 2000)
            errors.Add("Product description cannot exceed 2000 characters.");
        if (price <= 0)
            errors.Add("Product price must be greater than zero.");
        if (stock < 0)
            errors.Add("Product stock cannot be negative.");
        return errors.Count == 0 ? Result.Success() : Result.Failure([.. errors]);
    }
}
