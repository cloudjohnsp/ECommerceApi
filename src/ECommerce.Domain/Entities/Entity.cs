namespace ECommerce.Domain.Entities;

public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
}
