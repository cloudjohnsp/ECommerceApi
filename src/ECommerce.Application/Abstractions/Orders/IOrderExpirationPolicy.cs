namespace ECommerce.Application.Abstractions.Orders;

public interface IOrderExpirationPolicy
{
    TimeSpan PaymentLifetime { get; }
}
