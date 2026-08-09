<#
    Publish-FollowerForge.ps1 - produces a shareable Windows build.

    Self-contained: the person you send it to does NOT need .NET installed. Native
    dependencies (NiflySharp's niflycpp, SQLite) are extracted at run time, which is why
    single-file publishing needs IncludeNativeLibrariesForSelfExtract.
#>
[CmdletBinding()]
param(
    [string]$Version = '3.2.7',
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }
$src = Join-Path $root 'src'
$sln = Join-Path $src 'FollowerForge.slnx'
if (-not (Test-Path $sln)) { throw "Solution not found: $sln" }

$dist = Join-Path $root 'dist'
$stage = Join-Path $dist "FollowerForge $Version"
$zip = Join-Path $dist "FollowerForge-$Version-win-x64.zip"

Write-Host "== FollowerForge $Version - publish ==" -ForegroundColor Cyan

foreach ($name in 'FollowerForge', 'FollowerForge.Ui', 'FollowerForge.Cli') {
    Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

# Regenerate the app icon so the shipped exe always matches the source art.
& (Join-Path $root 'tools\New-AppIcon.ps1') -OutPath (Join-Path $src 'Ui\Assets\appicon.ico')

if (-not $SkipTests) {
    Write-Host '-- tests' -ForegroundColor DarkGray
    dotnet test (Join-Path $src 'Tests') -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "tests failed ($LASTEXITCODE)" }
}

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

$common = @(
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--nologo',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=none',
    "-p:Version=$Version",
    "-p:FileVersion=$Version.0",
    "-p:InformationalVersion=$Version",
    "-p:AssemblyVersion=$Version.0"
)

Write-Host '-- app' -ForegroundColor DarkGray
dotnet publish (Join-Path $src 'Ui') @common -o $stage
if ($LASTEXITCODE -ne 0) { throw "app publish failed ($LASTEXITCODE)" }

Write-Host '-- cli' -ForegroundColor DarkGray
dotnet publish (Join-Path $src 'Cli') @common -o (Join-Path $stage 'cli')
if ($LASTEXITCODE -ne 0) { throw "cli publish failed ($LASTEXITCODE)" }

# Skia/HarfBuzz ship ~100 MB of native debug symbols that nobody downloading a mod tool needs.
Get-ChildItem $stage -Recurse -Filter *.pdb | Remove-Item -Force

# Docs the user actually needs next to the exe.
foreach ($doc in 'README.md', 'NEXUS-CHANGELOG-3.2.7.txt') {
    $p = Join-Path $root $doc
    if (Test-Path $p) { Copy-Item $p $stage }
}
$changelog = Join-Path $root 'CHANGELOG.txt'
if (-not (Test-Path $changelog)) {
    $changelog = Join-Path (Split-Path $root -Parent) 'CHANGELOG.txt'
}
if (Test-Path $changelog) { Copy-Item $changelog $stage }

$exe = Join-Path $stage 'FollowerForge.exe'
if (-not (Test-Path $exe)) { throw "expected exe missing: $exe" }

# Boot check: start it, confirm the window survives a few seconds, then close it.
Write-Host '-- boot check' -ForegroundColor DarkGray
$p = Start-Process $exe -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 12
$alive = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
if (-not $alive) { throw 'published exe exited immediately' }
$alive | Stop-Process -Force
Write-Host '   window stayed up' -ForegroundColor Green

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)

Write-Host ''
Write-Host 'PUBLISH SUCCEEDED' -ForegroundColor Green
Write-Host "  folder : $stage"
Write-Host "  zip    : $zip  ($mb MB)"
Write-Host "  exe    : FollowerForge.exe  (self-contained - no .NET needed)"
exit 0
