[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$templatePath = Join-Path $repositoryRoot ".env.example"
$environmentPath = Join-Path $repositoryRoot ".env"

if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
    throw "Environment template was not found at '$templatePath'."
}

function New-LocalSecret {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateRange(16, 128)]
        [int]$ByteCount
    )

    $bytes = New-Object byte[] $ByteCount
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }

    return [Convert]::ToBase64String($bytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")
}

$secretLengths = [ordered]@{
    POSTGRES_PASSWORD                 = 32
    RABBITMQ_PASSWORD                 = 32
    JWT_SECRET_KEY                    = 48
    PAYMENT_GATEWAY_WEBHOOK_SECRET    = 32
    GRAFANA_ADMIN_PASSWORD            = 32
}

$lines = if (Test-Path -LiteralPath $environmentPath -PathType Leaf) {
    [System.Collections.Generic.List[string]]::new([string[]](Get-Content -LiteralPath $environmentPath))
}
else {
    [System.Collections.Generic.List[string]]::new([string[]](Get-Content -LiteralPath $templatePath))
}

$keyIndexes = @{}
for ($index = 0; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -match "^([A-Z0-9_]+)=(.*)$") {
        $keyIndexes[$Matches[1]] = $index
    }
}

$initializedKeys = [System.Collections.Generic.List[string]]::new()
foreach ($entry in $secretLengths.GetEnumerator()) {
    $key = $entry.Key
    $currentValue = $null

    if ($keyIndexes.ContainsKey($key)) {
        $currentValue = $lines[$keyIndexes[$key]].Substring($key.Length + 1)
    }

    if (-not [string]::IsNullOrWhiteSpace($currentValue)) {
        continue
    }

    $line = "$key=$(New-LocalSecret -ByteCount $entry.Value)"
    if ($keyIndexes.ContainsKey($key)) {
        $lines[$keyIndexes[$key]] = $line
    }
    else {
        $keyIndexes[$key] = $lines.Count
        $lines.Add($line)
    }

    $initializedKeys.Add($key)
}

[System.IO.File]::WriteAllLines(
    $environmentPath,
    $lines,
    [System.Text.UTF8Encoding]::new($false))

if ($initializedKeys.Count -eq 0) {
    Write-Output "The local .env already contains every required secret. No values were changed."
}
else {
    Write-Output "Initialized local .env values for: $($initializedKeys -join ', ')."
}

Write-Output "Secrets were not printed and existing non-empty values were preserved."
