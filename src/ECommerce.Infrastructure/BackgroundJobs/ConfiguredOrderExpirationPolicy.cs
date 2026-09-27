using ECommerce.Application.Abstractions.Orders;
using ECommerce.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.BackgroundJobs;

public sealed class ConfiguredOrderExpirationPolicy(
    IOptions<OrderExpirationOptions> options) : IOrderExpirationPolicy
{
    public TimeSpan PaymentLifetime { get; } =
        TimeSpan.FromMinutes(options.Value.PaymentLifetimeMinutes);
}
