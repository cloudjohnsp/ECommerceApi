namespace ECommerce.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; init; } = true;
    public string Configuration { get; init; } = "localhost:6379,abortConnect=false,connectTimeout=1000";
    public string InstanceName { get; init; } = "ecommerce:";
    public int ProductExpirationMinutes { get; init; } = 5;
}
