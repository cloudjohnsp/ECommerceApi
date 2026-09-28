[CmdletBinding()]
param(
    [string]$WorkerPath,
    [string]$PaymentPath,
    [string]$PaymentPythonPath,
    [switch]$SkipPaymentInstall,
    [switch]$AllowDirty,
    [switch]$SkipProjectGates,
    [switch]$SkipImageBuild,
    [switch]$SkipAcceptance,
    [switch]$SkipFullComposeSmoke
)

$ErrorActionPreference = "Stop"
$apiRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$WorkerPath = if ([string]::IsNullOrWhiteSpace($WorkerPath)) {
    [System.IO.Path]::GetFullPath((Join-Path $apiRoot "../../../personal-projects/ECommerceWorker"))
} else { [System.IO.Path]::GetFullPath($WorkerPath) }
$PaymentPath = if ([string]::IsNullOrWhiteSpace($PaymentPath)) {
    [System.IO.Path]::GetFullPath((Join-Path $apiRoot "../../../personal-projects/ECommercePayment"))
} else { [System.IO.Path]::GetFullPath($PaymentPath) }
$revisionLockPath = Join-Path $apiRoot "integration/revisions.json"
$artifactRoot = Join-Path $apiRoot ("TestResults/coordinated/{0:yyyyMMdd-HHmmss}" -f [DateTime]::UtcNow)
$temporaryPaths = [System.Collections.Generic.List[string]]::new()

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )
    Write-Host "==> $Name"
    Push-Location $WorkingDirectory
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
    }
    finally { Pop-Location }
}

