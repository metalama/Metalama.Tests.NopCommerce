# Tests the Metalama "divorce" procedure on NopCommerce.
#
# The divorce procedure permanently removes Metalama from the code base by
# injecting the compiler-transformed source files back into the repository
# and disabling Metalama in every .csproj. After divorce the solution must
# build and test under the stock Microsoft compiler alone.
#
# Reference: https://doc.metalama.net/conceptual/divorcing
#
# We intentionally do NOT use the globally-installed `metalama` dotnet tool.
# Instead we invoke the freshly-built Metalama.Tool assembly from the
# Metalama framework repo, so whatever changes the user is iterating on in
# Metalama-2026.1 are under test here.
#
# Usage (from repo root):
#   pwsh ./eng/Divorce.ps1               # run the whole procedure
#   pwsh ./eng/Divorce.ps1 -SkipBuild    # skip the initial transformed-files build
#   pwsh ./eng/Divorce.ps1 -SkipTest     # build post-divorce but skip tests

[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$Configuration = 'Debug',

    # Path to the Metalama source repo containing the locally-built Metalama.Tool.
    [string]$MetalamaRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\Metalama')).Path,

    # Skip the initial build that writes the transformed files to obj/.
    [switch]$SkipBuild,

    # Skip the post-divorce test run.
    [switch]$SkipTest,

    # Bypass the clean-working-tree check. Passed through to `metalama divorce --force`.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'src/NopCommerce.sln'
$testProject = Join-Path $repoRoot 'src/Tests/Nop.Tests/Nop.Tests.csproj'

# ---- Locate the locally-built metalama CLI ------------------------------
$metalamaExe = [System.IO.Path]::GetFullPath(
    (Join-Path $MetalamaRepo "Metalama.Framework/src/Metalama.Tool/bin/$Configuration/net8.0/metalama.exe"))

if (-not (Test-Path $metalamaExe))
{
    throw "Could not locate the locally-built Metalama CLI at '$metalamaExe'. " +
          "Build the Metalama repo first (Build.ps1 build in $MetalamaRepo)."
}

Write-Host ""
Write-Host "Divorce test" -ForegroundColor Cyan
Write-Host "  Repo root       : $repoRoot"
Write-Host "  Solution        : $solution"
Write-Host "  Test project    : $testProject"
Write-Host "  Configuration   : $Configuration"
Write-Host "  Metalama CLI    : $metalamaExe"
Write-Host ""

# ---- 1. Build the solution with the transformed-files flags ------------
if (-not $SkipBuild)
{
    Write-Host "[1/4] Building solution with MetalamaEmitCompilerTransformedFiles=true MetalamaFormatOutput=true ..." -ForegroundColor Cyan

    # The env-var form is what the divorce doc recommends and it applies
    # uniformly to every project in the solution. Format-output is what
    # makes the divorced sources readable.
    $env:MetalamaEmitCompilerTransformedFiles = 'true'
    $env:MetalamaFormatOutput = 'true'

    try
    {
        & dotnet build $solution -c $Configuration /t:Rebuild -v:minimal
        if ($LASTEXITCODE -ne 0)
        {
            throw "Initial transformed-files build failed with exit code $LASTEXITCODE."
        }
    }
    finally
    {
        Remove-Item Env:MetalamaEmitCompilerTransformedFiles -ErrorAction SilentlyContinue
        Remove-Item Env:MetalamaFormatOutput -ErrorAction SilentlyContinue
    }
}
else
{
    Write-Host "[1/4] Skipping initial transformed-files build (--SkipBuild)." -ForegroundColor DarkYellow
}

# ---- 2. Run the divorce command -----------------------------------------
Write-Host ""
Write-Host "[2/4] Running 'metalama divorce' from $metalamaExe ..." -ForegroundColor Cyan

$divorceArgs = @('divorce')
if ($Force) { $divorceArgs += '--force' }

Push-Location $repoRoot
try
{
    & $metalamaExe @divorceArgs
    if ($LASTEXITCODE -ne 0)
    {
        throw "'metalama divorce' failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Pop-Location
}

# ---- 3. Rebuild the solution with the stock Microsoft compiler ---------
Write-Host ""
Write-Host "[3/4] Rebuilding solution without Metalama ..." -ForegroundColor Cyan

# Divorce added MetalamaEnabled=false to every csproj. The next build uses
# only the standard Microsoft compiler with the code committed back in.
& dotnet build $solution -c $Configuration /t:Rebuild -v:minimal
if ($LASTEXITCODE -ne 0)
{
    throw "Post-divorce build failed with exit code $LASTEXITCODE. The divorced code did not compile with the stock Microsoft compiler."
}

# ---- 4. Run the unit tests ---------------------------------------------
if (-not $SkipTest)
{
    Write-Host ""
    Write-Host "[4/4] Running unit tests ..." -ForegroundColor Cyan

    & dotnet test $testProject -c $Configuration --no-build -v:minimal
    if ($LASTEXITCODE -ne 0)
    {
        throw "Post-divorce tests failed with exit code $LASTEXITCODE."
    }
}
else
{
    Write-Host ""
    Write-Host "[4/4] Skipping tests (--SkipTest)." -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "Divorce test succeeded." -ForegroundColor Green
