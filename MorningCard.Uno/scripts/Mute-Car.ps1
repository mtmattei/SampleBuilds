#requires -Version 7
<#
.SYNOPSIS
    Writes the desaturated car the card shows, from the full-colour source photo.

.DESCRIPTION
    Run once, by hand, whenever Assets/Car/xc60.png changes. The matte design keeps the car at about
    30% of its own saturation so it reads as a grey object on the paper rather than as a product
    shot. XAML on the Skia renderer has no image colour filter, so the muted copy is a real asset.

    Saturation is cut around Rec. 709 luminance, the same weights the map's colour filter uses in
    RouteRulerRenderer, so the car and the map mute the same way. Alpha is left untouched.

.EXAMPLE
    ./scripts/Mute-Car.ps1
#>
[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\MorningCard\Assets\Car\xc60.png'),
    [string]$Out = (Join-Path $PSScriptRoot '..\MorningCard\Assets\Car\xc60_muted.png'),
    [double]$Saturation = 0.3
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$s = [single]$Saturation
$lr = [single]0.2126
$lg = [single]0.7152
$lb = [single]0.0722

# System.Drawing's ColorMatrix is row-vector: each ROW is one input channel's contribution.
# Every cell parenthesised: in PowerShell the comma binds tighter than +, so a bare "a + s, b"
# builds an array rather than a sum.
$rows = [single[][]]@(
    [single[]]@((($lr * (1 - $s)) + $s), ($lr * (1 - $s)), ($lr * (1 - $s)), 0, 0),
    [single[]]@(($lg * (1 - $s)), (($lg * (1 - $s)) + $s), ($lg * (1 - $s)), 0, 0),
    [single[]]@(($lb * (1 - $s)), ($lb * (1 - $s)), (($lb * (1 - $s)) + $s), 0, 0),
    [single[]]@(0, 0, 0, 1, 0),
    [single[]]@(0, 0, 0, 0, 1)
)

$photo = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Source).Path)

try {
    $target = [System.Drawing.Bitmap]::new($photo.Width, $photo.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($target)
    $attributes = [System.Drawing.Imaging.ImageAttributes]::new()

    try {
        $attributes.SetColorMatrix([System.Drawing.Imaging.ColorMatrix]::new($rows))
        $bounds = [System.Drawing.Rectangle]::new(0, 0, $photo.Width, $photo.Height)
        $graphics.DrawImage($photo, $bounds, 0, 0, $photo.Width, $photo.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
        $target.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $attributes.Dispose()
        $graphics.Dispose()
        $target.Dispose()
    }
}
finally {
    $photo.Dispose()
}

Write-Host "Wrote $Out at $([int]($Saturation * 100))% saturation"
