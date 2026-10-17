<#
.SYNOPSIS
  Multi-process test for P03 networked players (direct UDP on this PC, 4 processes).

.DESCRIPTION
  Build first: Avatar5 > Build > P03 Network Arena Player
  (or -executeMethod Avatar5.EditorTools.Avatar5P03NetworkArenaBuilder.BuildPlayerFromCommandLine).

  host     stands still
  client1  moves +X until blocked
  client2  moves -Z until blocked
  client3  moves -X and also forges commands for every other avatar (host must refuse them)

  Every process logs all avatar positions ("A5PLAYER ...") so the script can compare what each peer sees.
#>
param(
    [string] $Player = (Join-Path $PSScriptRoot '..\Artifacts\P03Arena\Avatar5_P03Arena.exe'),
    [string] $LogDir = (Join-Path $PSScriptRoot '..\Artifacts\P03Arena\logs'),
    [int] $Port = 7783,
    [int] $Seconds = 24
)
$ErrorActionPreference = 'Stop'
$Player = (Resolve-Path $Player).Path
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
Get-ChildItem $LogDir -Filter '*.log' -ErrorAction SilentlyContinue | Remove-Item -Force

$code = "127.0.0.1:$Port"
$processes = @{}
function Start-Instance([string] $name, [string[]] $extra) {
    $log = Join-Path $LogDir "$name.log"
    $args = @('-batchmode', '-nographics', '-logFile', $log, '-a5-mode', 'direct', '-a5-port', "$Port", '-a5-report-players', '1', '-a5-bot-delay', '6') + $extra
    $script:processes[$name] = Start-Process -FilePath $Player -ArgumentList $args -PassThru -WindowStyle Hidden
}

Start-Instance 'host' @('-a5-session', 'host', '-a5-bot-move', '0,0', '-a5-quit-after', "$($Seconds + 4)")
Start-Sleep -Seconds 4
Start-Instance 'client1' @('-a5-session', 'join', '-a5-code', $code, '-a5-bot-move', '1,0', '-a5-quit-after', "$Seconds")
# Each client joins only after the previous one is connected: fixed slot order (1..3), so the movers never collide.
function Wait-Joined([string] $name) {
    $log = Join-Path $LogDir "$name.log"
    $until = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $until) {
        if ((Test-Path $log) -and (Select-String -Path $log -Pattern "A5SESSION result=ok" -Quiet)) { return }
        Start-Sleep -Milliseconds 250
    }
}
Wait-Joined 'client1'
Start-Instance 'client2' @('-a5-session', 'join', '-a5-code', $code, '-a5-bot-move', '0,-1', '-a5-quit-after', "$Seconds")
Wait-Joined 'client2'
Start-Instance 'client3' @('-a5-session', 'join', '-a5-code', $code, '-a5-bot-move', '-1,0', '-a5-bot-hijack', '-a5-quit-after', "$Seconds")

$deadline = (Get-Date).AddSeconds($Seconds + 30)
while ((Get-Date) -lt $deadline -and ($processes.Values | Where-Object { -not $_.HasExited })) { Start-Sleep -Milliseconds 500 }
$processes.Values | Where-Object { -not $_.HasExited } | ForEach-Object { $_.Kill() }

function Reports([string] $name) {
    $log = Join-Path $LogDir "$name.log"
    if (-not (Test-Path $log)) { return @() }
    $result = @()
    foreach ($line in (Get-Content $log | Where-Object { $_ -like 'A5PLAYER*' })) {
        $entry = @{ Count = 0; Actors = @{} }
        if ($line -match 'count=(\d+)') { $entry.Count = [int]$Matches[1] }
        foreach ($m in [regex]::Matches($line, 'a(\d+)=(-?[\d.]+),(-?[\d.]+)(\[seq=(\d+),noauth=(\d+),rate=(\d+)\])?')) {
            $entry.Actors[[int]$m.Groups[1].Value] = @{
                X = [double]::Parse($m.Groups[2].Value, [Globalization.CultureInfo]::InvariantCulture)
                Z = [double]::Parse($m.Groups[3].Value, [Globalization.CultureInfo]::InvariantCulture)
                NoAuth = if ($m.Groups[6].Success) { [int]$m.Groups[6].Value } else { 0 }
                Rate = if ($m.Groups[7].Success) { [int]$m.Groups[7].Value } else { 0 }
            }
        }
        $result += ,$entry
    }
    return $result
}

