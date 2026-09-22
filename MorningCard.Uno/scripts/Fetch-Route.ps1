<#
.SYNOPSIS
    Generates the committed route asset from OSRM.

.DESCRIPTION
    Run once, by hand. The app has no routing service: this writes a fixed route into the repo and
    the card lays the morning's times over it. Durations come back free-flow, and TimeRulerLayout
    scales them to whatever the card is currently saying the drive takes, so the numbers here only
    have to set the SHAPE of the speed profile, never its length.

    The public OSRM demo server is fine for a one-off generation like this and is not to be called
    from the app.

.EXAMPLE
    ./scripts/Fetch-Route.ps1
    ./scripts/Fetch-Route.ps1 -HomeLat 45.5385 -HomeLon -73.6295 -Verbose
#>
[CmdletBinding()]
param(
    # Home. Roughly a 14 minute morning drive from the school, which is what the seeded trip says.
    [double]$HomeLat = 45.5385,
    [double]$HomeLon = -73.6295,

    # Where the car stops: Rue Rachel Est, the street the card names as the parking spot.
    [double]$ParkLat = 45.5262,
    [double]$ParkLon = -73.5810,

    # The door.
    [double]$SchoolLat = 45.5250,
    [double]$SchoolLon = -73.5795,

    [string]$Server = 'https://router.project-osrm.org',

    [string]$Out = "$PSScriptRoot/../MorningCard/Assets/route.json"
)

$ErrorActionPreference = 'Stop'

function Get-Leg {
    param(
        [string]$Profile,
        [double]$FromLat, [double]$FromLon,
        [double]$ToLat,   [double]$ToLon
    )

    # OSRM takes lon,lat - the opposite order to how coordinates are usually spoken.
    $coords = '{0},{1};{2},{3}' -f $FromLon, $FromLat, $ToLon, $ToLat
    $uri = "$Server/route/v1/$Profile/$coords`?overview=full&geometries=geojson&annotations=duration,distance"

    Write-Verbose "GET $uri"
    $response = Invoke-RestMethod -Uri $uri -TimeoutSec 60

    if ($response.code -ne 'Ok') {
        throw "OSRM refused the $Profile leg: $($response.code) $($response.message)"
    }

    $route = $response.routes[0]
    $annotation = $route.legs[0].annotation

    [pscustomobject]@{
        coordinates = $route.geometry.coordinates
        durations   = $annotation.duration
        distances   = $annotation.distance
        totalMin    = [Math]::Round($route.duration / 60, 1)
        totalKm     = [Math]::Round($route.distance / 1000, 2)
    }
}

$drive = Get-Leg -Profile 'driving' -FromLat $HomeLat -FromLon $HomeLon -ToLat $ParkLat -ToLon $ParkLon
$walk  = Get-Leg -Profile 'foot'    -FromLat $ParkLat -FromLon $ParkLon -ToLat $SchoolLat -ToLon $SchoolLon

foreach ($leg in @(@{ n = 'drive'; v = $drive }, @{ n = 'walk'; v = $walk })) {
    $shape = $leg.v.coordinates.Count
    $edges = $leg.v.durations.Count

    if ($edges -ne $shape - 1) {
        throw "$($leg.n): got $shape shape points and $edges annotations; expected one fewer annotation."
    }

    "{0,-6} {1,5} pts  {2,6} min  {3,6} km" -f $leg.n, $shape, $leg.v.totalMin, $leg.v.totalKm | Write-Host
}

$document = [ordered]@{
    drive = [ordered]@{
        coordinates = $drive.coordinates
        durations   = $drive.durations
        distances   = $drive.distances
    }
    walk = [ordered]@{
        coordinates = $walk.coordinates
        durations   = $walk.durations
        distances   = $walk.distances
    }
}

$resolved = [System.IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force -Path (Split-Path $resolved) | Out-Null

# Depth matters: the coordinate array is three levels down and silently truncates below 5.
$document | ConvertTo-Json -Depth 10 -Compress | Set-Content -Path $resolved -Encoding utf8NoBOM

"wrote $resolved ({0:N0} bytes)" -f (Get-Item $resolved).Length | Write-Host
