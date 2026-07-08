param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir,
  [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $UnityExe)) { throw "Unity not found: $UnityExe" }
if (!(Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }
if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $ArtifactDir = Join-Path (Split-Path -Parent $ProjectPath) "..\T2-QuestFlightLab-setup-artifacts\jsbsim_editor_bridge_$(Get-Date -Format yyyyMMdd_HHmmss)"
  $ArtifactDir = [System.IO.Path]::GetFullPath($ArtifactDir)
}

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
$env:QFL_JSBSIM_BRIDGE_DIR = $ArtifactDir

$log = Join-Path $ArtifactDir 'unity_jsbsim_editor_bridge.log'
$lock = Join-Path $ProjectPath 'Temp\UnityLockfile'
if (Test-Path $lock) { Remove-Item -LiteralPath $lock -Force -ErrorAction SilentlyContinue }

$args = @(
  '-batchmode',
  '-quit',
  '-projectPath', $ProjectPath,
  '-executeMethod', 'QuestFlightLab.Editor.JSBSimEditorBridgeRunner.RunBridge',
  '-logFile', $log
)

$process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
  Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
  throw "Unity JSBSim editor bridge exceeded ${TimeoutSeconds}s and was stopped. Log: $log"
}

$exitCode = [int]$process.ExitCode
$logText = ''
if (Test-Path $log) {
  $logText = Get-Content -Raw -Path $log
}
if ($logText -match 'Application will terminate with return code ([1-9][0-9]*)') {
  $exitCode = [int]$Matches[1]
}
if ($exitCode -ne 0) {
  throw "Unity JSBSim editor bridge failed with exit code $exitCode. Log: $log"
}

Write-Host "JSBSim editor bridge artifacts: $ArtifactDir"
