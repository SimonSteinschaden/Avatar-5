<#
.SYNOPSIS
  Runs every automated Avatar5 check and writes a machine-readable report (P08).

.DESCRIPTION
  1. Build: regenerates generated scenes and builds all test players + the playable Windows build.
  2. EditMode and PlayMode tests (NUnit XML).
  3. Multi-process tests (sessions, network players, combat, enemy, Level01, save/continue).
  Report: Artifacts/TestReport/report.json and report.md (+ copies of all logs/XML).

  The Unity editor must NOT have this project open (batch mode needs the project lock). Run from a copy
  or close the editor first.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts/Run-AllTests.ps1
  powershell -ExecutionPolicy Bypass -File scripts/Run-AllTests.ps1 -SkipMultiprocess
#>
param(
    [string] $UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe',
    [string] $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch] $SkipBuild,
    [switch] $SkipMultiprocess
)
$ErrorActionPreference = 'Stop'
$reportDir = Join-Path $ProjectPath 'Artifacts\TestReport'
if (Test-Path $reportDir) { Remove-Item $reportDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null

if (Test-Path (Join-Path $ProjectPath 'Temp\UnityLockfile')) {
    $lock = Join-Path $ProjectPath 'Temp\UnityLockfile'
    try { [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None').Close() }
    catch { throw "The project is open in the Unity editor ($ProjectPath). Close it or run this script on a copy." }
}
if (-not (Test-Path $UnityPath)) { throw "Unity not found: $UnityPath" }

$steps = New-Object System.Collections.Generic.List[object]
function Add-Step([string] $name, [string] $status, [string] $summary, [double] $seconds, [string] $log) {
    $steps.Add([ordered]@{ name = $name; status = $status; summary = $summary; seconds = [math]::Round($seconds, 1); log = $log })
    $color = if ($status -eq 'passed') { 'Green' } elseif ($status -eq 'skipped') { 'Yellow' } else { 'Red' }
    Write-Host ("{0,-28} {1,-8} {2}" -f $name, $status.ToUpper(), $summary) -ForegroundColor $color
}

function Invoke-Unity([string] $name, [string[]] $arguments) {
    $log = Join-Path $reportDir "$name.log"
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $UnityPath -ArgumentList (@('-batchmode', '-projectPath', "`"$ProjectPath`"", '-logFile', "`"$log`"") + $arguments) -PassThru -Wait -WindowStyle Hidden
    $watch.Stop()
    return @{ Exit = $p.ExitCode; Seconds = $watch.Elapsed.TotalSeconds; Log = $log }
}

function Read-NUnit([string] $xml) {
    if (-not (Test-Path $xml)) { return $null }
    [xml] $doc = Get-Content $xml
    $run = $doc.'test-run'
    return @{ Total = [int]$run.total; Passed = [int]$run.passed; Failed = [int]$run.failed; Skipped = [int]$run.skipped; Result = $run.result }
}

# ---- 1. build ------------------------------------------------------------------------------
if ($SkipBuild) { Add-Step 'build' 'skipped' 'requested' 0 '' }
else {
    $r = Invoke-Unity 'build' @('-quit', '-executeMethod', 'Avatar5.EditorTools.Avatar5BuildAll.BuildFromCommandLine')
    $errors = @(Select-String -Path $r.Log -Pattern 'error CS\d+' -ErrorAction SilentlyContinue).Count
    $status = if ($r.Exit -eq 0 -and $errors -eq 0) { 'passed' } else { 'failed' }
    Add-Step 'build' $status "exit=$($r.Exit), compiler errors=$errors" $r.Seconds $r.Log
    if ($status -ne 'passed') { $SkipMultiprocess = $true }
}

# ---- 2. unit/integration -------------------------------------------------------------------
foreach ($platform in 'EditMode', 'PlayMode') {
    $xml = Join-Path $reportDir "$platform.xml"
    $r = Invoke-Unity $platform @('-runTests', '-testPlatform', $platform, '-testResults', "`"$xml`"")
    $n = Read-NUnit $xml
    if ($null -eq $n) { Add-Step $platform 'failed' "no results (exit=$($r.Exit))" $r.Seconds $r.Log; continue }
    $status = if ($n.Failed -eq 0 -and $r.Exit -eq 0) { 'passed' } else { 'failed' }
    Add-Step $platform $status "$($n.Passed)/$($n.Total) passed, $($n.Failed) failed, $($n.Skipped) skipped" $r.Seconds $xml
}

# ---- 3. multi-process ----------------------------------------------------------------------
$scripts = @('P03-SessionTest', 'P03-NetworkPlayerTest', 'P02-CombatTest', 'P04-EnemyTest', 'P05-Level01Test', 'P06-SaveTest')
foreach ($s in $scripts) {
    if ($SkipMultiprocess) { Add-Step $s 'skipped' 'build missing or -SkipMultiprocess' 0 ''; continue }
    $out = Join-Path $reportDir "$s.txt"
    $watch = [Diagnostics.Stopwatch]::StartNew()
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "$s.ps1") *> $out
    $exit = $LASTEXITCODE
    $watch.Stop()
    $summary = (Select-String -Path $out -Pattern '(\d+)/(\d+) checks passed' | Select-Object -Last 1).Line
    if (-not $summary) { $summary = "exit=$exit (no summary)" }
    Add-Step $s $(if ($exit -eq 0) { 'passed' } else { 'failed' }) $summary $watch.Elapsed.TotalSeconds $out
}

# ---- report --------------------------------------------------------------------------------
$commit = (& git -C $ProjectPath rev-parse --short HEAD 2>$null)
$branch = (& git -C $ProjectPath rev-parse --abbrev-ref HEAD 2>$null)
$unityVersion = (Get-Content (Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt') | Select-Object -First 1) -replace 'm_EditorVersion: ', ''
$failedCount = @($steps | Where-Object { $_.status -eq 'failed' }).Count
$report = [ordered]@{
    generatedUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    commit = $commit; branch = $branch; unity = $unityVersion; machine = $env:COMPUTERNAME
    result = $(if ($failedCount -eq 0) { 'passed' } else { 'failed' })
    steps = $steps
}
$report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $reportDir 'report.json') -Encoding UTF8

$md = @("# Avatar5 - automatischer Testlauf", "", "Commit ``$commit`` ($branch), Unity $unityVersion, $($report.generatedUtc)", "",
    "**Ergebnis: $($report.result.ToUpper())**", "", "| Schritt | Status | Ergebnis | Dauer |", "|---|---|---|---|")
foreach ($st in $steps) { $md += "| $($st.name) | $($st.status) | $($st.summary) | $($st.seconds) s |" }
$md += ""
$md += "Nicht automatisiert: Relay ueber zwei Internetanschluesse, Latenz-/Verlustsimulation, manueller Spieltest, Performance auf Referenz-GPU."
$md | Set-Content (Join-Path $reportDir 'report.md') -Encoding UTF8

Write-Host ''
Write-Host "Report: $reportDir\report.md"
exit $failedCount
