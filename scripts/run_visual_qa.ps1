param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir = '',
  [int]$TimeoutSeconds = 360,
  [int]$Width = 1280,
  [int]$Height = 720,
  [switch]$SkipPythonAnalysis
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $UnityExe)) { throw "Unity not found: $UnityExe" }
if (!(Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }

if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $ArtifactDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\visual_qa_$stamp"
}

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
$env:QFL_VISUAL_QA_DIR = $ArtifactDir
$env:QFL_VISUAL_QA_WIDTH = "$Width"
$env:QFL_VISUAL_QA_HEIGHT = "$Height"

$log = Join-Path $ArtifactDir 'unity_visual_qa.log'
$args = @(
  '-batchmode',
  '-quit',
  '-projectPath', $ProjectPath,
  '-executeMethod', 'QuestFlightLab.Editor.VisualQaBatchRunner.RunVisualQa',
  '-logFile', $log
)

$process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
  Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
  throw "Unity visual QA exceeded ${TimeoutSeconds}s and was stopped. Log: $log"
}

$exitCode = [int]$process.ExitCode
if (Test-Path $log) {
  $logText = Get-Content -Raw -Path $log
  if ($logText -match 'Application will terminate with return code ([1-9][0-9]*)') {
    $exitCode = [int]$Matches[1]
  }
}

if ($exitCode -ne 0) {
  throw "Unity visual QA failed with exit code $exitCode. Log: $log"
}

$report = Join-Path $ArtifactDir 'visual_qa_report.json'
$summary = Join-Path $ArtifactDir 'visual_qa_summary.md'
if (!(Test-Path $report)) {
  throw "Unity visual QA exited successfully but did not produce $report. Log: $log"
}

if (-not $SkipPythonAnalysis) {
  $analyzer = Join-Path (Split-Path $PSScriptRoot -Parent) 'tools\visual_qa_analyze.py'
  if (Test-Path $analyzer) {
    python $analyzer --input $ArtifactDir --fail-on-errors
    if ($LASTEXITCODE -ne 0) {
      throw "Python visual QA analysis failed with exit code $LASTEXITCODE. ArtifactDir: $ArtifactDir"
    }
  }
}

Write-Host "Visual QA artifacts: $ArtifactDir"
Write-Host "Visual QA summary: $summary"
