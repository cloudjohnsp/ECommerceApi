[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$initializerPath = Join-Path $PSScriptRoot "initialize-dev-env.ps1"
$environmentPath = Join-Path $repositoryRoot ".env"
$composePath = Join-Path $repositoryRoot "docker-compose.yml"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker was not found. Install or start Docker Desktop and try again."
}

& $initializerPath

$composeArguments = [System.Collections.Generic.List[string]]::new()
$composeArguments.Add("compose")
$composeArguments.Add("--env-file")
$composeArguments.Add($environmentPath)
$composeArguments.Add("--file")
$composeArguments.Add($composePath)
$composeArguments.Add("up")
$composeArguments.Add("--detach")
$composeArguments.Add("--wait")
$composeArguments.Add("--wait-timeout")
$composeArguments.Add("300")
$composeArguments.Add("--force-recreate")

if (-not $NoBuild) {
    $composeArguments.Add("--build")
}

& docker $composeArguments
if ($LASTEXITCODE -ne 0) {
    throw "Docker Compose failed with exit code $LASTEXITCODE."
}

if (-not $SkipSmokeTest) {
    & (Join-Path $PSScriptRoot "smoke-test-local.ps1")
}

Write-Output "ECommerce local services started successfully."
