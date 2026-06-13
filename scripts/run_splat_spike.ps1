param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir = '',
  [string]$SampleDir = '',
  [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $UnityExe)) { throw "Unity not found: $UnityExe" }
if (!(Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }

if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $ArtifactDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_spike_$stamp"
}

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
$env:QFL_ARTIFACT_DIR = $ArtifactDir
if (-not [string]::IsNullOrWhiteSpace($SampleDir)) {
  $env:QFL_SPLAT_SAMPLE_DIR = $SampleDir
}

$log = Join-Path $ArtifactDir 'unity_splat_spike.log'
$args = @(
  '-batchmode',
  '-quit',
  '-projectPath', $ProjectPath,
  '-executeMethod', 'QuestFlightLab.Editor.SplatSpikeBatchRunner.RunSplatSpike',
  '-logFile', $log
)

$process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
  Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
  throw "Unity splat spike exceeded ${TimeoutSeconds}s and was stopped. Log: $log"
}

$exitCode = [int]$process.ExitCode
if (Test-Path $log) {
  $logText = Get-Content -Raw -Path $log
  if ($logText -match 'Application will terminate with return code ([1-9][0-9]*)') {
    $exitCode = [int]$Matches[1]
  }
}

if ($exitCode -ne 0) {
  throw "Unity splat spike failed with exit code $exitCode. Log: $log"
}

Write-Host "Splat spike evidence: $ArtifactDir"
