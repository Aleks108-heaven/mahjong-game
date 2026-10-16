<#
.SYNOPSIS
    Writes the text of a GitHub release: the CHANGELOG.md section for the version, then how to pick, run and
    verify the files. Used by the release workflow; run it by hand to preview the notes.

.EXAMPLE
    .\tools\release-notes.ps1 -Version 0.1.0 -Repository Aleks108-heaven/mahjong-game
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Repository,
    [string]$OutFile = 'release-notes.md'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 1.2.3 (got '$Version')." }

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$changelog = Get-Content (Join-Path $root 'CHANGELOG.md') -Raw
$pattern = '(?ms)^## \[' + [regex]::Escape($Version) + '\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)'
$match = [regex]::Match($changelog, $pattern)
if (-not $match.Success -or -not $match.Groups[1].Value.Trim()) { throw "CHANGELOG.md has no entries for $Version." }
$section = $match.Groups[1].Value.Trim()

$fence = '```'
$notes = @"
$section

## Which file do I want?

| File | Use it for |
| --- | --- |
| ``MahjongTable-$Version-Setup.exe`` | Installing the game. No administrator rights needed; the installer speaks English and Ukrainian. |
| ``MahjongTable-$Version-win-x64-portable.zip`` | Running it without installing: unzip and start ``MahjongTable.exe``. |
| ``SHA256SUMS.txt`` | Checking that a download is intact. |

Windows 10 or 11, 64-bit. Nothing else needs to be installed.

## Windows may warn you

These files are **not code-signed**, so Windows SmartScreen can show "Windows protected your PC" the first time you
run them. Check the download as below, then choose **More info**, then **Run anyway**.

## Check your download

${fence}powershell
# The hash printed here should match the line for your file in SHA256SUMS.txt
Get-FileHash .\MahjongTable-$Version-Setup.exe -Algorithm SHA256

# With the GitHub CLI: confirm GitHub Actions built this file from this repository's source
gh attestation verify .\MahjongTable-$Version-Setup.exe --repo $Repository
${fence}
"@

[System.IO.File]::WriteAllText($OutFile, $notes, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Wrote $OutFile"
