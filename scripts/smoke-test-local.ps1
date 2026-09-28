[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$environmentPath = Join-Path $repositoryRoot ".env"
$composePath = Join-Path $repositoryRoot "docker-compose.yml"
$expectedServices = @(
    "postgres-db",
    "rabbitmq",
    "redis",
    "azurite",
    "mailpit",
    "ecommerce-payment",
    "ecommerce-api",
    "ecommerce-worker",
    "prometheus",
    "grafana"
)

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker was not found. Install or start Docker Desktop and try again."
}

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    throw "The local .env was not found. Run scripts/start-local.ps1 first."
}

$composeArguments = @(
    "compose",
    "--env-file", $environmentPath,
    "--file", $composePath
)

function Invoke-Compose {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & docker @composeArguments @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose command failed: $($Arguments -join ' ')"
    }

    return $output
}

function Assert-HttpSuccess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri
    )

    $response = Invoke-WebRequest -Uri $Uri -Method Get -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "GET $Uri returned HTTP $($response.StatusCode)."
    }
}

$configuredServices = @(Invoke-Compose -Arguments @("config", "--services"))
foreach ($service in $expectedServices) {
    if ($configuredServices -notcontains $service) {
        throw "Required Compose service '$service' is missing."
    }

    $containerId = (Invoke-Compose -Arguments @("ps", "--quiet", $service) | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($containerId)) {
        throw "Required Compose service '$service' is not running."
    }

    $stateJson = & docker inspect --format "{{json .State}}" $containerId
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect Compose service '$service'."
    }

    $state = $stateJson | ConvertFrom-Json
    if (-not $state.Running) {
        throw "Compose service '$service' is not running."
    }
    if ($null -eq $state.Health -or $state.Health.Status -ne "healthy") {
        throw "Compose service '$service' is not healthy."
    }
}

Assert-HttpSuccess -Uri "http://127.0.0.1:8080/api/health/ready"
Assert-HttpSuccess -Uri "http://127.0.0.1:5002/health"
Assert-HttpSuccess -Uri "http://127.0.0.1:8025/livez"
Assert-HttpSuccess -Uri "http://127.0.0.1:9090/-/ready"
Assert-HttpSuccess -Uri "http://127.0.0.1:3001/api/health"

Invoke-Compose -Arguments @(
    "exec", "-T", "ecommerce-api",
    "curl", "--fail", "--silent", "--show-error",
    "http://ecommerce-payment:5000/health"
) | Out-Null

$smokeReference = "smoke-$([Guid]::NewGuid().ToString('N'))"
$paymentBody = @{
    amount = 1.00
    currency = "BRL"
    reference = $smokeReference
    correlationId = $smokeReference
} | ConvertTo-Json

$payment = Invoke-RestMethod `
    -Uri "http://127.0.0.1:5002/payments" `
    -Method Post `
    -ContentType "application/json" `
    -Headers @{ "Idempotency-Key" = $smokeReference } `
    -Body $paymentBody `
    -TimeoutSec 10

if ([string]::IsNullOrWhiteSpace($payment.id) -or $payment.status -ne "pending") {
    throw "Payment smoke flow did not create a pending payment."
}

$approvedPayment = Invoke-RestMethod `
    -Uri "http://127.0.0.1:5002/payments/$($payment.id)/approve" `
    -Method Post `
    -ContentType "application/json" `
    -Body "{}" `
    -TimeoutSec 10

if ($approvedPayment.id -ne $payment.id -or $approvedPayment.status -ne "approved") {
    throw "Payment smoke flow did not approve the created payment."
}

$queueOutput = @(Invoke-Compose -Arguments @(
    "exec", "-T", "rabbitmq",
    "rabbitmqctl", "list_queues", "--quiet", "name"
))
foreach ($queueName in @(
    "ecommerce.worker.orders",
    "ecommerce.worker.orders.retry",
    "ecommerce.worker.orders.dead"
)) {
    if ($queueOutput -notcontains $queueName) {
        throw "Worker queue '$queueName' was not declared."
    }
}

Write-Output "Unified local environment smoke test passed."
