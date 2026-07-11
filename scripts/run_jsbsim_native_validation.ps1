param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir = '',
  [int]$TimeoutSeconds = 360
)

$ErrorActionPreference = 'Stop'
if (!(Test-Path $UnityExe)) { throw "Unity not found: $UnityExe" }
if (!(Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }
if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $ArtifactDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\jsbsim_native_validation_$(Get-Date -Format yyyyMMdd_HHmmss)"
}
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
$env:QFL_JSBSIM_NATIVE_VALIDATION_DIR = [System.IO.Path]::GetFullPath($ArtifactDir)
$Log = Join-Path $ArtifactDir 'unity_jsbsim_native_validation.log'
$Lock = Join-Path $ProjectPath 'Temp\UnityLockfile'
if (Test-Path $Lock) {
  if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw "Unity is already running; refusing to remove the active project lock: $Lock"
  }
  Remove-Item -LiteralPath $Lock -Force
}
$Arguments = @(
  '-batchmode',
  '-quit',
  '-projectPath', $ProjectPath,
  '-executeMethod', 'QuestFlightLab.Editor.JSBSimNativeValidationRunner.Run',
  '-logFile', $Log
)
$Process = Start-Process -FilePath $UnityExe -ArgumentList $Arguments -PassThru -WindowStyle Hidden
if (!$Process.WaitForExit($TimeoutSeconds * 1000)) {
  Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
  throw "JSBSim native Unity validation exceeded ${TimeoutSeconds}s. Log: $Log"
}
$ExitCode = [int]$Process.ExitCode
if (Test-Path $Log) {
  $LogText = Get-Content -Raw $Log
  if ($LogText -match 'Application will terminate with return code ([1-9][0-9]*)') { $ExitCode = [int]$Matches[1] }
}
if ($ExitCode -ne 0) { throw "JSBSim native Unity validation failed with exit code $ExitCode. Log: $Log" }
$Report = Join-Path $ArtifactDir 'jsbsim_native_scenario_report.json'
if (!(Test-Path $Report)) { throw "Native validation did not write $Report. Log: $Log" }
Write-Host "JSBSim native validation: $ArtifactDir"
