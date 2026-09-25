param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$solutionRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$minimumLineCoverage = 80.0
$targets = @(
    @{
        Project = "ECommerce.Domain.Tests/ECommerce.Domain.Tests.csproj"
        Assembly = "ECommerce.Domain"
    },
    @{
        Project = "ECommerce.Application.Tests/ECommerce.Application.Tests.csproj"
        Assembly = "ECommerce.Application"
    }
)

foreach ($target in $targets) {
    $resultsDirectory = Join-Path ([System.IO.Path]::GetTempPath()) (
        "ecommerce-coverage-" + [System.Guid]::NewGuid().ToString("N"))

    try {
        & dotnet test (Join-Path $solutionRoot $target.Project) `
            --configuration $Configuration `
            --no-build `
            --no-restore `
            --collect:"XPlat Code Coverage" `
            --results-directory $resultsDirectory

        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed for $($target.Project)."
        }

        $report = Get-ChildItem -LiteralPath $resultsDirectory `
            -Filter "coverage.cobertura.xml" `
            -Recurse | Select-Object -First 1
        if ($null -eq $report) {
            throw "Coverage report was not generated for $($target.Project)."
        }

        [xml]$coverage = Get-Content -LiteralPath $report.FullName
        $package = @($coverage.coverage.packages.package) |
            Where-Object { $_.name -eq $target.Assembly } |
            Select-Object -First 1
        if ($null -eq $package) {
            throw "Assembly $($target.Assembly) was not found in the coverage report."
        }

        $lineRate = [double]::Parse(
            $package.'line-rate',
            [System.Globalization.CultureInfo]::InvariantCulture) * 100
        Write-Host ("{0}: {1:N2}% line coverage" -f $target.Assembly, $lineRate)

        if ($lineRate -le $minimumLineCoverage) {
            throw ("{0} line coverage must be greater than {1:N2}%, but was {2:N2}%." -f `
                $target.Assembly, $minimumLineCoverage, $lineRate)
        }
    }
    finally {
        $resolvedResults = [System.IO.Path]::GetFullPath($resultsDirectory)
        $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if ($resolvedResults.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
            (Test-Path -LiteralPath $resolvedResults)) {
            Remove-Item -LiteralPath $resolvedResults -Recurse -Force
        }
    }
}
