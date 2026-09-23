#requires -Version 7
<#
.SYNOPSIS
    Fetches the static Instrument Sans TTFs into MorningCard/Assets/Fonts.

.DESCRIPTION
    The Skia text stack renders a variable TTF's default named instance only - FontWeight does not
    select the wght axis - so every weight has to arrive as its own static file. Google's css2
    endpoint decides what to serve from the User-Agent: a modern UA gets WOFF2, an IE6 UA gets EOT
    (which Skia ignores silently), and the Android 2.2 stock-browser UA gets true static TTFs. This
    asks with that UA, then proves each download is a TTF by its magic bytes, and rejects any file
    carrying an fvar table.

    It reports each file's internal family name, because the XAML #fragment must match it exactly.
    Adapted from UnoBusiness/scripts/05-fetch-fonts.ps1.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\MorningCard\Assets\Fonts')
)

$ErrorActionPreference = 'Stop'

$LegacyUserAgent = 'Mozilla/5.0 (Linux; U; Android 2.2; en-us; Nexus One Build/FRF91) AppleWebKit/533.1 (KHTML, like Gecko) Version/4.0 Mobile Safari/533.1'

$Requests = @(
    @{ File = 'InstrumentSans_Regular.ttf';  Css = 'family=Instrument+Sans:wght@400' }
    @{ File = 'InstrumentSans_Medium.ttf';   Css = 'family=Instrument+Sans:wght@500' }
    @{ File = 'InstrumentSans_SemiBold.ttf'; Css = 'family=Instrument+Sans:wght@600' }
)

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

function Get-TtfNames {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $u16 = { param($o) ([int]$bytes[$o] -shl 8) -bor [int]$bytes[$o + 1] }
    $u32 = { param($o) ([uint32]$bytes[$o] -shl 24) -bor ([uint32]$bytes[$o + 1] -shl 16) -bor ([uint32]$bytes[$o + 2] -shl 8) -bor [uint32]$bytes[$o + 3] }

    $tableCount = & $u16 4
    $nameOffset = 0
    $hasFvar = $false
    for ($i = 0; $i -lt $tableCount; $i++) {
        $rec = 12 + ($i * 16)
        $tag = [System.Text.Encoding]::ASCII.GetString($bytes, $rec, 4)
        if ($tag -eq 'name') { $nameOffset = [int](& $u32 ($rec + 8)) }
        if ($tag -eq 'fvar') { $hasFvar = $true }
    }
    if ($nameOffset -eq 0) { return [pscustomobject]@{ Family = '(no name table)'; Subfamily = ''; IsVariable = $hasFvar } }

    $count = & $u16 ($nameOffset + 2)
    $stringOffset = $nameOffset + (& $u16 ($nameOffset + 4))
    $family = ''
    $subfamily = ''

    for ($i = 0; $i -lt $count; $i++) {
        $rec = $nameOffset + 6 + ($i * 12)
        $platformId = & $u16 $rec
        $nameId = & $u16 ($rec + 6)
        if ($nameId -ne 1 -and $nameId -ne 2) { continue }

        $length = & $u16 ($rec + 8)
        $offset = & $u16 ($rec + 10)
        $encoding = if ($platformId -eq 3 -or $platformId -eq 0) { [System.Text.Encoding]::BigEndianUnicode } else { [System.Text.Encoding]::ASCII }
        $value = $encoding.GetString($bytes, $stringOffset + $offset, $length)

        if ($nameId -eq 1 -and -not $family) { $family = $value }
        if ($nameId -eq 2 -and -not $subfamily) { $subfamily = $value }
    }

    return [pscustomobject]@{ Family = $family; Subfamily = $subfamily; IsVariable = $hasFvar }
}

$report = @()

foreach ($request in $Requests) {
    $cssUri = "https://fonts.googleapis.com/css2?$($request.Css)&display=swap"
    Write-Host "-> $($request.File)" -ForegroundColor Cyan

    $css = (Invoke-WebRequest -Uri $cssUri -Headers @{ 'User-Agent' = $LegacyUserAgent } -UseBasicParsing).Content

    $match = [regex]::Match($css, "url\((https://[^)]+?\.ttf)\)")
    if (-not $match.Success) {
        $served = [regex]::Match($css, "url\((https://[^)]+)\)")
        $what = if ($served.Success) { $served.Groups[1].Value } else { '(no url in css)' }
        throw "css2 did not serve a TTF for '$($request.Css)'. Served: $what"
    }

    $destination = Join-Path $OutputDirectory $request.File
    Invoke-WebRequest -Uri $match.Groups[1].Value -Headers @{ 'User-Agent' = $LegacyUserAgent } -OutFile $destination -UseBasicParsing

    $magic = [System.IO.File]::ReadAllBytes($destination)[0..3]
    $magicHex = ($magic | ForEach-Object { $_.ToString('X2') }) -join ' '
    if ($magicHex -ne '00 01 00 00') {
        throw "$($request.File) is not a static TTF (magic $magicHex). Expected 00 01 00 00."
    }

    $names = Get-TtfNames -Path $destination
    if ($names.IsVariable) {
        throw "$($request.File) is a variable font (fvar present). Skia renders its default instance only."
    }

    $report += [pscustomobject]@{
        File      = $request.File
        Family    = $names.Family
        Subfamily = $names.Subfamily
        KB        = [math]::Round((Get-Item $destination).Length / 1KB, 1)
    }
}

Write-Host ''
Write-Host 'Internal family names - the XAML #fragment must match Family exactly:' -ForegroundColor Yellow
$report | Format-Table -AutoSize

