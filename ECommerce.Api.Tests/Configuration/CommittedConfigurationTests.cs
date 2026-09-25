using System.Text.Json;
using FluentAssertions;

namespace ECommerce.Api.Tests.Configuration;

public sealed class CommittedConfigurationTests
{
    [Fact]
    public void AppSettings_DoesNotContainRuntimeSecrets()
    {
        var root = FindSolutionRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "src", "ECommerce.Api", "appsettings.json")));
        var configuration = document.RootElement;

        Read(configuration, "ConnectionStrings", "DefaultConnection").Should().BeEmpty();
        Read(configuration, "Jwt", "SecretKey").Should().BeEmpty();
        Read(configuration, "RabbitMq", "UserName").Should().BeEmpty();
        Read(configuration, "RabbitMq", "Password").Should().BeEmpty();
        Read(configuration, "PaymentGateway", "WebhookSecret").Should().BeEmpty();
        Read(configuration, "DatabaseSeed", "AdministratorPassword").Should().BeEmpty();
    }

    [Fact]
    public void DockerCompose_RequiresSecretsFromEnvironment()
    {
        var root = FindSolutionRoot();
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));
        var environmentTemplate = File.ReadAllLines(Path.Combine(root, ".env.example"));

        compose.Should().Contain("${POSTGRES_PASSWORD:?");
        compose.Should().Contain("${RABBITMQ_PASSWORD:?");
        compose.Should().Contain("${JWT_SECRET_KEY:?");
        compose.Should().Contain("${PAYMENT_GATEWAY_WEBHOOK_SECRET:?");
        compose.Should().Contain("${GRAFANA_ADMIN_PASSWORD:?");
        compose.Should().NotContain("Password=postgres");
        compose.Should().NotContain("RabbitMq__Password: guest");
        compose.Should().NotContain(":-development-secret");
        environmentTemplate.Should().Contain("POSTGRES_PASSWORD=");
        environmentTemplate.Should().Contain("RABBITMQ_PASSWORD=");
        environmentTemplate.Should().Contain("JWT_SECRET_KEY=");
        environmentTemplate.Should().Contain("PAYMENT_GATEWAY_WEBHOOK_SECRET=");
        environmentTemplate.Should().Contain("GRAFANA_ADMIN_PASSWORD=");
        environmentTemplate.Should().Contain("SEED_ADMIN_PASSWORD=");
    }

    [Fact]
    public void ReleaseWorkflow_UsesOidcAndImmutableContainerImage()
    {
        var workflow = File.ReadAllText(Path.Combine(
            FindSolutionRoot(), ".github", "workflows", "release.yml"));

        workflow.Should().Contain("environment: production");
        workflow.Should().Contain("id-token: write");
        workflow.Should().Contain("uses: azure/login@v3");
        workflow.Should().Contain("uses: azure/webapps-deploy@v3");
        workflow.Should().Contain("ecommerce-api:sha-${GITHUB_SHA}");
        workflow.Should().Contain("/api/health/live");
        workflow.Should().NotContain("creds:");
        workflow.Should().NotContain("publish-profile:");
    }

    private static string Read(JsonElement root, string section, string key) =>
        root.GetProperty(section).GetProperty(key).GetString() ?? string.Empty;

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ECommerceApi.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the solution root.");
    }
}
