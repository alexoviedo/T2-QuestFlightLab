[CmdletBinding()]
param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir = '',
  [ValidateRange(60, 3600)]
  [int]$TimeoutSeconds = 1200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $UnityExe)) { throw "Unity not found: $UnityExe" }
if (-not (Test-Path -LiteralPath $ProjectPath)) { throw "Project not found: $ProjectPath" }
if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $ArtifactDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_quest_build_$stamp"
}
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
$ArtifactDir = (Resolve-Path -LiteralPath $ArtifactDir).Path

$lock = Join-Path $ProjectPath 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lock) {
  if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw "Unity is already running; refusing to collide with the active project: $lock"
  }
  Remove-Item -LiteralPath $lock -Force
}

$log = Join-Path $ArtifactDir 'unity_build_production_quest.log'
$arguments = @(
  '-batchmode',
  '-quit',
  '-projectPath', $ProjectPath,
  '-executeMethod', 'QuestFlightLab.Editor.QuestBuild.PerformProductionAndroidBuild',
  '-logFile', $log
)
$process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
  Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
  throw "Production Quest build exceeded ${TimeoutSeconds}s and was stopped. Log: $log"
}

$exitCode = [int]$process.ExitCode
$logText = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
if ($logText -match 'Application will terminate with return code ([1-9][0-9]*)') {
  $exitCode = [int]$Matches[1]
}
if ($logText -match 'No valid Unity Editor license found') {
  throw "Unity license is not active. Log: $log"
}
if ($exitCode -ne 0) { throw "Production Quest build failed with exit code $exitCode. Log: $log" }

$apk = Join-Path $ProjectPath 'Builds\Android\QuestFlightLab-production-v2.apk'
if (-not (Test-Path -LiteralPath $apk)) { throw "Unity exited without the production APK: $apk" }
$hash = (Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash.ToLowerInvariant()
[ordered]@{
  generatedUtc = [DateTime]::UtcNow.ToString('O')
  scene = 'Assets/Scenes/ProductionVerticalSlice.unity'
  apk = $apk
  bytes = (Get-Item -LiteralPath $apk).Length
  sha256 = $hash
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ArtifactDir 'production_apk_manifest.json') -Encoding UTF8

Write-Host "Production APK: $apk"
Write-Host "SHA256: $hash"
Write-Host "Build evidence: $ArtifactDir"
