namespace ECommerce.Api.Options;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string ServiceName { get; init; } = "ECommerce.Api";
    public bool EnablePrometheus { get; init; } = true;
    public string? OtlpEndpoint { get; init; }
}
