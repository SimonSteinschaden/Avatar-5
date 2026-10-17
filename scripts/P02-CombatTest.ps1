<#
.SYNOPSIS
  Multi-process combat test for P02 (host + 3 clients, direct UDP on this PC).

.DESCRIPTION
  Build first: Avatar5 > Build > P03 Network Arena Player (contains combat since P02).

  All four bots walk up to the nearest training dummy and cast:
    host     fireball every 1.0 s
    client1  water wave every 2.2 s
    client2  fireball 1.0 s + water wave 2.5 s
    client3  tries a fireball every 0.05 s (spam; host must enforce the 0.8 s cooldown)
  Casting stops after CastSeconds; burning runs out; then every peer reports dummy health, the damage it
  received as events, accepted casts and reactions. All peers must agree with the host.
#>
param(
    [string] $Player = (Join-Path $PSScriptRoot '..\Artifacts\P03Arena\Avatar5_P03Arena.exe'),
    [string] $LogDir = (Join-Path $PSScriptRoot '..\Artifacts\P03Arena\combat-logs'),
    [int] $Port = 7785,
    [int] $CastSeconds = 14
)
$ErrorActionPreference = 'Stop'
$Player = (Resolve-Path $Player).Path
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
Get-ChildItem $LogDir -Filter '*.log' -ErrorAction SilentlyContinue | Remove-Item -Force

$delay = 6
$quit = $delay + $CastSeconds + 9
$code = "127.0.0.1:$Port"
$processes = @{}
function Start-Instance([string] $name, [string[]] $extra) {
    $log = Join-Path $LogDir "$name.log"
    $args = @('-batchmode', '-nographics', '-logFile', $log, '-a5-mode', 'direct', '-a5-port', "$Port",
        '-a5-report-players', '1', '-a5-bot-move', '0,0', '-a5-bot-delay', "$delay", '-a5-bot-approach', '3.5',
        '-a5-bot-cast-for', "$CastSeconds") + $extra
    $script:processes[$name] = Start-Process -FilePath $Player -ArgumentList $args -PassThru -WindowStyle Hidden
}

Start-Instance 'host' @('-a5-session', 'host', '-a5-bot-cast', '0:1.0', '-a5-quit-after', "$($quit + 3)")
Start-Sleep -Seconds 3
Start-Instance 'client1' @('-a5-session', 'join', '-a5-code', $code, '-a5-bot-cast', '1:2.2', '-a5-quit-after', "$quit")
Start-Instance 'client2' @('-a5-session', 'join', '-a5-code', $code, '-a5-bot-cast', '0:1.0,1:2.5', '-a5-quit-after', "$quit")
Start-Instance 'client3' @('-a5-session', 'join', '-a5-code', $code, '-a5-bot-cast', '0:0.05', '-a5-quit-after', "$quit")

$deadline = (Get-Date).AddSeconds($quit + 40)
while ((Get-Date) -lt $deadline -and ($processes.Values | Where-Object { -not $_.HasExited })) { Start-Sleep -Milliseconds 500 }
$processes.Values | Where-Object { -not $_.HasExited } | ForEach-Object { $_.Kill() }

function LastCombat([string] $name) {
    $line = Get-Content (Join-Path $LogDir "$name.log") -ErrorAction SilentlyContinue | Where-Object { $_ -like 'A5COMBAT*' } | Select-Object -Last 1
    if (-not $line) { return $null }
    $r = @{ Line = $line; Dummies = @{} }
    foreach ($key in 'events', 'casts', 'steam', 'extinguish', 'dupes') {
        if ($line -match "$key=(-?[0-9]+)") { $r[$key] = [int]$Matches[1] }
    }
    foreach ($m in [regex]::Matches($line, 'e([0-9]+)=([0-9.]+)/([0-9]+)\|dmg=([0-9.]+)\|st=([0-9]+)')) {
        $inv = [Globalization.CultureInfo]::InvariantCulture
        $r.Dummies[$m.Groups[1].Value] = @{
            Health = [double]::Parse($m.Groups[2].Value, $inv); Max = [double]::Parse($m.Groups[3].Value, $inv)
            Damage = [double]::Parse($m.Groups[4].Value, $inv); Status = [int]$m.Groups[5].Value
        }
    }
    return $r
}

$results = @()
function Check([string] $name, [bool] $ok, [string] $detail) { $script:results += ,@{ Name = $name; Ok = $ok; Detail = $detail } }

$hostR = LastCombat 'host'
Check 'host reported combat' ($null -ne $hostR) ''
if ($hostR) {
    Check 'casts accepted on host' ($hostR.casts -ge 30) "casts=$($hostR.casts)"
    Check 'Steam reaction happened' ($hostR.steam -ge 1) "steam=$($hostR.steam)"
    Check 'Extinguish reaction happened' ($hostR.extinguish -ge 1) "extinguish=$($hostR.extinguish)"
    Check 'two dummies present' ($hostR.Dummies.Count -eq 2) "dummies=$($hostR.Dummies.Count)"
    foreach ($id in $hostR.Dummies.Keys) {
        $d = $hostR.Dummies[$id]
        Check "host: dummy $id damage events = HP lost" ([math]::Abs(($d.Max - $d.Health) - $d.Damage) -lt 0.5) ("lost {0:0.0}, events {1:0.0}" -f ($d.Max - $d.Health), $d.Damage)
    }
    # Spammer: at 0.8 s cooldown it can land at most CastSeconds/0.8 + 1 fireballs.
    foreach ($name in 'client1', 'client2', 'client3') {
        $c = LastCombat $name
        if (-not $c) { Check "$name reported combat" $false ''; continue }
        Check "$name received same number of events as host" ($c.events -eq $hostR.events) "client=$($c.events) host=$($hostR.events)"
        Check "$name no duplicate events" ($c.dupes -eq 0) "dupes=$($c.dupes)"
        foreach ($id in $hostR.Dummies.Keys) {
            $h = $hostR.Dummies[$id]; $d = $c.Dummies[$id]
            if (-not $d) { Check "$name sees dummy $id" $false ''; continue }
            Check "$name dummy $id health equals host" ([math]::Abs($d.Health - $h.Health) -lt 0.05) ("client {0:0.0} host {1:0.0}" -f $d.Health, $h.Health)
            Check "$name dummy $id damage events equal host" ([math]::Abs($d.Damage - $h.Damage) -lt 0.05) ("client {0:0.0} host {1:0.0}" -f $d.Damage, $h.Damage)
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
