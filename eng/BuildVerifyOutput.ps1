# Builds a single project (or the whole solution) under whitespace / roundtrip
# diagnostic mode:
#   * BENCHMARK preprocessor symbol -> AddLoggingFabric applies LogAspect to
#     100 % of types and members (see src/Metalama.Aspects/.../BenchmarkAspects.cs).
#   * MetalamaVerifyOutputCode=true -> SyntaxTreeVerifier round-trips every
#     transformed syntax tree through ToString() / ParseText and reports
#     LAMA9999 + Roslyn parse errors when the generated text does not parse.
#   * MetalamaDebugTransformedCode=true -> formatted output and writes the
#     transformed *.cs files (consumed by Metalama.Compiler) so failures can
#     be inspected directly.
#
# Usage (from repo root, inside the dev container):
#   pwsh ./eng/BuildVerifyOutput.ps1 src/Libraries/Nop.Core/Nop.Core.csproj
#   pwsh ./eng/BuildVerifyOutput.ps1            # rebuilds the whole solution
#   pwsh ./eng/BuildVerifyOutput.ps1 src/Libraries/Nop.Core/Nop.Core.csproj -NoRebuild
#
# After a fix in the Metalama framework repo, ALWAYS run `./Build.ps1 build` in
# C:\src\Metalama-2026.1\Metalama before re-running this script (the local
# package version bumps every time, so no NuGet cache cleanup is needed).

[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Position = 0)]
    [string]$Project = 'src/NopCommerce.sln',

    [string]$Configuration = 'Debug',

    [string]$TargetFramework,

    # Skip the implicit clean of the project's bin/obj. Use when iterating fast
    # and you want incremental.
    [switch]$NoRebuild,

    # Extra args forwarded verbatim to `dotnet build`.
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedProject = Resolve-Path -Path (Join-Path $repoRoot $Project) -ErrorAction Stop

Write-Host ""
Write-Host "Whitespace-diagnostic build" -ForegroundColor Cyan
Write-Host "  Project         : $resolvedProject" -ForegroundColor Cyan
Write-Host "  Configuration   : $Configuration" -ForegroundColor Cyan
Write-Host "  ExtraConstants  : BENCHMARK" -ForegroundColor Cyan
Write-Host "  VerifyOutputCode: true" -ForegroundColor Cyan
Write-Host "  DebugTransformed: true" -ForegroundColor Cyan
Write-Host ""

if (-not $NoRebuild)
{
    # Drop the obj/bin under the project's directory to force the Metalama
    # source generator to re-run. Targeted clean rather than `dotnet clean`
    # because we want to invalidate even when MSBuild thinks the inputs match.
    $projectDir =
        if ((Get-Item $resolvedProject).PSIsContainer) { $resolvedProject }
        else { Split-Path -Parent $resolvedProject }

    foreach ($sub in @('obj', 'bin'))
    {
        $path = Join-Path $projectDir $sub
        if (Test-Path $path)
        {
            Write-Host "Removing $path" -ForegroundColor DarkGray
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

$dotnetArgs = @(
    'build'
    "$resolvedProject"
    "-c", $Configuration
    '-p:ExtraConstants=BENCHMARK'
    '-p:MetalamaVerifyOutputCode=true'
    '-p:MetalamaDebugTransformedCode=true'
    '-v:minimal'
)

if ($TargetFramework)
{
    $dotnetArgs += "-f", $TargetFramework
}

if ($ExtraArgs)
{
    $dotnetArgs += $ExtraArgs
}

Write-Host "dotnet $($dotnetArgs -join ' ')" -ForegroundColor DarkGray
& dotnet @dotnetArgs
exit $LASTEXITCODE
