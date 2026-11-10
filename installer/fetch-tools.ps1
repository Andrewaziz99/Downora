<#
.SYNOPSIS
  Downloads yt-dlp, FFmpeg + FFprobe and Deno from their official releases into a folder, checking each file's published SHA-256.

.DESCRIPTION
  Mirrors what Downora itself does when you press "Install" (see src/Downora.Infrastructure/Tools/ToolCatalog.cs): same
  addresses, same checksum files, same files taken out of the zips. The result is the folder the installer bundles.

.EXAMPLE
  .\installer\fetch-tools.ps1 -OutDir artifacts\tools
#>
[CmdletBinding()]
param(
    [string]$OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\tools')
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$sources = @(
    @{
        Name     = 'yt-dlp'
        Url      = 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe'
        SumsUrl  = 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS'
        FileName = 'yt-dlp.exe'
        Extract  = $null
    },
    @{
        Name     = 'FFmpeg'
        Url      = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl.zip'
        SumsUrl  = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/checksums.sha256'
        FileName = 'ffmpeg-master-latest-win64-lgpl.zip'
        Extract  = @('ffmpeg.exe', 'ffprobe.exe')
    },
    @{
        Name     = 'Deno'
        Url      = 'https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip'
        SumsUrl  = 'https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip.sha256sum'
        FileName = 'deno-x86_64-pc-windows-msvc.zip'
        Extract  = @('deno.exe')
    }
)

function Get-ExpectedHash([string]$sumsText, [string]$fileName) {
    $lines = $sumsText -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    foreach ($line in $lines) {
        # "hash  name" or "hash *name" (yt-dlp, FFmpeg); Get-FileHash style output also ends with the name.
        if ($line -match '^(?<hash>[0-9a-fA-F]{64})\s+\*?(?<name>.+)$' -and $Matches['name'].Trim() -ieq $fileName) {
            return $Matches['hash'].ToLowerInvariant()
        }
    }

    # A file that holds one bare hash (Deno's .sha256sum may be just that).
    $bare = $lines | Where-Object { $_ -match '^[0-9a-fA-F]{64}$' }
    if (@($bare).Count -eq 1) { return $bare.ToLowerInvariant() }

    throw "No checksum for $fileName was found in the published checksum file."
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$work = Join-Path ([IO.Path]::GetTempPath()) ("downora-tools-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null

try {
    foreach ($s in $sources) {
        Write-Host "== $($s.Name)"
        $sums = (Invoke-WebRequest -Uri $s.SumsUrl -UseBasicParsing).Content
        if ($sums -is [byte[]]) { $sums = [Text.Encoding]::UTF8.GetString($sums) }
        $expected = Get-ExpectedHash $sums $s.FileName

        $download = Join-Path $work $s.FileName
        Invoke-WebRequest -Uri $s.Url -OutFile $download -UseBasicParsing
        $actual = (Get-FileHash -Algorithm SHA256 -Path $download).Hash.ToLowerInvariant()
        if ($actual -ne $expected) {
            throw "$($s.FileName) does not match its published checksum (expected $expected, got $actual). Nothing was installed."
        }
        Write-Host "   checksum ok ($actual)"

        if ($null -eq $s.Extract) {
            Copy-Item $download (Join-Path $OutDir $s.FileName) -Force
        }
        else {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $zip = [IO.Compression.ZipFile]::OpenRead($download)
            try {
                foreach ($name in $s.Extract) {
                    # Matched by file name only and written to a fixed path, as Downora does, so a zip can't write elsewhere.
                    $entry = $zip.Entries | Where-Object { $_.Name -ieq $name } | Select-Object -First 1
                    if (-not $entry) { throw "$name was not found in $($s.FileName)." }
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $OutDir $name), $true)
                }
            }
            finally { $zip.Dispose() }
        }
    }
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}

Get-ChildItem $OutDir -Filter *.exe | ForEach-Object { '{0,-14} {1,10:N0} bytes' -f $_.Name, $_.Length }
