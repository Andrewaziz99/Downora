<#
.SYNOPSIS
  Builds the Downora installer: publishes the app, bundles the tools, optionally signs, and compiles it with Inno Setup.

.PARAMETER Version
  The version to stamp on the installer. Defaults to <Version> in Directory.Build.props.

.PARAMETER ToolsDir
  A folder holding yt-dlp.exe, ffmpeg.exe, ffprobe.exe and deno.exe to bundle. Default: artifacts\tools.

.PARAMETER FetchTools
  Download the tools (checksum-verified) into ToolsDir first. Needs the network.

.PARAMETER CertThumbprint
  SHA-1 thumbprint of a code-signing certificate in the current user's (or the machine's) certificate store. When given, Downora's
  own files and the installer are signed. Without it the installer is built unsigned and Windows SmartScreen will warn on first run.

.PARAMETER TimestampUrl
  RFC 3161 timestamp server used when signing, so signatures stay valid after the certificate expires.

.PARAMETER SkipPublish
  Reuse artifacts\publish from an earlier run.

.PARAMETER TestInstall
  Builds a quick test installer (fast compression, no check for a running Downora, named Downora-Setup-<version>-test.exe) for trying the
  install and uninstall without disturbing a Downora that is open. Never ship it.

.EXAMPLE
  .\installer\build-installer.ps1 -FetchTools

.EXAMPLE
  .\installer\build-installer.ps1 -ToolsDir "$env:LOCALAPPDATA\Downora\tools" -CertThumbprint 0123456789ABCDEF0123456789ABCDEF01234567
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$ToolsDir,
    [switch]$FetchTools,
    [string]$CertThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$SkipPublish,
    [switch]$TestInstall
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts 'publish'
$installerDir = Join-Path $artifacts 'installer'
if (-not $ToolsDir) { $ToolsDir = Join-Path $artifacts 'tools' }

function Step([string]$text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }

# ---- Version ----
if (-not $Version) {
    $props = [xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)
    $Version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version '$Version' must look like 1.2.3." }
Write-Host "Downora $Version"

# ---- Tools ----
if ($FetchTools) {
    Step 'Downloading the tools'
    & (Join-Path $PSScriptRoot 'fetch-tools.ps1') -OutDir $ToolsDir
}
$required = 'yt-dlp.exe', 'ffmpeg.exe', 'ffprobe.exe', 'deno.exe'
$missing = $required | Where-Object { -not (Test-Path (Join-Path $ToolsDir $_)) }
if ($missing) { throw "Missing in ${ToolsDir}: $($missing -join ', '). Use -FetchTools, or point -ToolsDir at a folder that has them." }

# ---- Signing helper ----
$signtool = $null
if ($CertThumbprint) {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $signtool) { throw 'signtool.exe was not found. Install the Windows SDK to sign.' }
}
function Sign([string[]]$files) {
    if (-not $signtool) { return }
    foreach ($f in $files) {
        & $signtool sign /sha1 $CertThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 /d 'Downora' $f
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $f" }
    }
}

# ---- Publish ----
if (-not $SkipPublish) {
    Step 'Publishing Downora (self-contained, win-x64)'
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
    dotnet publish (Join-Path $root 'src\Downora.App\Downora.App.csproj') -c Release -r win-x64 --self-contained true `
        -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -p:PublishReadyToRun=false -p:SatelliteResourceLanguages=en -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
}
if (-not (Test-Path (Join-Path $publishDir 'Downora.exe'))) { throw "No published app in $publishDir." }

Step 'Bundling the tools and notices'
$bundled = Join-Path $publishDir 'tools'
New-Item -ItemType Directory -Force -Path $bundled | Out-Null
foreach ($name in $required) { Copy-Item (Join-Path $ToolsDir $name) (Join-Path $bundled $name) -Force }
Copy-Item (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') $publishDir -Force
if (Test-Path (Join-Path $root 'LICENSE')) { Copy-Item (Join-Path $root 'LICENSE') $publishDir -Force }

if ($signtool) {
    Step 'Signing Downora'
    # Only Downora's own files: the tools are signed (or not) by the people who build them.
    Sign (Get-ChildItem $publishDir -File | Where-Object { $_.Name -like 'Downora*.exe' -or $_.Name -like 'Downora*.dll' } | ForEach-Object FullName)
}

# ---- Compile ----
Step 'Compiling the installer'
$iscc = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 was not found (ISCC.exe). Install it from https://jrsoftware.org/isinfo.php' }

New-Item -ItemType Directory -Force -Path $installerDir | Out-Null
$baseName = if ($TestInstall) { "Downora-Setup-$Version-test" } else { "Downora-Setup-$Version" }
$isccArgs = @("/DAppVersion=$Version", "/DSourceDir=$publishDir", "/DOutputDir=$installerDir", "/DOutputBaseName=$baseName")
if ($TestInstall) { $isccArgs += '/DTestInstall' }
& $iscc @isccArgs (Join-Path $PSScriptRoot 'Downora.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }

$setup = Join-Path $installerDir "$baseName.exe"
if ($signtool) { Step 'Signing the installer'; Sign @($setup) }

# ---- Checksum ----
$hash = (Get-FileHash -Algorithm SHA256 -Path $setup).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $setup)" | Set-Content -Path "$setup.sha256" -Encoding ascii

Step 'Done'
$size = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host "Installer : $setup ($size MB)"
Write-Host "SHA-256   : $hash"
Write-Host ("Signed    : " + $(if ($signtool) { 'yes' } else { 'no (SmartScreen will warn until it is signed)' }))