function SelfActor([string] $name) {
    $line = Get-Content (Join-Path $LogDir "$name.log") -ErrorAction SilentlyContinue | Where-Object { $_ -like 'A5PLAYER*' } | Select-Object -Last 1
    if ($line -match 'self=([0-9]+)') { return [int]$Matches[1] }
    return -1
}
# Actor ids follow connection order, which is not deterministic; read each client's own id from its log.
$aHost = SelfActor 'host'; $aMoveX = SelfActor 'client1'; $aMoveZ = SelfActor 'client2'; $aHijack = SelfActor 'client3'
Write-Host "actors: host=a$aHost client1(+X)=a$aMoveX client2(-Z)=a$aMoveZ client3(-X, hijack)=a$aHijack"

$hostReports = @(Reports 'host' | Where-Object { $_.Count -eq 4 })
$client2Reports = @(Reports 'client2' | Where-Object { $_.Count -eq 4 })
$results = @()
function Check([string] $name, [bool] $ok, [string] $detail) {
    $script:results += ,@{ Name = $name; Ok = $ok; Detail = $detail }
}

Check 'host sees 4 avatars' ($hostReports.Count -gt 0) "$($hostReports.Count) reports with count=4"
if ($hostReports.Count -ge 2) {
    $first = $hostReports[0].Actors; $last = $hostReports[-1].Actors
    $dx2 = $last[$aMoveX].X - $first[$aMoveX].X; $dz3 = $last[$aMoveZ].Z - $first[$aMoveZ].Z; $dx4 = $last[$aHijack].X - $first[$aHijack].X
    $d1 = [math]::Sqrt([math]::Pow($last[$aHost].X - $first[$aHost].X, 2) + [math]::Pow($last[$aHost].Z - $first[$aHost].Z, 2))
    Check 'client1 avatar moved +X (host view)' ($dx2 -gt 3) ("dx={0:0.00}" -f $dx2)
    Check 'client2 avatar moved -Z (host view)' ($dz3 -lt -3) ("dz={0:0.00}" -f $dz3)
    Check 'hijacker own commands moved it -X' ($dx4 -lt -3) ("dx={0:0.00}" -f $dx4)
    Check 'host avatar stayed in place' ($d1 -lt 0.5) ("moved {0:0.00} m" -f $d1)
    $noauth = ($last.Values | ForEach-Object { $_.NoAuth } | Measure-Object -Sum).Sum
    Check 'forged commands refused by host' ($noauth -gt 0) "noauth total=$noauth"
    $rate = ($last.Values | ForEach-Object { $_.Rate } | Measure-Object -Sum).Sum
    Check 'no rate-limit hits for normal clients' ($last[$aMoveX].Rate -eq 0 -and $last[$aMoveZ].Rate -eq 0) "rate total=$rate"
    if ($client2Reports.Count -gt 0) {
        $c = $client2Reports[-1].Actors
        $gap = [math]::Sqrt([math]::Pow($c[$aMoveX].X - $last[$aMoveX].X, 2) + [math]::Pow($c[$aMoveX].Z - $last[$aMoveX].Z, 2))
        Check 'client2 sees client1 avatar where host has it' ($gap -lt 0.75) ("gap {0:0.00} m" -f $gap)
    } else {
        Check 'client2 sees client1 avatar where host has it' $false 'client2 never saw 4 avatars'
    }
}

$failed = 0
foreach ($r in $results) {
    if ($r.Ok) { Write-Host "PASS  $($r.Name)  ($($r.Detail))" } else { Write-Host "FAIL  $($r.Name)  ($($r.Detail))"; $failed++ }
}
Write-Host ''
foreach ($name in $processes.Keys | Sort-Object) {
    Write-Host "--- $name (last 3 reports)"
    Get-Content (Join-Path $LogDir "$name.log") -ErrorAction SilentlyContinue | Where-Object { $_ -like 'A5PLAYER*' -or $_ -like 'A5SESSION*result*' } | Select-Object -Last 4 | ForEach-Object { Write-Host "    $_" }
}
Write-Host ''
Write-Host "$($results.Count - $failed)/$($results.Count) checks passed"
exit $failed
