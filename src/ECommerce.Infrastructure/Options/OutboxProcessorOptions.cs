namespace ECommerce.Infrastructure.Options;

public sealed class OutboxProcessorOptions
{
    public const string SectionName = "OutboxProcessor";

    public bool Enabled { get; init; } = true;
    public bool PublishIntegrationEvents { get; init; }
    public string CronExpression { get; init; } = "* * * * *";
    public int BatchSize { get; init; } = 20;
    public int WorkerCount { get; init; } = 3;
}
