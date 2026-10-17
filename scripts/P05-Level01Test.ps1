<#
.SYNOPSIS
  End-to-end run of the Level01 greybox: solo once per element class, and co-op with mixed classes (host + 2 clients + 1 late joiner).

.DESCRIPTION
  Build first: Avatar5 > Build > Level01 Player
  (or -executeMethod Avatar5.EditorTools.Avatar5P05Level01Builder.BuildPlayerFromCommandLine).

  Route bots walk the level: pull the lever (gate A), fight the brute, light the brazier with fireballs
  (gate B), walk into the goal. Checks that the level completes exactly once, every object ends "on", and
  that every peer - including a client who joined after the lever was pulled - sees the same world state.
#>
param(
    [string] $Player = (Join-Path $PSScriptRoot '..\Artifacts\Level01\Avatar5_Level01.exe'),
    [string] $LogDir = (Join-Path $PSScriptRoot '..\Artifacts\Level01\logs'),
    [int] $Port = 7789,
    [int] $Seconds = 75
)
$ErrorActionPreference = 'Stop'
$Player = (Resolve-Path $Player).Path
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
Get-ChildItem $LogDir -Filter '*.log' -ErrorAction SilentlyContinue | Remove-Item -Force

$route = 'W:4:6.3;I;W:0:9;W:0:15;K;W:3:29;F:7:32;F:7:32;F:7:32;W:0:33;W:0:39;W:0:43'
$results = @()
function Check([string] $name, [bool] $ok, [string] $detail) { $script:results += ,@{ Name = $name; Ok = $ok; Detail = $detail } }

function Run([string] $label, [hashtable[]] $instances) {
    $procs = @{}
    foreach ($i in $instances) {
        if ($i.Wait) { Start-Sleep -Seconds $i.Wait }
        $log = Join-Path $LogDir "$label-$($i.Name).log"
        $args = @('-batchmode', '-nographics', '-logFile', $log, '-a5-mode', 'direct', '-a5-port', "$Port", '-a5-report-players', '1', '-a5-save-dir', (Join-Path $LogDir "saves-$label"),
            '-a5-bot-move', '0,0', '-a5-bot-target', 'brute', '-a5-quit-after', "$($i.Quit)") + $i.Args
        $procs[$i.Name] = Start-Process -FilePath $Player -ArgumentList $args -PassThru -WindowStyle Hidden
    }
    $deadline = (Get-Date).AddSeconds($Seconds + 60)
    while ((Get-Date) -lt $deadline -and ($procs.Values | Where-Object { -not $_.HasExited })) { Start-Sleep -Milliseconds 500 }
    $procs.Values | Where-Object { -not $_.HasExited } | ForEach-Object { $_.Kill() }
}

function Lines([string] $file, [string] $tag) {
    return @(Get-Content (Join-Path $LogDir $file) -ErrorAction SilentlyContinue | Where-Object { $_ -like "$tag*" })
}

function WorldOf([string] $file) {
    $line = Lines $file 'A5WORLD' | Select-Object -Last 1
    if (-not $line) { return $null }
    $w = @{ Line = $line; Completed = ($line -match 'completed=1'); States = '' }
    if ($line -match 'states=([0-9:,]+)') { $w.States = $Matches[1] }
    return $w
}

# ---- solo, once per element class (every class must be able to finish alone) -----------------
$code = "127.0.0.1:$Port"
foreach ($class in 'fire', 'water', 'earth', 'air') {
    Run "solo-$class" @(@{ Name = 'host'; Quit = $Seconds; Args = @('-a5-session', 'host', '-a5-class', $class, '-a5-bot-delay', '3', '-a5-bot-route', $route) })
    $log = "solo-$class-host.log"
    $soloWorld = WorldOf $log
    $plays = (Get-Content (Join-Path $LogDir $log) -ErrorAction SilentlyContinue | Where-Object { $_ -like '*Actor 1 plays*' } | Select-Object -First 1)
    Check "solo ${class}: class applied" ([string]$plays -match "plays $class") "$plays"
    Check "solo ${class}: route finished" ((Lines $log 'A5ROUTE' | Where-Object { $_ -like '*done*' }).Count -ge 1) ''
    Check "solo ${class}: level completed" ($soloWorld -and $soloWorld.Completed) $(if ($soloWorld) { $soloWorld.Line } else { 'no world report' })
    Check "solo ${class}: lever, gates and shrine on" ($soloWorld -and $soloWorld.States -eq '1001:1,1101:1,2001:1,2101:1,9001:0') $(if ($soloWorld) { $soloWorld.States } else { '' })
}

