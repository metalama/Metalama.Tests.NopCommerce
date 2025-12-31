# Run Roslyn API benchmarks with different Roslyn versions
# Usage: .\run-benchmarks.ps1 [-Versions "4.14.0","5.0.0"]

param(
    [string[]]$Versions = @("4.14.0", "5.0.0"),
    [switch]$Quick
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Roslyn API Benchmarks" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan
Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean "$scriptDir\Roslyn.Api.Benchmarks.csproj" -c Release 2>$null

foreach ($version in $Versions) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Running benchmarks with Roslyn $version" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Restore with specific version
    Write-Host "Restoring packages for Roslyn $version..." -ForegroundColor Yellow
    dotnet restore "$scriptDir\Roslyn.Api.Benchmarks.csproj" -p:RoslynVersion=$version --force

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to restore packages for Roslyn $version" -ForegroundColor Red
        continue
    }

    # Build
    Write-Host "Building with Roslyn $version..." -ForegroundColor Yellow
    dotnet build "$scriptDir\Roslyn.Api.Benchmarks.csproj" -c Release -p:RoslynVersion=$version --no-restore

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to build with Roslyn $version" -ForegroundColor Red
        continue
    }

    # Run benchmarks
    Write-Host "Running benchmarks..." -ForegroundColor Yellow

    $filter = "*"
    if ($Quick) {
        $filter = "*Medium*"
    }

    dotnet run --project "$scriptDir\Roslyn.Api.Benchmarks.csproj" -c Release -p:RoslynVersion=$version --no-build -- --filter $filter

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Benchmark run failed for Roslyn $version" -ForegroundColor Red
    }
    else {
        Write-Host "Completed benchmarks for Roslyn $version" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "All benchmarks completed!" -ForegroundColor Green
Write-Host "Results are in: benchmarks\roslyn-api-results\" -ForegroundColor Yellow
