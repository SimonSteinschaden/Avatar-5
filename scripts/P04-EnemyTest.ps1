<#
.SYNOPSIS
  Multi-process test for P04's first enemy (brute) with host + 2 clients over direct UDP.

.DESCRIPTION
  Build first: Avatar5 > Build > P03 Network Arena Player.

  All bots walk to the brute and fight it (fireball 1.0 s, water wave 2.5 s). The brute notices them,
  telegraphs its slam (red ground ring), slams, recovers, repeats; it is defeated and revives after 8 s.
  Checks: every slam was telegraphed first, slams hit players, the brute was defeated, and all peers
  agree on events and player health.
#>
param(
    [string] $Player = (Join-Path $PSScriptRoot '..\Artifacts\P03Arena\Avatar5_P03Arena.exe'),
    [string] $LogDir = (Join-Path $PSScriptRoot '..\Artifacts\P03Arena\enemy-logs'),
    [int] $Port = 7787,
    [int] $FightSeconds = 20
)
$ErrorActionPreference = 'Stop'
$Player = (Resolve-Path $Player).Path
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
Get-ChildItem $LogDir -Filter '*.log' -ErrorAction SilentlyContinue | Remove-Item -Force

$delay = 5
# After the fight every bot walks south out of the brute's leash, so the log ends in a quiet, comparable state.
$quit = $delay + $FightSeconds + 14
$code = "127.0.0.1:$Port"
$processes = @{}
function Start-Instance([string] $name, [string[]] $extra) {
    $log = Join-Path $LogDir "$name.log"
    $args = @('-batchmode', '-nographics', '-logFile', $log, '-a5-mode', 'direct', '-a5-port', "$Port",
        '-a5-report-players', '1', '-a5-bot-move', '0,0', '-a5-bot-delay', "$delay", '-a5-bot-target', 'brute',
        '-a5-bot-approach', '3.5', '-a5-bot-cast', '0:1.0,1:2.5', '-a5-bot-cast-for', "$FightSeconds", '-a5-bot-after-move', '0,-1') + $extra
    $script:processes[$name] = Start-Process -FilePath $Player -ArgumentList $args -PassThru -WindowStyle Hidden
}

Start-Instance 'host' @('-a5-session', 'host', '-a5-quit-after', "$($quit + 3)")
Start-Sleep -Seconds 3
Start-Instance 'client1' @('-a5-session', 'join', '-a5-code', $code, '-a5-quit-after', "$quit")
Start-Instance 'client2' @('-a5-session', 'join', '-a5-code', $code, '-a5-quit-after', "$quit")

$deadline = (Get-Date).AddSeconds($quit + 40)
while ((Get-Date) -lt $deadline -and ($processes.Values | Where-Object { -not $_.HasExited })) { Start-Sleep -Milliseconds 500 }
$processes.Values | Where-Object { -not $_.HasExited } | ForEach-Object { $_.Kill() }

function ParseCombat([string] $line) {
    $r = @{ Line = $line; Players = @{} }
    foreach ($key in 'events', 'dupes', 'telegraphs', 'enemyCasts', 'enemyHits', 'enemyDefeats', 'playerDefeats') {
        if ($line -match " $key=(-?[0-9]+)") { $r[$key] = [int]$Matches[1] }
    }
    foreach ($m in [regex]::Matches($line, ' p([0-9]+)=([0-9.]+)')) {
        $r.Players[$m.Groups[1].Value] = [double]::Parse($m.Groups[2].Value, [Globalization.CultureInfo]::InvariantCulture)
    }
    return $r
}

function AllCombat([string] $name) {
    return @(Get-Content (Join-Path $LogDir "$name.log") -ErrorAction SilentlyContinue | Where-Object { $_ -like 'A5COMBAT*' } | ForEach-Object { ParseCombat $_ })
}

function LastCombat([string] $name) {
    $all = AllCombat $name
    if ($all.Count -eq 0) { return $null }
    return $all[-1]
}

$results = @()
function Check([string] $name, [bool] $ok, [string] $detail) { $script:results += ,@{ Name = $name; Ok = $ok; Detail = $detail } }

$hostR = LastCombat 'host'
Check 'host reported combat' ($null -ne $hostR) ''
if ($hostR) {
    Check 'brute telegraphed attacks' ($hostR.telegraphs -ge 2) "telegraphs=$($hostR.telegraphs)"
    # A telegraph can end without a slam (brute defeated mid-telegraph), never the other way round.
    Check 'every slam was telegraphed first' ($hostR.enemyCasts -ge 1 -and $hostR.enemyCasts -le $hostR.telegraphs) "telegraphs=$($hostR.telegraphs) slams=$($hostR.enemyCasts)"
    Check 'slams hit players' ($hostR.enemyHits -ge 1) "hits=$($hostR.enemyHits)"
    Check 'players defeated the brute' ($hostR.enemyDefeats -ge 1) "enemyDefeats=$($hostR.enemyDefeats)"
    $hostAll = AllCombat 'host'
    $maxPlayers = ($hostAll | ForEach-Object { $_.Players.Count } | Measure-Object -Maximum).Maximum
    Check 'three players present' ($maxPlayers -eq 3) "players=$maxPlayers"
    foreach ($name in 'client1', 'client2') {
        $c = LastCombat $name
        if (-not $c) { Check "$name reported combat" $false ''; continue }
        # The host keeps running a little longer; compare against the host report that had seen the same events.
        $same = $hostAll | Where-Object { $_.events -eq $c.events -and $_.Players.Count -eq 3 } | Select-Object -Last 1
        Check "$name final state matches a host state with the same event count" ($null -ne $same) "client events=$($c.events)"
        Check "$name no duplicate events" ($c.dupes -eq 0) "dupes=$($c.dupes)"
        if ($same) {
            Check "$name saw same telegraphs/slams/hits" ($c.telegraphs -eq $same.telegraphs -and $c.enemyCasts -eq $same.enemyCasts -and $c.enemyHits -eq $same.enemyHits) "client=$($c.telegraphs)/$($c.enemyCasts)/$($c.enemyHits) host=$($same.telegraphs)/$($same.enemyCasts)/$($same.enemyHits)"
            foreach ($id in $same.Players.Keys) {
                $ok = $c.Players.ContainsKey($id) -and [math]::Abs($c.Players[$id] - $same.Players[$id]) -lt 0.05
                Check "$name player $id health equals host" $ok ("client {0} host {1}" -f $c.Players[$id], $same.Players[$id])
            }
        }
    }
}

$failed = 0
foreach ($r in $results) {
    if ($r.Ok) { Write-Host "PASS  $($r.Name)  ($($r.Detail))" } else { Write-Host "FAIL  $($r.Name)  ($($r.Detail))"; $failed++ }
}
Write-Host ''
foreach ($name in $processes.Keys | Sort-Object) {
    $c = LastCombat $name
    Write-Host "--- $name"
    if ($c) { Write-Host "    $($c.Line)" }
}
Write-Host ''
Write-Host "$($results.Count - $failed)/$($results.Count) checks passed"
exit $failed
