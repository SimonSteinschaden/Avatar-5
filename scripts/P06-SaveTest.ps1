<#
.SYNOPSIS
  Save/continue test for P06 on the Level01 player.

.DESCRIPTION
  Build first: Avatar5 > Build > Level01 Player.

  1. Host plays until gate A opens (checkpoint) and quits.         -> save file exists with lever + gate A on
  2. Host restarts with -a5-continue, a client joins.               -> both see lever + gate A on from the start
  3. The save is replaced by one from an unknown future schema.     -> host refuses it, starts fresh, keeps the file
#>
param(
    [string] $Player = (Join-Path $PSScriptRoot '..\Artifacts\Level01\Avatar5_Level01.exe'),
    [string] $LogDir = (Join-Path $PSScriptRoot '..\Artifacts\Level01\save-logs'),
    [int] $Port = 7791
)
$ErrorActionPreference = 'Stop'
$Player = (Resolve-Path $Player).Path
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
Get-ChildItem $LogDir -Filter '*.log' -ErrorAction SilentlyContinue | Remove-Item -Force
$saveDir = Join-Path $LogDir 'saves'
if (Test-Path $saveDir) { Remove-Item $saveDir -Recurse -Force }
$saveFile = Join-Path $saveDir 'campaign.json'
$code = "127.0.0.1:$Port"

$results = @()
function Check([string] $name, [bool] $ok, [string] $detail) { $script:results += ,@{ Name = $name; Ok = $ok; Detail = $detail } }

function Start-Instance([string] $name, [int] $quit, [string[]] $extra) {
    $log = Join-Path $LogDir "$name.log"
    $args = @('-batchmode', '-nographics', '-logFile', $log, '-a5-mode', 'direct', '-a5-port', "$Port", '-a5-report-players', '1',
        '-a5-save-dir', $saveDir, '-a5-quit-after', "$quit") + $extra
    return Start-Process -FilePath $Player -ArgumentList $args -PassThru -WindowStyle Hidden
}
function WaitAll($procs, [int] $seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline -and ($procs | Where-Object { -not $_.HasExited })) { Start-Sleep -Milliseconds 500 }
    $procs | Where-Object { -not $_.HasExited } | ForEach-Object { $_.Kill() }
}
function Lines([string] $name, [string] $tag) { return @(Get-Content (Join-Path $LogDir "$name.log") -ErrorAction SilentlyContinue | Where-Object { $_ -like "$tag*" }) }
function FirstWorld([string] $name) { return Lines $name 'A5WORLD' | Where-Object { $_ -match 'states=[0-9]' } | Select-Object -First 1 }

# 1. play to the first checkpoint
$p = Start-Instance 'run1-host' 16 @('-a5-session', 'host', '-a5-bot-move', '0,0', '-a5-bot-delay', '2', '-a5-bot-route', 'W:4:6.3;I;W:0:9')
WaitAll @($p) 40
Check 'run1: checkpoint saved' ((Lines 'run1-host' 'A5SAVE' | Where-Object { $_ -like '*saved*checkpoint=1101*' }).Count -ge 1) ((Lines 'run1-host' 'A5SAVE') -join ' | ')
Check 'run1: save file on disk' (Test-Path $saveFile) $saveFile
if (Test-Path $saveFile) {
    $json = Get-Content $saveFile -Raw | ConvertFrom-Json
    $states = ($json.worldObjects | Sort-Object id | ForEach-Object { "$($_.id):$($_.state)" }) -join ','
    Check 'run1: save holds lever and gate A on' ($states -like '1001:1,1101:1,*') $states
    Check 'run1: save is schema 1 with UTC time' ($json.schemaVersion -eq 1 -and $json.savedAtUtc -match 'Z$') "schema=$($json.schemaVersion) time=$($json.savedAtUtc)"
}

# 2. continue, with a client joining the continued session
$h = Start-Instance 'run2-host' 14 @('-a5-session', 'host', '-a5-continue')
Start-Sleep -Seconds 3
$c = Start-Instance 'run2-client' 9 @('-a5-session', 'join', '-a5-code', $code)
WaitAll @($h, $c) 40
Check 'run2: host loaded the save' ((Lines 'run2-host' 'A5SAVE' | Where-Object { $_ -like '*loaded*checkpoint=1101*' }).Count -ge 1) ((Lines 'run2-host' 'A5SAVE') -join ' | ')
$hw = FirstWorld 'run2-host'
Check 'run2: host world restored before play' ($hw -match 'states=1001:1,1101:1,2001:0,2101:0') "$hw"
$cw = FirstWorld 'run2-client'
Check 'run2: joining client sees restored world' ($cw -match 'states=1001:1,1101:1,2001:0,2101:0') "$cw"

# 3. unsupported save from a future build
$future = '{ "schemaVersion": 9, "saveId": "campaign", "levelId": 1 }'
Set-Content -Path $saveFile -Value $future -Encoding UTF8
$h = Start-Instance 'run3-host' 8 @('-a5-session', 'host', '-a5-continue')
WaitAll @($h) 30
Check 'run3: future schema refused with a clear error' ((Lines 'run3-host' 'A5SAVE' | Where-Object { $_ -like '*load-failed error=UnsupportedSchema*schema 9*' }).Count -ge 1) ((Lines 'run3-host' 'A5SAVE') -join ' | ')
$w3 = FirstWorld 'run3-host'
Check 'run3: host started the level fresh' ($w3 -match 'states=1001:0,1101:0') "$w3"
Check 'run3: refused save file left untouched' ((Get-Content $saveFile -Raw).Trim() -eq $future) ''

$failed = 0
foreach ($r in $results) {
    if ($r.Ok) { Write-Host "PASS  $($r.Name)  ($($r.Detail))" } else { Write-Host "FAIL  $($r.Name)  ($($r.Detail))"; $failed++ }
}
Write-Host ''
Write-Host "$($results.Count - $failed)/$($results.Count) checks passed"
exit $failed
