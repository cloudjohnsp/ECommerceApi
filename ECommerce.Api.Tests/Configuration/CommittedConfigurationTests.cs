using System.Text.Json;
using System.Text.RegularExpressions;
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
    public void DesignTimeFactories_DoNotEmbedDatabasePasswords()
    {
        var root = FindSolutionRoot();
        var apiFactory = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ECommerce.Persistence",
            "Contexts",
            "DesignTimeDbContextFactory.cs"));
        apiFactory.Should().Contain("ECOMMERCE_DESIGN_TIME_CONNECTION_STRING");
        apiFactory.Should().NotContain("Password=");
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
        compose.Should().Contain("image: ${ECOMMERCE_WORKER_IMAGE:-ecommerce-worker:local}");
        compose.Should().Contain("ApiOutboxPublisher__Enabled: \"true\"");
        compose.Should().NotContain("dockerfile: Dockerfile.worker");
        environmentTemplate.Should().Contain("POSTGRES_PASSWORD=");
        environmentTemplate.Should().Contain("RABBITMQ_PASSWORD=");
        environmentTemplate.Should().Contain("JWT_SECRET_KEY=");
        environmentTemplate.Should().Contain("PAYMENT_GATEWAY_WEBHOOK_SECRET=");
        environmentTemplate.Should().Contain("GRAFANA_ADMIN_PASSWORD=");
        environmentTemplate.Should().Contain("SEED_ADMIN_PASSWORD=");
    }

    [Fact]
    public void IntegrationEventPublisher_IsOwnedByIndependentWorker()
    {
        var root = FindSolutionRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "src", "ECommerce.Api", "appsettings.json")));

        document.RootElement
            .GetProperty("OutboxProcessor")
            .GetProperty("PublishIntegrationEvents")
            .GetBoolean()
            .Should().BeFalse();
    }

    [Fact]
    public void DockerCompose_PublishesDevelopmentPortsOnlyOnLoopback()
    {
        var compose = File.ReadAllText(Path.Combine(FindSolutionRoot(), "docker-compose.yml"));
        var publishedPorts = Regex.Matches(
                compose,
                "(?m)^\\s*-\\s*\"(?<binding>(?:127\\.0\\.0\\.1:)?\\d+:\\d+)\"\\s*$")
            .Select(match => match.Groups["binding"].Value)
            .ToArray();

        publishedPorts.Should().NotBeEmpty();
        publishedPorts.Should().OnlyContain(binding =>
            binding.StartsWith("127.0.0.1:", StringComparison.Ordinal));
    }

    [Fact]
    public void DockerBuildContext_ExcludesSecretsGitMetadataAndTestArtifacts()
    {
        var dockerIgnore = File.ReadAllLines(Path.Combine(FindSolutionRoot(), ".dockerignore"));

        dockerIgnore.Should().Contain("**/.env*");
        dockerIgnore.Should().Contain("**/.git");
        dockerIgnore.Should().Contain("**/.agents");
        dockerIgnore.Should().Contain("**/.codex");
        dockerIgnore.Should().Contain("**/*.Tests");
        dockerIgnore.Should().NotContain(line => line.StartsWith("!.git", StringComparison.Ordinal));
    }

    [Fact]
    public void ApiRuntimeImage_UsesNonRootUserAndCopiesOnlyApplicationSources()
    {
        var dockerfile = File.ReadAllText(Path.Combine(FindSolutionRoot(), "Dockerfile"));

        dockerfile.Should().Contain("USER $APP_UID");
        dockerfile.Should().Contain("COPY [\"src/\", \"src/\"]");
        dockerfile.Should().NotContain("COPY . .");
    }

    [Fact]
    public void LocalStartupScript_InitializesSecretsAndUsesExplicitComposeFiles()
    {
        var root = FindSolutionRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "start-local.ps1"));

        script.Should().Contain("initialize-dev-env.ps1");
        script.Should().Contain("Get-Command docker");
        script.Should().Contain("--env-file");
        script.Should().Contain("docker-compose.yml");
        script.Should().Contain("--detach");
        script.Should().Contain("--build");
        script.Should().Contain("$LASTEXITCODE");
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
    public void ForwardedHeaders_AreDisabledByDefaultAndDocumentedForIsolatedIngress()
    {
        var root = FindSolutionRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root, "src", "ECommerce.Api", "appsettings.json")));
        var apiProtection = document.RootElement.GetProperty("ApiProtection");
        var deploymentGuide = File.ReadAllText(Path.Combine(root, "docs", "azure-deployment.md"));

        apiProtection.GetProperty("UseForwardedHeaders").GetBoolean().Should().BeFalse();
        deploymentGuide.Should().Contain("ApiProtection__UseForwardedHeaders=true");
        deploymentGuide.Should().Contain("Não habilite essa opção quando o processo puder receber tráfego direto");
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
        workflow.Should().NotContain("ecommerce-worker:");
        workflow.Should().NotContain("AZURE_WORKER_CONTAINER_APP_NAME");
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
        variables["newPassword"].Should().BeEmpty();
        variables["accessToken"].Should().BeEmpty();
        variables["refreshToken"].Should().BeEmpty();
        variables["confirmationToken"].Should().BeEmpty();
        variables["resetToken"].Should().BeEmpty();
        variables["productImagePath"].Should().BeEmpty();
        variables["baseUrl"].Should().Be("http://localhost:5000/api/v1");
    }

    [Fact]
    public void PostmanCollection_CoversPublicApiOperations()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindSolutionRoot(), "postman", "ECommerceApi.postman_collection.json")));
        var operations = ReadPostmanOperations(document.RootElement.GetProperty("item"))
            .ToHashSet(StringComparer.Ordinal);
        string[] expectedOperations =
        [
            "GET {{hostUrl}}/api/health/live",
            "POST {{baseUrl}}/auth/register",
            "POST {{baseUrl}}/auth/confirm-email",
            "POST {{baseUrl}}/auth/login",
            "POST {{baseUrl}}/auth/refresh",
            "POST {{baseUrl}}/auth/logout",
            "POST {{baseUrl}}/auth/forgot-password",
            "POST {{baseUrl}}/auth/reset-password",
            "GET {{baseUrl}}/categories",
            "GET {{baseUrl}}/categories/{{categoryId}}",
            "POST {{baseUrl}}/categories",
            "PUT {{baseUrl}}/categories/{{categoryId}}",
            "DELETE {{baseUrl}}/categories/{{categoryId}}",
            "GET {{baseUrl}}/products",
            "GET {{baseUrl}}/products/{{productId}}",
            "GET {{baseUrl}}/products/{{productId}}/images",
            "POST {{baseUrl}}/products/{{productId}}/images",
            "POST {{baseUrl}}/products",
            "PUT {{baseUrl}}/products/{{productId}}",
            "DELETE {{baseUrl}}/products/{{productId}}",
            "GET {{baseUrl}}/admin/dashboard",
            "GET {{baseUrl}}/admin/reports/sales",
            "GET {{baseUrl}}/orders",
            "GET {{baseUrl}}/orders/search",
            "GET {{baseUrl}}/orders/{{orderId}}",
            "POST {{baseUrl}}/orders",
            "POST {{baseUrl}}/orders/{{orderId}}/items",
            "PUT {{baseUrl}}/orders/{{orderId}}",
            "DELETE {{baseUrl}}/orders/{{cancelOrderId}}",
            "GET {{baseUrl}}/payments/{{orderId}}",
            "POST {{baseUrl}}/payments",
            "POST {{baseUrl}}/payments/{{orderId}}/refund",
            "GET {{baseUrl}}/user/{{userId}}",
            "GET {{baseUrl}}/user/{{userId}}/history",
            "PUT {{baseUrl}}/user/{{userId}}/profile",
            "PUT {{baseUrl}}/user/{{userId}}/password",
            "PUT {{baseUrl}}/user/{{userId}}/role",
            "DELETE {{baseUrl}}/user/{{userId}}"
        ];

        operations.Should().Contain(expectedOperations);
    }

    [Fact]
    public void PostmanCollection_ReferencesOnlyDeclaredOrGeneratedVariables()
    {
        var root = FindSolutionRoot();
        var collectionJson = File.ReadAllText(Path.Combine(
            root, "postman", "ECommerceApi.postman_collection.json"));
        using var environmentDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root, "postman", "Local.postman_environment.json")));
        var declaredVariables = environmentDocument.RootElement.GetProperty("values")
            .EnumerateArray()
            .Select(item => item.GetProperty("key").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        declaredVariables.UnionWith(["$randomInt", "reportFromUtc", "reportToUtc"]);

        var referencedVariables = Regex.Matches(collectionJson, "\\{\\{([^{}]+)\\}\\}")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        referencedVariables.Should().BeSubsetOf(declaredVariables);
        collectionJson.Should().Contain("pm.response.json().userId");
    }

    private static string Read(JsonElement root, string section, string key) =>
        root.GetProperty(section).GetProperty(key).GetString() ?? string.Empty;

    private static IEnumerable<string> ReadPostmanOperations(JsonElement items)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("item", out var children))
            {
                foreach (var operation in ReadPostmanOperations(children))
                    yield return operation;
                continue;
            }

            var request = item.GetProperty("request");
            var method = request.GetProperty("method").GetString();
            var rawUrl = request.GetProperty("url").GetString()!;
            var path = rawUrl.Split('?', 2)[0];
            yield return $"{method} {path}";
        }
    }

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ECommerceApi.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the solution root.");
    }
}
