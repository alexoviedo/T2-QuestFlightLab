param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir = '',
  [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $UnityExe)) { throw "Unity not found: $UnityExe" }
if (!(Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }

if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $ArtifactDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\flight_core_$stamp"
}

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
$env:QFL_ARTIFACT_DIR = $ArtifactDir

$log = Join-Path $ArtifactDir 'unity_editor_scenario_tests.log'
$args = @(
  '-batchmode',
  '-quit',
  '-projectPath', $ProjectPath,
  '-executeMethod', 'QuestFlightLab.Editor.FlightCoreBatchRunner.RunDefaultScenarios',
  '-logFile', $log
)

$process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
  Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
  throw "Unity editor scenario tests exceeded ${TimeoutSeconds}s and were stopped. Log: $log"
}
$exitCode = [int]$process.ExitCode

if (Test-Path $log) {
  $logText = Get-Content -Raw -Path $log
  if ($logText -match 'Application will terminate with return code ([1-9][0-9]*)') {
    $exitCode = [int]$Matches[1]
  }
}

if ($exitCode -ne 0) {
  throw "Unity editor scenario tests failed with exit code $exitCode. Log: $log"
}

Write-Host "Scenario evidence: $ArtifactDir"
