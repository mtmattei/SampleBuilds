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
[void][Sizer.Win32Size]::SetWindowPos($hwnd, [IntPtr]::Zero, 40, 10, $Width, $Height, 0x0004 -bor 0x0010)
Start-Sleep -Milliseconds 1200

& (Join-Path $PSScriptRoot '..\..\..\..\SimpleThemeDemo\tools\Capture-Window.ps1') -ProcessId $ProcessId -Out $Out