function Get-RepositoryState {
    param([string]$Name, [string]$Path, [string]$ExpectedRevision)
    if (-not (Test-Path -LiteralPath (Join-Path $Path ".git"))) {
        throw "$Name repository was not found at '$Path'."
    }
    $revision = (& git -C $Path rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Could not resolve the $Name revision." }
    if ($ExpectedRevision -ne "self" -and $revision -ne $ExpectedRevision) {
        throw "$Name is at '$revision', but integration/revisions.json requires '$ExpectedRevision'."
    }
    $dirty = -not [string]::IsNullOrWhiteSpace((& git -C $Path status --porcelain) -join "`n")
    if ($dirty -and -not $AllowDirty) {
        throw "$Name has uncommitted changes. Commit them or use -AllowDirty for a pre-commit validation."
    }
    [pscustomobject]@{ Name = $Name; Path = $Path; Revision = $revision; Dirty = $dirty }
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Assert-ImageRevision {
    param([string]$Image, [string]$ExpectedRevision)
    $actualRevision = (& docker image inspect $Image --format `
        '{{ index .Config.Labels "org.opencontainers.image.revision" }}').Trim()
    if ($LASTEXITCODE -ne 0) { throw "Image '$Image' was not found." }
    if ($actualRevision -ne $ExpectedRevision) {
        throw "Image '$Image' has revision '$actualRevision'; expected '$ExpectedRevision'."
    }
}

function Set-ProcessEnvironment {
    param([hashtable]$Values)
    $previous = @{}
    foreach ($entry in $Values.GetEnumerator()) {
        $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, "Process")
        [Environment]::SetEnvironmentVariable($entry.Key, [string]$entry.Value, "Process")
    }
    return $previous
}

function Restore-ProcessEnvironment {
    param([hashtable]$Values)
    foreach ($entry in $Values.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, "Process")
    }
}

function Remove-VerifiedTemporaryDirectory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $resolved = [System.IO.Path]::GetFullPath($Path)
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary directory '$resolved'."
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required for the coordinated gate."
}
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$lock = Get-Content -LiteralPath $revisionLockPath -Raw | ConvertFrom-Json
if ($lock.schemaVersion -ne 1) { throw "Unsupported integration revision schema." }

$api = Get-RepositoryState "API" $apiRoot $lock.api.revision
$worker = Get-RepositoryState "Worker" $WorkerPath $lock.worker.revision
$payment = Get-RepositoryState "Payment" $PaymentPath $lock.payment.revision
$repositories = @($api, $worker, $payment)

$python = $PaymentPythonPath
try {
    if (-not $SkipProjectGates) {
        Invoke-Native "Restore acceptance tests" $apiRoot { dotnet restore ECommerceAcceptance.slnx }
        Invoke-Native "Verify acceptance formatting" $apiRoot {
            dotnet format ECommerceAcceptance.slnx --verify-no-changes --no-restore
        }

        & (Join-Path $apiRoot "scripts/verify-local.ps1") -Configuration Release -SkipImage

        & (Join-Path $WorkerPath "scripts/verify-local.ps1") -Configuration Release -SkipImage

        if ([string]::IsNullOrWhiteSpace($python)) {
            $venv = Join-Path ([System.IO.Path]::GetTempPath()) ("ecommerce-payment-gate-" + [Guid]::NewGuid().ToString("N"))
            $temporaryPaths.Add($venv)
            Invoke-Native "Create isolated Payment environment" $PaymentPath {
                python -m venv $venv
            }
            $python = if ($IsWindows) {
                Join-Path $venv "Scripts/python.exe"
            } else { Join-Path $venv "bin/python" }
        }
        & (Join-Path $PaymentPath "scripts/verify-local.ps1") `
            -Python $python -SkipInstall:$SkipPaymentInstall -SkipImage
    }

    $apiTag = "ecommerce-api:sha-$($api.Revision)" + $(if ($api.Dirty) { "-dirty" } else { "" })
    $workerTag = "ecommerce-worker:sha-$($worker.Revision)" + $(if ($worker.Dirty) { "-dirty" } else { "" })
    $paymentTag = "ecommerce-payment:sha-$($payment.Revision)" + $(if ($payment.Dirty) { "-dirty" } else { "" })

    if (-not $SkipImageBuild) {
        Invoke-Native "Build API image" $apiRoot {
            docker build --label "org.opencontainers.image.revision=$($api.Revision)" --tag $apiTag .
        }
        Invoke-Native "Build Worker image" $WorkerPath {
            docker build --label "org.opencontainers.image.revision=$($worker.Revision)" --tag $workerTag .
        }
        Invoke-Native "Build Payment image" $PaymentPath {
            docker build --label "org.opencontainers.image.revision=$($payment.Revision)" --tag $paymentTag .
        }
    }

    Assert-ImageRevision $apiTag $api.Revision
    Assert-ImageRevision $workerTag $worker.Revision
    Assert-ImageRevision $paymentTag $payment.Revision

    $manifest = [ordered]@{
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        repositories = $repositories
        images = @(
            [ordered]@{ service = "api"; tag = $apiTag; id = (& docker image inspect $apiTag --format "{{.Id}}") },
            [ordered]@{ service = "worker"; tag = $workerTag; id = (& docker image inspect $workerTag --format "{{.Id}}") },
            [ordered]@{ service = "payment"; tag = $paymentTag; id = (& docker image inspect $paymentTag --format "{{.Id}}") }
        )
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $artifactRoot "manifest.json")

    $sharedEnvironment = @{
        ECOMMERCE_API_IMAGE = $apiTag
        ECOMMERCE_WORKER_IMAGE = $workerTag
        ECOMMERCE_PAYMENT_IMAGE = $paymentTag
        ECOMMERCE_WORKER_CONTEXT = $WorkerPath.Replace('\', '/')
        ECOMMERCE_PAYMENT_CONTEXT = $PaymentPath.Replace('\', '/')
    }
    $previousSharedEnvironment = Set-ProcessEnvironment $sharedEnvironment
    try {
        if (-not $SkipAcceptance) {
            $previousAcceptance = Set-ProcessEnvironment @{
                RUN_CROSS_SERVICE_ACCEPTANCE_TESTS = "true"
                ECOMMERCE_ACCEPTANCE_REUSE_IMAGES = "true"
            }
            try {
                Invoke-Native "Run cross-service acceptance gate" $apiRoot {
                    dotnet test ECommerceAcceptance.slnx --configuration Release --no-restore
                }
            }
            finally { Restore-ProcessEnvironment $previousAcceptance }
        }

        if (-not $SkipFullComposeSmoke) {
            $projectName = "ecommerce-gate-$([Guid]::NewGuid().ToString('N').Substring(0, 12))"
            $ports = @{
                Api = Get-FreeTcpPort; Payment = Get-FreeTcpPort; Mailpit = Get-FreeTcpPort
                Prometheus = Get-FreeTcpPort; Grafana = Get-FreeTcpPort
                RabbitManagement = Get-FreeTcpPort; Azurite = Get-FreeTcpPort
            }
            $secretSuffix = [Guid]::NewGuid().ToString("N")
            $gateEnvironment = @{
                POSTGRES_USER = "gate"
                POSTGRES_PASSWORD = "postgres-$secretSuffix"
                JWT_SECRET_KEY = "jwt-$secretSuffix"
                PAYMENT_GATEWAY_WEBHOOK_SECRET = "webhook-$secretSuffix"
                RABBITMQ_USER = "gate"
                RABBITMQ_PASSWORD = "rabbit-$secretSuffix"
                GRAFANA_ADMIN_USER = "gate"
                GRAFANA_ADMIN_PASSWORD = "grafana-$secretSuffix"
                ECOMMERCE_API_PORT = $ports.Api
                PAYMENT_GATEWAY_PORT = $ports.Payment
                MAILPIT_PORT = $ports.Mailpit
                PROMETHEUS_PORT = $ports.Prometheus
                GRAFANA_PORT = $ports.Grafana
                RABBITMQ_MANAGEMENT_PORT = $ports.RabbitManagement
                AZURITE_PORT = $ports.Azurite
            }
            $previousGateEnvironment = Set-ProcessEnvironment $gateEnvironment
            $gateEnvironmentPath = Join-Path ([System.IO.Path]::GetTempPath()) ("$projectName.env")
            $temporaryPaths.Add($gateEnvironmentPath)
            ($sharedEnvironment + $gateEnvironment).GetEnumerator() |
                ForEach-Object { "$($_.Key)=$($_.Value)" } |
                Set-Content -LiteralPath $gateEnvironmentPath
            $compose = @("compose", "--project-name", $projectName,
                "--env-file", $gateEnvironmentPath,
                "--file", (Join-Path $apiRoot "docker-compose.yml"))
            try {
                Invoke-Native "Start full coordinated Compose" $apiRoot {
                    docker @compose up --detach --no-build --wait --wait-timeout 300
                }
                & (Join-Path $apiRoot "scripts/smoke-test-local.ps1") `
                    -EnvironmentPath $gateEnvironmentPath -ProjectName $projectName `
                    -ApiPort $ports.Api -PaymentPort $ports.Payment `
                    -MailpitPort $ports.Mailpit -PrometheusPort $ports.Prometheus `
                    -GrafanaPort $ports.Grafana
                if ($LASTEXITCODE -ne 0) { throw "Full Compose smoke test failed." }
            }
            finally {
                (& docker @compose ps --all 2>&1) |
                    Set-Content -LiteralPath (Join-Path $artifactRoot "compose-ps.txt")
                (& docker @compose logs --no-color --timestamps 2>&1) |
                    Set-Content -LiteralPath (Join-Path $artifactRoot "compose.log")
                & docker @compose down --volumes --remove-orphans --timeout 10 | Out-Null
                Restore-ProcessEnvironment $previousGateEnvironment
            }
        }
    }
    finally { Restore-ProcessEnvironment $previousSharedEnvironment }
}
finally {
    foreach ($temporaryPath in $temporaryPaths) {
        if (Test-Path -LiteralPath $temporaryPath -PathType Container) {
            Remove-VerifiedTemporaryDirectory $temporaryPath
        } elseif (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

Write-Host "Coordinated multi-repository gate passed. Artifacts: $artifactRoot"
