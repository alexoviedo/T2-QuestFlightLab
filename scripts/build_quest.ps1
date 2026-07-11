param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [switch]$RegenerateScene,
  [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $UnityExe)) { throw "Unity not found: $UnityExe" }
if (!(Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }

$logDir = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Reset-UnityLock {
  $lock = Join-Path $ProjectPath 'Temp\UnityLockfile'
  if (Test-Path $lock) {
    if (Get-Process Unity -ErrorAction SilentlyContinue) {
      throw "Unity is already running; refusing to remove the active project lock: $lock"
    }
    Remove-Item -LiteralPath $lock -Force
  }
}

function Invoke-UnityMethod {
  param([string]$Method, [string]$LogName)
  Reset-UnityLock
  $log = Join-Path $logDir $LogName
  $args = @('-batchmode', '-quit', '-projectPath', $ProjectPath, '-executeMethod', $Method, '-logFile', $log)
  $process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru -WindowStyle Hidden
  if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw "Unity method exceeded ${TimeoutSeconds}s and was stopped: $Method. Log: $log"
  }
  $exitCode = [int]$process.ExitCode

  $logText = ''
  if (Test-Path $log) {
    $logText = Get-Content -Raw -Path $log
  }

  if ($logText -match 'No valid Unity Editor license found') {
    throw "Unity license is not active. Open Unity Hub, sign in/activate the editor, then rerun this script. Log: $log"
  }

  if ($logText -match 'DisplayDialog: Project folder or disk is read only') {
    throw "Unity could not write the project folder. Close any open Unity instance and rerun this script. Log: $log"
  }

  if ($logText -match 'Application will terminate with return code ([1-9][0-9]*)') {
    $exitCode = [int]$Matches[1]
  }

  if ($exitCode -ne 0) {
    throw "Unity method failed: $Method (exit $exitCode). Log: $log"
  }
}

Invoke-UnityMethod -Method 'QuestFlightLab.Editor.QuestProjectBootstrap.ConfigureProject' -LogName 'bootstrap_configure.log'

$scenePath = Join-Path $ProjectPath 'Assets\Scenes\InputLab.unity'
if ($RegenerateScene -or !(Test-Path $scenePath)) {
  Invoke-UnityMethod -Method 'QuestFlightLab.Editor.QuestProjectBootstrap.CreateInputLabScene' -LogName 'bootstrap_scene.log'
} else {
  Write-Host "Using existing scene: $scenePath"
  Write-Host 'Pass -RegenerateScene to rebuild the generated InputLab scene.'
}

Invoke-UnityMethod -Method 'QuestFlightLab.Editor.QuestProjectBootstrap.ValidateProject' -LogName 'validate_project.log'
Invoke-UnityMethod -Method 'QuestFlightLab.Editor.QuestBuild.PerformAndroidBuild' -LogName 'build_android.log'

Write-Host "APK built: $(Join-Path $ProjectPath 'Builds\Android\QuestFlightLab-v0.1-dev.apk')"
