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
    # Home: Longueuil, on the south shore. Chosen for the shape of the drive, not just its length.
    # The route crosses Pont Jacques-Cartier and comes into the Plateau from the south-east, so it
    # occupies the lower-right of the map panel and stays clear of the ETA block, which owns the
    # top-left. A route that approaches from the north-west put its origin pin, its dwell ring and
    # its callout underneath the card's own headline. Roughly a 14 minute drive, which is what the
    # seeded trip already claims.
    [double]$HomeLat = 45.5230,
    [double]$HomeLon = -73.5080,

    # Where the car stops: Rue Rachel Est, the street the card names as the parking spot.
    [double]$ParkLat = 45.5262,
    [double]$ParkLon = -73.5810,

    # The door.
    [double]$SchoolLat = 45.5250,
    [double]$SchoolLon = -73.5795,

    [string]$Server = 'https://router.project-osrm.org',

    # OSRM's public demo hosts the driving profile only: a /foot/ request is silently answered by a
    # car, which obeys one-way streets a pedestrian ignores. The first run of this script produced
    # a walk that set off southwest, flipped northwest and doubled back - 332 m of car manoeuvre
    # for a 176 m stroll. Valhalla's demo has real pedestrian costing, so the walk comes from there.
    [string]$PedestrianServer = 'https://valhalla1.openstreetmap.de',

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

function ConvertFrom-Polyline6 {
    <#
        Valhalla answers with an encoded polyline at 1e-6 precision rather than a coordinate array.
        Standard Google polyline decoding, six decimal places instead of five.
    #>
    param([string]$Encoded)

    $lat = 0; $lon = 0; $i = 0
    $points = [System.Collections.Generic.List[object]]::new()

    while ($i -lt $Encoded.Length) {
        foreach ($axis in 'lat', 'lon') {
            $shift = 0; $result = 0

            do {
                $b = [int][char]$Encoded[$i++] - 63
                $result = $result -bor (($b -band 0x1f) -shl $shift)
                $shift += 5
            } while ($b -ge 0x20)

            $delta = if ($result -band 1) { -(($result -shr 1) + 1) } else { $result -shr 1 }

            if ($axis -eq 'lat') { $lat += $delta } else { $lon += $delta }
        }

        # Written lon,lat to match the GeoJSON order the rest of the asset uses.
        $points.Add(@([Math]::Round($lon / 1e6, 6), [Math]::Round($lat / 1e6, 6)))
    }

    $points
}

function Get-WalkLeg {
    param([double]$FromLat, [double]$FromLon, [double]$ToLat, [double]$ToLon)

    $request = @{
        locations = @(
            @{ lat = $FromLat; lon = $FromLon },
            @{ lat = $ToLat;   lon = $ToLon }
        )
        costing = 'pedestrian'
    } | ConvertTo-Json -Depth 5 -Compress

    $uri = "$PedestrianServer/route?json=$([uri]::EscapeDataString($request))"

    Write-Verbose "GET $uri"
    $response = Invoke-RestMethod -Uri $uri -TimeoutSec 60

    $leg = $response.trip.legs[0]
    $coordinates = ConvertFrom-Polyline6 -Encoded $leg.shape

    if ($coordinates.Count -lt 2) {
        throw "Valhalla returned $($coordinates.Count) shape points for the walk."
    }

    # Valhalla annotates per manoeuvre, not per edge. Rather than smear one manoeuvre's time over
    # its edges by guesswork, the walk is timed at a constant pace - which is what walking is. The
    # speed profile the ticks exist to show belongs to the drive; a person crossing three hundred
    # metres does not have one.
    $totalMeters = $leg.summary.length * 1000
    $totalSeconds = $leg.summary.time

    $distances = @()
    $durations = @()

    for ($i = 1; $i -lt $coordinates.Count; $i++) {
        $a = $coordinates[$i - 1]
        $b = $coordinates[$i]
        $dx = ($b[0] - $a[0]) * 111320 * [Math]::Cos($a[1] * [Math]::PI / 180)
        $dy = ($b[1] - $a[1]) * 111320
        $distances += [Math]::Sqrt($dx * $dx + $dy * $dy)
    }

    # Normalised so the segment lengths add up to the length Valhalla reports.
    $measured = ($distances | Measure-Object -Sum).Sum
    $scale = if ($measured -gt 0) { $totalMeters / $measured } else { 1 }
    $distances = $distances | ForEach-Object { $_ * $scale }
    $durations = $distances | ForEach-Object { if ($totalMeters -gt 0) { $totalSeconds * $_ / $totalMeters } else { 0 } }

    [pscustomobject]@{
        coordinates = $coordinates
        durations   = $durations
        distances   = $distances
        totalMin    = [Math]::Round($totalSeconds / 60, 1)
        totalKm     = [Math]::Round($totalMeters / 1000, 2)
    }
}

$drive = Get-Leg -Profile 'driving' -FromLat $HomeLat -FromLon $HomeLon -ToLat $ParkLat -ToLon $ParkLon

# The walk is the optional half. An earlier run threw here and took the whole fetch down with it,
# leaving the previous asset in place and looking like the run had simply done nothing - so a
# pedestrian router that is unreachable now costs the walk's shape, not the drive as well.
try {
    $walk = Get-WalkLeg -FromLat $ParkLat -FromLon $ParkLon -ToLat $SchoolLat -ToLon $SchoolLon
}
catch {
    Write-Warning "Pedestrian routing failed ($($_.Exception.Message)). Falling back to OSRM, which answers /foot/ with a car - the walk will obey one-way streets."
    $walk = Get-Leg -Profile 'foot' -FromLat $ParkLat -FromLon $ParkLon -ToLat $SchoolLat -ToLon $SchoolLon
}

foreach ($leg in @(@{ n = 'drive'; v = $drive }, @{ n = 'walk'; v = $walk })) {
    $shape = $leg.v.coordinates.Count
    $edges = $leg.v.durations.Count

    if ($edges -ne $shape - 1) {
        throw "$($leg.n): got $shape shape points and $edges annotations; expected one fewer annotation."
    }

    # Pace is the tell that a leg came back from the right profile: a walk answered by the driving
    # engine returns at twenty-odd km/h and looks perfectly plausible in the totals.
    $kmh = if ($leg.v.totalMin -gt 0) { $leg.v.totalKm / ($leg.v.totalMin / 60) } else { 0 }

    "{0,-6} {1,5} pts  {2,6} min  {3,6} km  {4,5:N1} km/h" -f `
        $leg.n, $shape, $leg.v.totalMin, $leg.v.totalKm, $kmh | Write-Host
}

if ($walk.totalMin -gt 0 -and ($walk.totalKm / ($walk.totalMin / 60)) -gt 8) {
    Write-Warning "The walk came back faster than 8 km/h. That is not a person - check the pedestrian profile."
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
