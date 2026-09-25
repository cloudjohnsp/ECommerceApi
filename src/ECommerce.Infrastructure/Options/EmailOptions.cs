namespace ECommerce.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; } = true;
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public bool UseSsl { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = "no-reply@ecommerce.local";
    public string FromName { get; init; } = "ECommerce";
    public string PublicAppBaseUrl { get; init; } = "http://localhost:3000";
}
