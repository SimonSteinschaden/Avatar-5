<#
.SYNOPSIS
  Multi-process session test for P03 (direct UDP on this PC). Starts one host and several client
  processes of the P03 session test player and checks the "A5SESSION" log lines.

.DESCRIPTION
  Build the player first: Avatar5 > Build > P03 Session Test Player
  (or -executeMethod Avatar5.EditorTools.Avatar5P03SessionSceneBuilder.BuildPlayerFromCommandLine).

  Scenario on one timeline:
    host                 creates a private session, quits after HostSeconds
    client1..3           join -> session is 4/4 (client1 leaves after a while -> 3/4)
    client_full          joins while 4/4 -> SessionFull
    client_version       joins with another protocol version -> VersionMismatch
    client2/3            see HostEnded when the host quits

.EXAMPLE
  powershell -File scripts/P03-SessionTest.ps1 -Player Artifacts/P03/Avatar5_P03.exe
#>
param(
    [string] $Player = (Join-Path $PSScriptRoot '..\Artifacts\P03\Avatar5_P03.exe'),
    [string] $LogDir = (Join-Path $PSScriptRoot '..\Artifacts\P03\logs'),
    [int] $Port = 7781,
    [int] $HostSeconds = 30
)
$ErrorActionPreference = 'Stop'
$Player = (Resolve-Path $Player).Path
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
Get-ChildItem $LogDir -Filter '*.log' -ErrorAction SilentlyContinue | Remove-Item -Force

$code = "127.0.0.1:$Port"
$processes = @{}

function Start-Instance([string] $name, [string[]] $extra) {
    $log = Join-Path $LogDir "$name.log"
    $args = @('-batchmode', '-nographics', '-logFile', $log, '-a5-mode', 'direct', '-a5-port', "$Port") + $extra
    $script:processes[$name] = Start-Process -FilePath $Player -ArgumentList $args -PassThru -WindowStyle Hidden
}

Start-Instance 'host' @('-a5-session', 'host', '-a5-quit-after', "$HostSeconds")
Start-Sleep -Seconds 4
Start-Instance 'client1' @('-a5-session', 'join', '-a5-code', $code, '-a5-leave-after', '12', '-a5-quit-after', "$($HostSeconds + 40)")
Start-Instance 'client2' @('-a5-session', 'join', '-a5-code', $code, '-a5-quit-after', "$($HostSeconds + 40)")
Start-Instance 'client3' @('-a5-session', 'join', '-a5-code', $code, '-a5-quit-after', "$($HostSeconds + 40)")
Start-Sleep -Seconds 6
Start-Instance 'client_full' @('-a5-session', 'join', '-a5-code', $code, '-a5-quit-after', '20')
Start-Instance 'client_version' @('-a5-session', 'join', '-a5-code', $code, '-a5-protocol', '99', '-a5-quit-after', '20')

$deadline = (Get-Date).AddSeconds($HostSeconds + 60)
while ((Get-Date) -lt $deadline -and ($processes.Values | Where-Object { -not $_.HasExited })) { Start-Sleep -Milliseconds 500 }
$processes.Values | Where-Object { -not $_.HasExited } | ForEach-Object { $_.Kill() }

function Lines([string] $name) {
    $log = Join-Path $LogDir "$name.log"
    if (-not (Test-Path $log)) { return @() }
    return @(Get-Content $log | Where-Object { $_ -like 'A5SESSION*' })
}

$checks = @(
    @{ Name = 'host created session';              Who = 'host';           Pattern = 'result=ok' },
    @{ Name = 'host reached 4/4';                  Who = 'host';           Pattern = 'phase=Connected role=Host players=4/4' },
    @{ Name = 'host back to 3/4 after leave';      Who = 'host';           Pattern = 'phase=Connected role=Host players=3/4' },
    @{ Name = 'client1 joined';                    Who = 'client1';        Pattern = 'result=ok' },
    @{ Name = 'client1 left cleanly';              Who = 'client1';        Pattern = 'phase=Offline role=None players=0/0 code=- error=None' },
    @{ Name = 'client2 sees 4 players';            Who = 'client2';        Pattern = 'role=Client players=4/4' },
    @{ Name = 'client3 joined';                    Who = 'client3';        Pattern = 'result=ok' },
    @{ Name = 'fifth player rejected (full)';      Who = 'client_full';    Pattern = 'result=fail error=SessionFull' },
    @{ Name = 'other version rejected';            Who = 'client_version'; Pattern = 'result=fail error=VersionMismatch' },
    @{ Name = 'client2 told host ended';           Who = 'client2';        Pattern = 'phase=Offline role=None players=0/0 code=- error=HostEnded' },
    @{ Name = 'client3 told host ended';           Who = 'client3';        Pattern = 'phase=Offline role=None players=0/0 code=- error=HostEnded' }
)

$failed = 0
foreach ($check in $checks) {
    $hit = Lines $check.Who | Where-Object { $_ -like "*$($check.Pattern)*" } | Select-Object -First 1
    if ($hit) { Write-Host "PASS  $($check.Name)" } else { Write-Host "FAIL  $($check.Name)  (expected '$($check.Pattern)' in $($check.Who).log)"; $failed++ }
}
Write-Host ''
foreach ($name in $processes.Keys | Sort-Object) {
    Write-Host "--- $name"
    Lines $name | ForEach-Object { Write-Host "    $_" }
}
Write-Host ''
Write-Host "$($checks.Count - $failed)/$($checks.Count) checks passed"
exit $failed
