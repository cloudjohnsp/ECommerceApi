namespace ECommerce.Api.Options;

public sealed class ApiProtectionOptions
{
    public const string SectionName = "ApiProtection";

    public string[] AllowedOrigins { get; init; } = ["http://localhost:3000"];
    public int GlobalPermitLimit { get; init; } = 100;
    public int AuthenticationPermitLimit { get; init; } = 10;
    public int WindowSeconds { get; init; } = 60;
}
