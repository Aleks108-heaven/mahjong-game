<#
.SYNOPSIS
    Builds the Windows release files: a self-contained single-file exe, a portable zip, the Setup installer and
    SHA-256 checksums. The release workflow runs this same script, so what you test here is what gets released.

.PARAMETER OutDir
    Where the finished files go (default: artifacts next to this repo's root).

.PARAMETER IsccPath
    Path to Inno Setup's ISCC.exe. Found automatically if it is installed in the usual places.

.EXAMPLE
    .\tools\package.ps1
#>
[CmdletBinding()]
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\artifacts'),
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$props = [xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)
$versionNode = $props.SelectSingleNode('/Project/PropertyGroup/Version')
$version = if ($versionNode) { $versionNode.InnerText } else { '' }
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Directory.Build.props has no valid Version (found '$version')." }

if (-not $IsccPath) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $onPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue | ForEach-Object { $_.Source }
    $IsccPath = @($onPath) + $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $IsccPath) { throw 'Inno Setup 6 (ISCC.exe) was not found. Install it or pass -IsccPath.' }

New-Item -ItemType Directory -Force $OutDir | Out-Null
$out = (Resolve-Path $OutDir).Path
$publish = Join-Path $out 'publish'
$staging = Join-Path $out 'staging'
Remove-Item $publish, $staging -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $out -File -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "==> Publishing Mahjong Table $version (self-contained, single file, win-x64)"
dotnet publish (Join-Path $root 'Wpf\MahjongTable.csproj') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# Only the exe ships; the publish folder also holds a stray runtimeconfig from the engine project.
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item (Join-Path $publish 'MahjongTable.exe') $staging

$portable = "MahjongTable-$version-win-x64-portable"
$portableDir = Join-Path $out $portable
New-Item -ItemType Directory -Force $portableDir | Out-Null
Copy-Item (Join-Path $staging 'MahjongTable.exe') $portableDir
@"
Mahjong Table $version (portable)

Run MahjongTable.exe. Nothing is installed; your language, level and seat are saved in
%LOCALAPPDATA%\MahjongTable\settings.json (delete that folder to forget them).

Windows may warn "Windows protected your PC" because this build is not code-signed: choose
More info > Run anyway. To check the download, compare its SHA-256 with SHA256SUMS.txt from the release page.

https://github.com/Aleks108-heaven/mahjong-game
"@ | Set-Content (Join-Path $portableDir 'README.txt') -Encoding UTF8
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath (Join-Path $out "$portable.zip") -CompressionLevel Optimal
Remove-Item $portableDir -Recurse -Force

Write-Host '==> Building the installer'
& $IsccPath "/DAppVersion=$version" "/DSourceExe=$(Join-Path $staging 'MahjongTable.exe')" "/DOutputDir=$out" `
    (Join-Path $root 'installer\MahjongTable.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }

Write-Host '==> Checksums'
$files = Get-ChildItem $out -File | Where-Object { $_.Extension -in '.exe', '.zip' }
$lines = foreach ($file in $files) { '{0} *{1}' -f (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower(), $file.Name }
$lines | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ASCII
$lines

Remove-Item $publish, $staging -Recurse -Force
Write-Host "Done. Files in $out"
