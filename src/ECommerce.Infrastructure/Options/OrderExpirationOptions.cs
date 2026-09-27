namespace ECommerce.Infrastructure.Options;

public sealed class OrderExpirationOptions
{
    public const string SectionName = "OrderExpiration";

    public bool Enabled { get; init; } = true;
    public int PaymentLifetimeMinutes { get; init; } = 30;
    public string CronExpression { get; init; } = "* * * * *";
    public int BatchSize { get; init; } = 20;
}
