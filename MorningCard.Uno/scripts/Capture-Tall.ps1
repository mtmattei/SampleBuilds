#requires -Version 7
<#
.SYNOPSIS
    Captures the app at a given window size, so the whole card fits in frame.

.DESCRIPTION
    The card is about 888 epx tall and the default window is 640, so every capture until now has
    been clipped - which is how the card shadow went unreviewed. AppWindow.Resize is a documented
    no-op on net10.0-desktop, but SetWindowPos on the host HWND does move and size it.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Width = 520,
    [int]$Height = 1120
)

$ErrorActionPreference = 'Stop'

Add-Type -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
'@ -Name Win32Size -Namespace Sizer

$process = Get-Process -Id $ProcessId -ErrorAction Stop
$hwnd = $process.MainWindowHandle

if ($hwnd -eq [IntPtr]::Zero) {
    throw "Process $ProcessId has no main window."
}

# SWP_NOZORDER | SWP_NOACTIVATE. Do not force topmost: that destroyed the Skia window on a
# previous project.
#
# Sized twice on purpose. The app opens at 1024x640, which is the wide two-column stage, and
# collapsing that straight to a tall narrow window leaves stale pixels behind: text from the wide
# layout survived in the band the mode chips landed in, so every chip label appeared to carry a
# ghost - including a stray "68" that was the cabin-temperature value from the old layout. It is an
# invalidation gap in the Skia renderer, not anything in the app's tree (probed: one TextBlock per
# chip, nothing overlapping). A second, small relayout repaints the band, so the capture gets a
# clean frame.
$flags = 0x0004 -bor 0x0010
[void][Sizer.Win32Size]::SetWindowPos($hwnd, [IntPtr]::Zero, 40, 10, $Width + 8, $Height, $flags)
Start-Sleep -Milliseconds 900
[void][Sizer.Win32Size]::SetWindowPos($hwnd, [IntPtr]::Zero, 40, 10, $Width, $Height, $flags)
Start-Sleep -Milliseconds 1200

& (Join-Path $PSScriptRoot '..\..\..\..\SimpleThemeDemo\tools\Capture-Window.ps1') -ProcessId $ProcessId -Out $Out
