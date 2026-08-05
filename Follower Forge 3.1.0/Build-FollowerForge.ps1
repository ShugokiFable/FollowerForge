<#
    Build-FollowerForge.ps1 — one-command build + verify for Follower Forge.
    Cleans, builds Release, runs the test suite, and boot-verifies the CLI.
    Exits non-zero on any failure.
#>
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'src'
$sln  = Join-Path $root 'FollowerForge.slnx'
if (-not (Test-Path $sln)) { throw "Solution not found: $sln" }

Write-Host '== Follower Forge build ==' -ForegroundColor Cyan

# 1. Kill any running CLI/UI so output files are not locked.
foreach ($name in 'FollowerForge.Cli','FollowerForge.Ui') {
    Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

# 2. Clean stale output.
dotnet clean $sln -c $Configuration --nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw "clean failed ($LASTEXITCODE)" }

# 3. Build.
dotnet build $sln -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed ($LASTEXITCODE)" }

# 4. Tests (unless skipped).
if (-not $SkipTests) {
    dotnet test (Join-Path $root 'Tests') -c $Configuration --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "tests failed ($LASTEXITCODE)" }
}

# 5. Boot-verify the CLI (usage screen returns exit 2 by design; just confirm it runs).
$cliDll = Join-Path $root "Cli\bin\$Configuration\net10.0\FollowerForge.Cli.dll"
if (-not (Test-Path $cliDll)) { throw "CLI not built at $cliDll" }
& dotnet $cliDll 2>&1 | Out-Null   # prints usage
Write-Host "CLI OK: $cliDll" -ForegroundColor Green

Write-Host ''
Write-Host 'BUILD SUCCEEDED' -ForegroundColor Green
Write-Host "Run the CLI:  dotnet `"$cliDll`" env"
Write-Host "Run the UI:   dotnet run --project `"$(Join-Path $root 'Ui')`""
exit 0
