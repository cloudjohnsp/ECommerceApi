namespace ECommerce.Infrastructure.Options;

public sealed class OutboxProcessorOptions
{
    public const string SectionName = "OutboxProcessor";

    public int PollingIntervalSeconds { get; init; } = 5;
    public int BatchSize { get; init; } = 20;
}
