#requires -Version 7
<#
.SYNOPSIS
    Captures the card in each of its states, for parity review against the prototype.

.DESCRIPTION
    Launches the desktop build once per state with the DEBUG environment hooks set, waits for the
    window, and saves a PNG. PrintWindow can hand back a blank bitmap for a Skia GL window, so the
    capture script it calls samples the result and falls back to a screen copy.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\captures'),
    [string]$Exe = (Join-Path $PSScriptRoot '..\MorningCard\bin\Debug\net10.0-desktop\MorningCard.exe'),
    [string]$CaptureScript = "$env:USERPROFILE\SimpleThemeDemo\tools\Capture-Window.ps1"
)

$ErrorActionPreference = 'Stop'

$states = @(
    @{ Name = 'live';       Env = @{} }
    @{ Name = 'empty';      Env = @{ MC_STATE = 'empty' } }
    @{ Name = 'offline';    Env = @{ MC_STATE = 'offline' } }
    @{ Name = 'error';      Env = @{ MC_STATE = 'error' } }
    @{ Name = 'leave-now';  Env = @{ MC_CLOCK = '452' } }
    @{ Name = 'late';       Env = @{ MC_CLOCK = '460' } }
    @{ Name = 'heavy';      Env = @{ MC_DELAY = '9' } }
    @{ Name = 'warn';       Env = @{ MC_RANGE = '60'; MC_TIRE = '29' } }
    @{ Name = 'bad';        Env = @{ MC_RANGE = '20'; MC_TIRE = '25' } }
    @{ Name = 'dark';       Env = @{ MC_THEME = 'dark' } }
    @{ Name = 'dark-late';  Env = @{ MC_THEME = 'dark'; MC_CLOCK = '460' } }
)

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

$hooks = 'MC_STATE', 'MC_CLOCK', 'MC_DELAY', 'MC_RANGE', 'MC_TIRE', 'MC_THEME', 'MC_PROBE_RETRY', 'MC_TRACE'

foreach ($state in $states) {
    Get-Process MorningCard -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
    Start-Sleep -Milliseconds 400

    foreach ($hook in $hooks) { Remove-Item "Env:$hook" -ErrorAction SilentlyContinue }
    $env:APP_NO_HOTDESIGN = '1'
    foreach ($key in $state.Env.Keys) { Set-Item "Env:$key" $state.Env[$key] }

    Write-Host "-> $($state.Name)" -ForegroundColor Cyan
    $process = Start-Process $Exe -PassThru

    $deadline = (Get-Date).AddSeconds(30)
    do {
        $process.Refresh()
        if ($process.HasExited -or $process.MainWindowHandle -ne 0) { break }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $deadline)

    if ($process.HasExited -or $process.MainWindowHandle -eq 0) {
        Write-Warning "$($state.Name): the window never appeared"
        continue
    }

    Start-Sleep -Seconds 3

    # Through Capture-Tall, not Capture-Window directly: the app opens at 1024x640 (the wide
    # two-column stage) and the card is ~888 epx tall, so a capture at the default size is both
    # clipped and carries stale pixels from the layout it just collapsed out of. Capture-Tall
    # sizes twice, which settles both.
    & (Join-Path $PSScriptRoot 'Capture-Tall.ps1') `
        -ProcessId $process.Id `
        -Out (Join-Path $OutputDirectory "$($state.Name).png") `
        -CaptureScript $CaptureScript | Out-Null
}

Get-Process MorningCard -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
Write-Host "`nSaved to $OutputDirectory" -ForegroundColor Yellow
Get-ChildItem $OutputDirectory -Filter *.png | Select-Object Name, Length
