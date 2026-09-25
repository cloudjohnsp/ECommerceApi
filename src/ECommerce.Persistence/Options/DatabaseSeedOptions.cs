namespace ECommerce.Persistence.Options;

public sealed class DatabaseSeedOptions
{
    public const string SectionName = "DatabaseSeed";

    public bool Enabled { get; init; }
    public bool SeedSampleCatalog { get; init; } = true;
    public string AdministratorEmail { get; init; } = string.Empty;
    public string AdministratorPassword { get; init; } = string.Empty;

    public bool HasAdministratorCredentials =>
        !string.IsNullOrWhiteSpace(AdministratorEmail) &&
        !string.IsNullOrWhiteSpace(AdministratorPassword);
}
