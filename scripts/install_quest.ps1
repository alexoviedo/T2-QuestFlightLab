param(
  [string]$AdbExe = 'C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$PackageId = 'com.alexoviedo.t2.questflightlab'
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $AdbExe)) { throw "ADB not found: $AdbExe" }

$apk = Join-Path $ProjectPath 'Builds\Android\QuestFlightLab-v0.1-dev.apk'
if (!(Test-Path $apk)) { throw "APK not found: $apk. Run scripts\build_quest.ps1 first." }

& $AdbExe start-server | Out-Null
$devices = (& $AdbExe devices | Out-String)
Write-Host $devices

if ($devices -match '(?m)^\S+\s+unauthorized\s*$') {
  throw 'Put on the Quest 3 and approve the USB debugging/RSA prompt, then tell me when it is approved.'
}

if ($devices -notmatch '(?m)^\S+\s+device\s*$') {
  throw "No Quest device detected. Connect Quest 3 by USB-C, enable Developer Mode, approve USB debugging, then rerun install_quest.ps1."
}

& $AdbExe install -r $apk
if ($LASTEXITCODE -ne 0) { throw 'adb install failed.' }

$launches = @(
  "$PackageId/com.unity3d.player.UnityPlayerGameActivity",
  "$PackageId/com.unity3d.player.UnityPlayerActivity"
)

$launched = $false
foreach ($component in $launches) {
  $out = (& $AdbExe shell am start -n $component | Out-String)
  if ($LASTEXITCODE -eq 0 -and $out -notmatch '(?i)error|exception|unable|does not exist|no activities') {
    $launched = $true
    break
  }
  Write-Host $out.Trim()
}

if (-not $launched) {
  $out = (& $AdbExe shell am start -a android.intent.action.MAIN -c com.oculus.intent.category.VR -p $PackageId | Out-String)
  if ($LASTEXITCODE -eq 0 -and $out -notmatch '(?i)error|exception|unable|does not exist|no activities') {
    $launched = $true
  } else {
    Write-Host $out.Trim()
  }
}

if (-not $launched) {
  & $AdbExe shell monkey -p $PackageId -c android.intent.category.LAUNCHER 1
}

Write-Host 'Install/launch sequence complete.'