# ---- co-op with a late joiner ------------------------------------------------------------------
$coop = @(
    @{ Name = 'host'; Quit = $Seconds + 6; Args = @('-a5-session', 'host', '-a5-class', 'fire', '-a5-bot-delay', '6', '-a5-bot-route', $route) },
    @{ Name = 'client1'; Wait = 3; Quit = $Seconds; Args = @('-a5-session', 'join', '-a5-code', $code, '-a5-class', 'water', '-a5-bot-delay', '3', '-a5-bot-route', $route) },
    @{ Name = 'client2'; Wait = 1; Quit = $Seconds; Args = @('-a5-session', 'join', '-a5-code', $code, '-a5-class', 'earth', '-a5-bot-delay', '2', '-a5-bot-route', $route) },
    # Joins after the lever was pulled; must still see gate A open (snapshot on join). Stands still.
    @{ Name = 'late'; Wait = 14; Quit = $Seconds - 18; Args = @('-a5-session', 'join', '-a5-code', $code, '-a5-class', 'air') }
)
Run 'coop' $coop
$hostWorld = WorldOf 'coop-host.log'
Check 'coop: level completed' ($hostWorld -and $hostWorld.Completed) $(if ($hostWorld) { $hostWorld.Line } else { 'no world report' })
$completedEvents = (Lines 'coop-host.log' 'A5WORLD' | Where-Object { $_ -like '*completed=1*' }).Count
Check 'coop: host reports completion' ($completedEvents -ge 1) "lines with completed=1: $completedEvents"
foreach ($name in 'client1', 'client2', 'late') {
    $w = WorldOf "coop-$name.log"
    Check "coop: $name world state equals host" ($w -and $hostWorld -and $w.States -eq $hostWorld.States -and $w.Completed -eq $hostWorld.Completed) $(if ($w) { "$($w.States) completed=$($w.Completed)" } else { 'no world report' })
}
# First report after the snapshot arrived (earlier lines are written before the connection exists).
$lateFirst = Lines 'coop-late.log' 'A5WORLD' | Where-Object { $_ -match 'states=[0-9]' } | Select-Object -First 1
Check 'coop: late joiner saw gate A open in its first report' ($lateFirst -match '1101:1') "$lateFirst"
 Check 'coop: late joiner arrived before the level was finished' ($lateFirst -match 'completed=0') "$lateFirst"
$doneCount = 0
foreach ($name in 'host', 'client1', 'client2') { if ((Lines "coop-$name.log" 'A5ROUTE' | Where-Object { $_ -like '*done*' }).Count -ge 1) { $doneCount++ } }
Check 'coop: every route bot reached the end' ($doneCount -eq 3) "$doneCount/3"

$failed = 0
foreach ($r in $results) {
    if ($r.Ok) { Write-Host "PASS  $($r.Name)  ($($r.Detail))" } else { Write-Host "FAIL  $($r.Name)  ($($r.Detail))"; $failed++ }
}
Write-Host ''
foreach ($f in Get-ChildItem $LogDir -Filter '*.log' | Sort-Object Name) {
    Write-Host "--- $($f.Name)"
    Get-Content $f.FullName | Where-Object { $_ -like 'A5ROUTE*' -or $_ -like 'A5SESSION*result*' } | ForEach-Object { Write-Host "    $_" }
    Get-Content $f.FullName | Where-Object { $_ -like 'A5WORLD*' } | Select-Object -Last 1 | ForEach-Object { Write-Host "    $_" }
}
Write-Host ''
Write-Host "$($results.Count - $failed)/$($results.Count) checks passed"
exit $failed
