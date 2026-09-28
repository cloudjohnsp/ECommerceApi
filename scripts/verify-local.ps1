[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipImage
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$temporaryEnvironmentPath = Join-Path ([System.IO.Path]::GetTempPath()) `
    ("ecommerce-api-gate-{0}.env" -f [Guid]::NewGuid().ToString("N"))

function Invoke-GateStep {
    param([string]$Name, [scriptblock]$Action)
    Write-Host "==> $Name"
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

Push-Location $root
try {
    Invoke-GateStep "Restore API" { dotnet restore ECommerceApi.slnx }
    Invoke-GateStep "Verify API formatting" {
        dotnet format ECommerceApi.slnx --verify-no-changes --no-restore
    }
    Invoke-GateStep "Build API" {
        dotnet build ECommerceApi.slnx --configuration $Configuration --no-restore
    }

    $previousIntegrationSetting = $env:RUN_POSTGRES_INTEGRATION_TESTS
    try {
        $env:RUN_POSTGRES_INTEGRATION_TESTS = "true"
        Invoke-GateStep "Test API with PostgreSQL" {
            dotnet test ECommerceApi.slnx --configuration $Configuration --no-build --no-restore
        }
    }
    finally {
        $env:RUN_POSTGRES_INTEGRATION_TESTS = $previousIntegrationSetting
    }

    Invoke-GateStep "Enforce API coverage" {
        & (Join-Path $PSScriptRoot "verify-unit-coverage.ps1") -Configuration $Configuration
    }

    $secretSuffix = [Guid]::NewGuid().ToString("N")
    @(
        "POSTGRES_PASSWORD=postgres-$secretSuffix"
        "JWT_SECRET_KEY=jwt-$secretSuffix"
        "PAYMENT_GATEWAY_WEBHOOK_SECRET=webhook-$secretSuffix"
        "RABBITMQ_PASSWORD=rabbit-$secretSuffix"
        "GRAFANA_ADMIN_PASSWORD=grafana-$secretSuffix"
    ) | Set-Content -LiteralPath $temporaryEnvironmentPath
    Invoke-GateStep "Validate Docker Compose configuration" {
        docker compose --env-file $temporaryEnvironmentPath `
            --file docker-compose.yml config --quiet
    }

    if (-not $SkipImage) {
        $revision = (& git rev-parse HEAD).Trim()
        $dirty = -not [string]::IsNullOrWhiteSpace((& git status --porcelain) -join "`n")
        $tag = "ecommerce-api:sha-$revision" + $(if ($dirty) { "-dirty" } else { "" })
        Invoke-GateStep "Build API image" {
            docker build --label "org.opencontainers.image.revision=$revision" --tag $tag .
        }
    }
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $temporaryEnvironmentPath -PathType Leaf) {
        Remove-Item -LiteralPath $temporaryEnvironmentPath -Force
    }
}

Write-Host "API local gate passed."
