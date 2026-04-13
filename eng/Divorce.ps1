# Tests the Metalama "divorce" procedure on the full NopCommerce solution.
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
# Metalama-2026.1 are exercised here.
#
# Steps (mirrors the 7-step procedure in the divorce doc):
#   0. Clean obj/bin so no stale (e.g. BENCHMARK-constant) transformed files
#      from prior builds get picked up by the divorce tool.
#   1. dotnet format BEFORE the divorce so the pre-divorce code style is
#      consistent with what MetalamaFormatOutput will produce.
#   2. Build with MetalamaEmitCompilerTransformedFiles=true and
#      MetalamaFormatOutput=true so the .transformed files land under obj/.
#   3. Invoke the locally-built metalama.exe divorce command.
#   4. dotnet format AFTER the divorce: Metalama does not respect local
#      formatting settings, so a second pass reformats the injected code.
#   5. Rebuild the solution with the stock Microsoft compiler.
#   6. Run Nop.Tests.
#
# Usage (from repo root):
#   pwsh ./eng/Divorce.ps1                                              # full solution
#   pwsh ./eng/Divorce.ps1 -Project src/Libraries/Nop.Core/Nop.Core.csproj  # single project
#   pwsh ./eng/Divorce.ps1 -SkipTest                                    # build post-divorce but skip tests
#   pwsh ./eng/Divorce.ps1 -SkipFormat                                  # skip the two dotnet format passes
#   pwsh ./eng/Divorce.ps1 -Force                                       # --force to metalama divorce (dirty tree)
#
# The pre-divorce dotnet format changes are git-staged after step 1, so:
#   git diff           shows divorce + post-format changes only
#   git diff --cached  shows pre-format changes only
#   git diff HEAD      shows everything

