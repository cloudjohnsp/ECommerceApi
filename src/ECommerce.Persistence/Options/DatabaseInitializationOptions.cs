namespace ECommerce.Persistence.Options;

public sealed class DatabaseInitializationOptions
{
    public const string SectionName = "DatabaseInitialization";

    public bool ApplyMigrationsOnStartup { get; init; }
    public int MaxAttempts { get; init; } = 10;
    public int RetryDelaySeconds { get; init; } = 3;
}
