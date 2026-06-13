param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir = '',
  [string]$SampleDir = '',
  [switch]$ForceD3D12,
  [int]$TimeoutSeconds = 420
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $UnityExe)) { throw "Unity not found: $UnityExe" }
if (!(Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }

if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $ArtifactDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\real_splat_renderer_$stamp"
}

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
$env:QFL_ARTIFACT_DIR = $ArtifactDir
if (-not [string]::IsNullOrWhiteSpace($SampleDir)) {
  $env:QFL_REAL_SPLAT_SAMPLE_DIR = $SampleDir
}

$log = Join-Path $ArtifactDir 'unity_real_splat_renderer.log'
$args = @(
  '-batchmode',
  '-quit',
  '-projectPath', $ProjectPath,
  '-executeMethod', 'QuestFlightLab.Experimental.Splats.Editor.RealSplatRendererBatchRunner.Run',
  '-logFile', $log
)
if ($ForceD3D12) {
  $args = @('-force-d3d12') + $args
}

$process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
  Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
  throw "Unity real splat renderer spike exceeded ${TimeoutSeconds}s and was stopped. Log: $log"
}

$exitCode = [int]$process.ExitCode
if (Test-Path $log) {
  $logText = Get-Content -Raw -Path $log
  if ($logText -match 'Application will terminate with return code ([1-9][0-9]*)') {
    $exitCode = [int]$Matches[1]
  }
}

if ($exitCode -ne 0) {
  throw "Unity real splat renderer spike failed with exit code $exitCode. Log: $log"
}

Write-Host "Real splat renderer evidence: $ArtifactDir"
