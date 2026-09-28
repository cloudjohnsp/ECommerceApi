using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ECommerce.AcceptanceTests;

public sealed class CrossServiceFixture : IAsyncLifetime
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(8);
    private readonly string _solutionRoot = FindSolutionRoot();
    private readonly string _runId = $"{Environment.ProcessId}-{Guid.NewGuid():N}"[..20];
    private readonly bool _enabled = string.Equals(
        Environment.GetEnvironmentVariable("RUN_CROSS_SERVICE_ACCEPTANCE_TESTS"),
        "true",
        StringComparison.OrdinalIgnoreCase);
    private string ProjectName => $"ecommerce-acceptance-{_runId}";
    private string ComposePath => Path.Combine(_solutionRoot, "acceptance", "docker-compose.acceptance.yml");

    public HttpClient Api { get; private set; } = null!;
    public HttpClient Payment { get; private set; } = null!;
    public string AccessToken { get; private set; } = string.Empty;
    public int RabbitPort { get; private set; }

    public async Task InitializeAsync()
    {
        if (!_enabled) return;

        try
        {
            var reuseImages = string.Equals(
                Environment.GetEnvironmentVariable("ECOMMERCE_ACCEPTANCE_REUSE_IMAGES"),
                "true",
                StringComparison.OrdinalIgnoreCase) &&
                await AcceptanceImagesExistAsync();
            var startupArguments = new List<string>
            {
                "up", "--detach",
                reuseImages ? "--no-build" : "--build",
                "--wait", "--wait-timeout", "300", "--scale", "worker=2"
            };
            await ComposeAsync(startupArguments, CommandTimeout);

            var apiPort = await ResolvePublishedPortAsync("api", 8080);
            var paymentPort = await ResolvePublishedPortAsync("payment", 5000);
            RabbitPort = await ResolvePublishedPortAsync("rabbitmq", 5672);
            Api = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{apiPort}") };
            Payment = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{paymentPort}") };
            AccessToken = await LoginAsync();

            var workerIds = (await ComposeAsync(["ps", "--quiet", "worker"], TimeSpan.FromSeconds(20)))
                .StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (workerIds.Length != 2)
                throw new InvalidOperationException($"Expected two Worker instances, found {workerIds.Length}.");
        }
        catch
        {
            await CaptureDiagnosticsAsync();
            await TryDownAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (!_enabled) return;

        Api?.Dispose();
        Payment?.Dispose();
        await CaptureDiagnosticsAsync();
        await TryDownAsync();
    }

    public HttpRequestMessage CreateApiRequest(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    public async Task<Guid> CreateCustomerAsync(string? discriminator = null)
    {
        var suffix = discriminator ?? Guid.NewGuid().ToString("N");
        using var response = await Api.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Acceptance",
            lastName = "Customer",
            email = $"acceptance-{suffix}@example.test",
            password = "Acceptance!23456"
        });
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Acceptance customer registration failed ({(int)response.StatusCode}): {body}");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("userId").GetGuid();
    }

    public async Task<Guid> CreateProductAsync(int stock = 1, string? discriminator = null)
    {
        var suffix = discriminator ?? Guid.NewGuid().ToString("N");
        using var request = CreateApiRequest(HttpMethod.Post, "/api/products", new
        {
            name = $"Acceptance product {suffix}",
            description = "Cross-service acceptance product",
            price = 19.90m,
            stock
        });
        using var response = await Api.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Acceptance product creation failed ({(int)response.StatusCode}): {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    public async Task<Guid> CreateOrderAsync(Guid customerId, Guid productId, int quantity = 1)
    {
        using var request = CreateApiRequest(HttpMethod.Post, "/api/orders", new
        {
            customerId,
            items = new[] { new { productId, quantity } }
        });
        using var response = await Api.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Acceptance order creation failed ({(int)response.StatusCode}): {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    public async Task<string> SqlAsync(string sql, string database = "ecommerce")
    {
        var result = await ComposeAsync([
            "exec", "-T", "postgres", "psql",
            "--username", "acceptance",
            "--dbname", database,
            "--tuples-only", "--no-align",
            "--command", sql
        ], TimeSpan.FromSeconds(30));
        return result.StandardOutput.Trim();
    }

    public async Task EventuallyAsync(
        Func<Task<bool>> assertion,
        string description,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        Exception? lastException = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                if (await assertion()) return;
                lastException = null;
            }
            catch (Exception exception)
            {
                lastException = exception;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"Timed out waiting for {description}.",
            lastException);
    }

    public Task<CommandResult> ComposeCommandAsync(params string[] arguments) =>
        ComposeAsync(arguments, TimeSpan.FromSeconds(30));

    private async Task<string> LoginAsync()
    {
        using var response = await Api.PostAsJsonAsync("/api/auth/login", new
        {
            email = "acceptance-admin@example.test",
            password = "Acceptance!23456"
        });
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Acceptance admin login failed ({(int)response.StatusCode}): {body}");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Acceptance login returned no access token.");
    }

    private async Task<int> ResolvePublishedPortAsync(string service, int containerPort)
    {
        var result = await ComposeAsync(
            ["port", service, containerPort.ToString()],
            TimeSpan.FromSeconds(20));
        var match = Regex.Match(result.StandardOutput.Trim(), @":(?<port>\d+)$");
        return match.Success
            ? int.Parse(match.Groups["port"].Value)
            : throw new InvalidOperationException(
                $"Could not resolve published port for {service}:{containerPort}: {result.StandardOutput}");
    }

    private Task<CommandResult> ComposeAsync(IEnumerable<string> arguments, TimeSpan timeout)
    {
        var allArguments = new List<string>
        {
            "compose",
            "--project-name", ProjectName,
            "--file", ComposePath
        };
        allArguments.AddRange(arguments);
        return RunAsync("docker", allArguments, timeout);
    }

    private async Task<bool> AcceptanceImagesExistAsync()
    {
        try
        {
            await RunAsync(
                "docker",
                [
                    "image", "inspect",
                    "ecommerce-acceptance-api:local",
                    "ecommerce-acceptance-worker:local",
                    "ecommerce-acceptance-payment:local"
                ],
                TimeSpan.FromSeconds(20));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async Task CaptureDiagnosticsAsync()
    {
        try
        {
            var artifactDirectory = Path.Combine(
                _solutionRoot, "TestResults", "acceptance", _runId);
            Directory.CreateDirectory(artifactDirectory);

            var logs = await ComposeAsync(
                ["logs", "--no-color", "--timestamps"],
                TimeSpan.FromSeconds(30));
            await File.WriteAllTextAsync(
                Path.Combine(artifactDirectory, "compose.log"),
                logs.StandardOutput + logs.StandardError);

            var state = await SqlAsync("""
                SELECT json_build_object(
                    'outbox', (SELECT count(*) FROM "OutboxMessages"),
                    'reservations', (SELECT count(*) FROM inventory_reservations),
                    'consumed', (SELECT count(*) FROM worker.consumed_integration_events)
                );
                """);
            await File.WriteAllTextAsync(
                Path.Combine(artifactDirectory, "persistent-state.json"),
                state);

            var queues = await ComposeAsync(
                ["exec", "-T", "rabbitmq", "rabbitmqctl", "list_queues", "name", "messages_ready", "messages_unacknowledged"],
                TimeSpan.FromSeconds(30));
            await File.WriteAllTextAsync(
                Path.Combine(artifactDirectory, "rabbitmq-queues.txt"),
                queues.StandardOutput + queues.StandardError);
        }
        catch
        {
            // Diagnostics are best effort and must never prevent deterministic cleanup.
        }
    }

    private async Task TryDownAsync()
    {
        try
        {
            await ComposeAsync(
                ["down", "--volumes", "--remove-orphans", "--timeout", "10"],
                TimeSpan.FromMinutes(2));
        }
        catch
        {
            // The original test or startup failure remains the primary failure.
        }
    }

    private async Task<CommandResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        TimeSpan timeout)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = _solutionRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);

        process.StartInfo.Environment["ECOMMERCE_WORKER_CONTEXT"] = ResolveSiblingRepository(
            "ECOMMERCE_WORKER_CONTEXT", "ECommerceWorker");
        process.StartInfo.Environment["ECOMMERCE_PAYMENT_CONTEXT"] = ResolveSiblingRepository(
            "ECOMMERCE_PAYMENT_CONTEXT", "ECommercePayment");

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Command '{fileName}' timed out after {timeout}.");
        }

        var result = new CommandResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command '{fileName} {string.Join(' ', arguments)}' failed with exit code {result.ExitCode}." +
                Environment.NewLine + result.StandardOutput + Environment.NewLine + result.StandardError);
        }

        return result;
    }

    private string ResolveSiblingRepository(string variable, string repositoryName)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(Path.Combine(_solutionRoot, "..", "..", "..", "personal-projects", repositoryName))
            : Path.GetFullPath(configured);
        return Directory.Exists(path)
            ? path
            : throw new DirectoryNotFoundException(
                $"{repositoryName} was not found at '{path}'. Set {variable} to its checkout.");
    }

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ECommerceApi.slnx")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the solution root.");
    }

    public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
