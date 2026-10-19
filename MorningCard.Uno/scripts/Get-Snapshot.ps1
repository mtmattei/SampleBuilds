<#
.SYNOPSIS
    Launches MorningCard once in a pinned state, reads its JSON snapshot, and checks it.

.DESCRIPTION
    One launch per check. The app writes MC_SNAPSHOT once a second and closes itself after
    -Seconds (MC_EXIT_AFTER), so no window is left open and nothing sweeps the screen.
    Requires a Debug build of the desktop head: the hooks are compiled out of Release.

    Exit code 0 when every -Expect text is on screen and every -Hidden name is not visible,
    1 when a check fails, 2 when the app produced no snapshot.

.EXAMPLE
    ./scripts/Get-Snapshot.ps1 -Clock 460 -Expect 'Running','7 min late'

.EXAMPLE
    pwsh -File scripts/Get-Snapshot.ps1 -Clock 460 -Expect 'Running|7 min late'
    (From -File, pass several values separated by '|'.)

.EXAMPLE
    ./scripts/Get-Snapshot.ps1 -State error -Theme dark -Expect 'Retry' -Hidden OpenInMaps
#>
[CmdletBinding()]
param(
    [int]$Clock,
    [int]$Delay,
    [ValidateSet('live', 'offline', 'empty', 'error', 'loading')][string]$State,
    [ValidateSet('light', 'dark')][string]$Theme,
    [int]$Seconds = 10,
    [string[]]$Expect = @(),
    [string[]]$Hidden = @(),
    [string]$Out = (Join-Path ([IO.Path]::GetTempPath()) 'morningcard-snapshot.json'),
    [switch]$Build
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# `pwsh -File` hands an array argument over as one string, so also accept 'a|b'.
$Expect = @($Expect | ForEach-Object { $_ -split '\|' } | Where-Object { $_ })
$Hidden = @($Hidden | ForEach-Object { $_ -split '\|' } | Where-Object { $_ })
$bin = Join-Path $root 'MorningCard/bin/Debug/net10.0-desktop'

if ($Build) {
    dotnet build (Join-Path $root 'MorningCard/MorningCard.csproj') -f net10.0-desktop -c Debug
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$exe = Join-Path $bin 'MorningCard.exe'
$dll = Join-Path $bin 'MorningCard.dll'
if (-not (Test-Path $dll)) { throw "No Debug desktop build at $bin. Run with -Build." }

Remove-Item $Out -ErrorAction SilentlyContinue

# Environment for the child only; restored afterwards so the shell is left as it was.
$vars = @{
    APP_NO_HOTDESIGN = '1'
    MC_SNAPSHOT      = $Out
    MC_EXIT_AFTER    = "$Seconds"
    MC_CLOCK         = if ($PSBoundParameters.ContainsKey('Clock')) { "$Clock" } else { $null }
    MC_DELAY         = if ($PSBoundParameters.ContainsKey('Delay')) { "$Delay" } else { $null }
    MC_STATE         = $State
    MC_THEME         = $Theme
}
$saved = @{}
foreach ($k in $vars.Keys) {
    $saved[$k] = [Environment]::GetEnvironmentVariable($k)
    [Environment]::SetEnvironmentVariable($k, $vars[$k])
}

try {
    $start = if (Test-Path $exe) { @{ FilePath = $exe } } else { @{ FilePath = 'dotnet'; ArgumentList = @($dll) } }
    # The app's own log goes beside the snapshot rather than into this script's output.
    $proc = Start-Process @start -WorkingDirectory $bin -PassThru `
        -RedirectStandardOutput "$Out.stdout.log" -RedirectStandardError "$Out.stderr.log"
    if (-not $proc.WaitForExit(($Seconds + 60) * 1000)) {
        $proc.Kill()
        Write-Warning 'The app did not exit on its own; killed it.'
    }
}
finally {
    foreach ($k in $saved.Keys) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) }
}

if (-not (Test-Path $Out)) {
    Write-Error "No snapshot at $Out. Is this a Debug build, and did the app start?"
    exit 2
}

$snap = Get-Content $Out -Raw | ConvertFrom-Json
Write-Host ("hub:     {0}" -f ($snap.hub | ConvertTo-Json -Compress))
Write-Host ("derived: {0}" -f ($snap.derived | ConvertTo-Json -Compress))
Write-Host ("theme:   {0}  (snapshot #{1})" -f $snap.ui.theme, $snap.seq)

$failed = 0
foreach ($text in $Expect) {
    if ($snap.ui.visibleText -contains $text) { Write-Host "PASS on screen: '$text'" }
    else { Write-Host "FAIL not on screen: '$text'" -ForegroundColor Red; $failed++ }
}
foreach ($name in $Hidden) {
    $hit = $snap.ui.named | Where-Object { $_.name -eq $name -and $_.visible }
    if ($hit) { Write-Host "FAIL visible: $name" -ForegroundColor Red; $failed++ }
    else { Write-Host "PASS hidden: $name" }
}

Write-Host "Snapshot: $Out"
exit ([int]($failed -gt 0))
