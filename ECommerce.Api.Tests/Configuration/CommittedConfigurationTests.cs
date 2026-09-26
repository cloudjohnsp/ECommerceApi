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

        using var workerDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "src", "ECommerce.Worker", "appsettings.json")));
        var workerConfiguration = workerDocument.RootElement;
        Read(workerConfiguration, "ConnectionStrings", "WorkerDatabase").Should().BeEmpty();
        Read(workerConfiguration, "RabbitMq", "UserName").Should().BeEmpty();
        Read(workerConfiguration, "RabbitMq", "Password").Should().BeEmpty();
        Read(workerConfiguration, "Email", "Password").Should().BeEmpty();
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
        compose.Should().Contain("ApiProtection__UseHttpsRedirection: \"false\"");
        compose.Should().NotContain("\"8081:8081\"");
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
    public void LaunchProfiles_EnableRedirectionOnlyWhenHttpsEndpointIsAvailable()
    {
        var root = FindSolutionRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root, "src", "ECommerce.Api", "Properties", "launchSettings.json")));
        var profiles = document.RootElement.GetProperty("profiles");
        var httpEnvironment = profiles.GetProperty("http").GetProperty("environmentVariables");
        var httpsEnvironment = profiles.GetProperty("https").GetProperty("environmentVariables");

        httpEnvironment.TryGetProperty("ApiProtection__UseHttpsRedirection", out _).Should().BeFalse();
        httpsEnvironment.GetProperty("ApiProtection__UseHttpsRedirection").GetString().Should().Be("true");
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
        workflow.Should().Contain("ecommerce-worker:sha-${GITHUB_SHA}");
        workflow.Should().Contain("az containerapp update");
        workflow.Should().Contain("AZURE_WORKER_CONTAINER_APP_NAME");
        workflow.Should().Contain("/api/health/live");
        workflow.Should().NotContain("creds:");
        workflow.Should().NotContain("publish-profile:");
    }

    [Fact]
    public void PostmanEnvironment_DoesNotContainCredentials()
    {
        var root = FindSolutionRoot();
        var environmentPath = Path.Combine(
            root, "postman", "Local.postman_environment.json");
        using var document = JsonDocument.Parse(File.ReadAllText(environmentPath));
        var variables = document.RootElement.GetProperty("values")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("key").GetString()!,
                item => item.GetProperty("value").GetString() ?? string.Empty);

        variables["password"].Should().BeEmpty();
        variables["accessToken"].Should().BeEmpty();
        variables["refreshToken"].Should().BeEmpty();
        variables["confirmationToken"].Should().BeEmpty();
        variables["baseUrl"].Should().Be("http://localhost:5000/api/v1");
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