[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$Configuration = 'Debug',

    # Path to the Metalama source repo containing the locally-built Metalama.Tool.
    [string]$MetalamaRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\Metalama')).Path,

    # Project (.csproj) to divorce. If omitted, divorces the whole solution.
    # When set, the build/format/divorce passes are scoped to that single
    # project so the user can inspect the diff in isolation.
    [string]$Project,

    # Skip the two dotnet format passes (pre- and post-divorce).
    [switch]$SkipFormat,

    # Skip the post-divorce test run.
    [switch]$SkipTest,

    # Bypass metalama divorce's clean-working-tree check.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'src/NopCommerce.sln'
$testProject = Join-Path $repoRoot 'src/Tests/Nop.Tests/Nop.Tests.csproj'

# Resolve the build/format/divorce target. When -Project is given we scope
# everything to that single .csproj so the user can review one project's
# divorce diff in isolation.
if ($Project)
{
    $buildTarget = (Resolve-Path $Project).Path
    $projectDir = Split-Path -Parent $buildTarget
    $cleanRoot = $projectDir
    $divorceCwd = $projectDir
}
else
{
    $buildTarget = $solution
    $cleanRoot = $repoRoot
    $divorceCwd = $repoRoot
}

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
Write-Host "  Build target    : $buildTarget"
Write-Host "  Test project    : $testProject"
Write-Host "  Configuration   : $Configuration"
Write-Host "  Metalama CLI    : $metalamaExe"
Write-Host ""

# ---- 0. Clean obj/bin throughout the solution ---------------------------
# The divorce tool copies whatever .transformed files it finds under each
# project's obj/ directory back over the source files. If a prior build
# populated obj/ with e.g. BENCHMARK-constant output, it must be cleared
# before the formatted transformed files are produced; otherwise the
# divorced tree will contain stale, non-parseable code.
Write-Host "[0/6] Cleaning obj/ and bin/ under $cleanRoot ..." -ForegroundColor Cyan
Get-ChildItem -Path $cleanRoot -Recurse -Force -Directory `
    -ErrorAction SilentlyContinue `
    | Where-Object { $_.Name -in 'obj', 'bin' -and $_.FullName -notmatch '\\\.git\\' } `
    | ForEach-Object {
        Write-Host "  Removing $($_.FullName)" -ForegroundColor DarkGray
        Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }

# ---- 1. dotnet format before the divorce --------------------------------
if (-not $SkipFormat)
{
    Write-Host ""
    Write-Host "[1/6] dotnet format (pre-divorce) ..." -ForegroundColor Cyan
    & dotnet format $buildTarget --verbosity minimal
    if ($LASTEXITCODE -ne 0)
    {
        throw "Pre-divorce dotnet format failed with exit code $LASTEXITCODE."
    }

    # Stage the pre-divorce format changes so the user can review the
    # divorce/post-format diff in isolation (git diff = post-format only;
    # git diff --cached = pre-format only; git diff HEAD = both).
    Write-Host "  Staging pre-divorce format changes ..." -ForegroundColor DarkGray
    Push-Location $repoRoot
    try
    {
        & git add -A
        if ($LASTEXITCODE -ne 0)
        {
            throw "git add -A failed with exit code $LASTEXITCODE."
        }
    }
    finally
    {
        Pop-Location
    }
}
else
{
    Write-Host "[1/6] Skipping pre-divorce dotnet format (--SkipFormat)." -ForegroundColor DarkYellow
}

# ---- 2. Build with transformed-files + format-output --------------------
Write-Host ""
Write-Host "[2/6] Building solution with MetalamaEmitCompilerTransformedFiles=true MetalamaFormatOutput=true ..." -ForegroundColor Cyan

# Pass as -p: properties (explicit and visible in the build log) in addition
# to env vars, so per-project property evaluation sees the flags even if
# env-var propagation is unexpectedly blocked.
$env:MetalamaEmitCompilerTransformedFiles = 'true'
$env:MetalamaFormatOutput = 'true'

try
{
    & dotnet build $buildTarget `
        -c $Configuration `
        -p:MetalamaEmitCompilerTransformedFiles=true `
        -p:MetalamaFormatOutput=true `
        -v:minimal
    if ($LASTEXITCODE -ne 0)
    {
        throw "Transformed-files build failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Remove-Item Env:MetalamaEmitCompilerTransformedFiles -ErrorAction SilentlyContinue
    Remove-Item Env:MetalamaFormatOutput -ErrorAction SilentlyContinue
}

# ---- 3. Run metalama divorce --------------------------------------------
Write-Host ""
Write-Host "[3/6] Running 'metalama divorce' from $metalamaExe ..." -ForegroundColor Cyan

$divorceArgs = @('divorce')
# Pre-format staging dirties the working tree; --force is required to let
# metalama divorce proceed past its clean-tree check.
if ($Force -or -not $SkipFormat) { $divorceArgs += '--force' }

Push-Location $divorceCwd
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

# ---- 4. dotnet format after the divorce ---------------------------------
# Metalama's format pass does not respect the repo's .editorconfig, so
# normalize the injected code with the project's preferred tool.
if (-not $SkipFormat)
{
    # Files copied out of obj/ by `metalama divorce` inherit the read-only
    # attribute that Metalama set on the transformed-files cache. dotnet
    # format then fails with UnauthorizedAccessException. Clear the bit on
    # every .cs file under the divorced scope before formatting.
    Write-Host ""
    Write-Host "  Clearing read-only attribute on .cs files under $cleanRoot ..." -ForegroundColor DarkGray
    Get-ChildItem -Path $cleanRoot -Recurse -Filter *.cs -File `
        | Where-Object { $_.IsReadOnly } `
        | ForEach-Object { $_.IsReadOnly = $false }

    Write-Host "[4/6] dotnet format (post-divorce) ..." -ForegroundColor Cyan
    & dotnet format $buildTarget --verbosity minimal
    if ($LASTEXITCODE -ne 0)
    {
        throw "Post-divorce dotnet format failed with exit code $LASTEXITCODE."
    }
}
else
{
    Write-Host "[4/6] Skipping post-divorce dotnet format (--SkipFormat)." -ForegroundColor DarkYellow
}

# ---- 5. Rebuild with the stock Microsoft compiler -----------------------
Write-Host ""
Write-Host "[5/6] Rebuilding solution without Metalama ..." -ForegroundColor Cyan

& dotnet build $buildTarget -c $Configuration /t:Rebuild -v:minimal
if ($LASTEXITCODE -ne 0)
{
    throw "Post-divorce build failed with exit code $LASTEXITCODE. The divorced code did not compile with the stock Microsoft compiler."
}

# ---- 6. Run the unit tests ----------------------------------------------
if ($Project)
{
    Write-Host ""
    Write-Host "[6/6] Skipping tests (single-project mode)." -ForegroundColor DarkYellow
}
elseif (-not $SkipTest)
{
    Write-Host ""
    Write-Host "[6/6] Running unit tests ..." -ForegroundColor Cyan

    & dotnet test $testProject -c $Configuration --no-build -v:minimal
    if ($LASTEXITCODE -ne 0)
    {
        throw "Post-divorce tests failed with exit code $LASTEXITCODE."
    }
}
else
{
    Write-Host ""
    Write-Host "[6/6] Skipping tests (--SkipTest)." -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "Divorce test succeeded." -ForegroundColor Green
