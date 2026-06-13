param(
  [ValidateSet('mesh','splat_5k','splat_50k','splat_100k')]
  [string]$Mode = 'mesh',
  [string]$AdbExe = 'C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe',
  [string]$PackageId = 'com.alexoviedo.t2.questflightlab',
  [string]$Activity = 'com.unity3d.player.UnityPlayerGameActivity',
  [string]$OutputDir = '',
  [int]$CaptureSeconds = 20,
  [int]$ScreenshotDelaySeconds = 12
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $AdbExe)) { throw "ADB not found: $AdbExe" }

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $OutputDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_runtime_manual_$stamp\$Mode"
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

& $AdbExe start-server | Out-Null
$devices = (& $AdbExe devices -l | Out-String)
Set-Content -Path (Join-Path $OutputDir 'adb_devices.txt') -Value $devices -Encoding UTF8
if ($devices -match '(?m)^\S+\s+unauthorized\b') {
  throw 'Put on the Quest 3 and approve the USB debugging prompt. If available, check "Always allow from this computer." Then tell me when done.'
}
if ($devices -notmatch '(?m)^\S+\s+device\b') {
  throw 'No Quest ADB device is available.'
}

& $AdbExe logcat -c

$component = "$PackageId/$Activity"
$launch = (& $AdbExe shell am start -S -n $component --es qfl_scenery_mode $Mode | Out-String)
Set-Content -Path (Join-Path $OutputDir 'app_launch.txt') -Value $launch -Encoding UTF8

$earlyCaptureDelay = [Math]::Max(1, [Math]::Min($CaptureSeconds, $ScreenshotDelaySeconds))
Start-Sleep -Seconds $earlyCaptureDelay

$screenshot = Join-Path $OutputDir 'adb_screenshot.png'
$capture = New-Object System.Diagnostics.ProcessStartInfo
$capture.FileName = $AdbExe
$capture.Arguments = 'exec-out screencap -p'
$capture.UseShellExecute = $false
$capture.RedirectStandardOutput = $true
$capture.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::Start($capture)
$fileStream = [System.IO.File]::Create($screenshot)
try {
  $process.StandardOutput.BaseStream.CopyTo($fileStream)
}
finally {
  $fileStream.Dispose()
}
$process.WaitForExit()
$screencapError = $process.StandardError.ReadToEnd()
if ($process.ExitCode -ne 0 -or (Test-Path $screenshot) -eq $false -or (Get-Item $screenshot).Length -eq 0) {
  Set-Content -Path (Join-Path $OutputDir 'adb_screenshot_error.txt') -Value $screencapError -Encoding UTF8
}

$remainingCaptureSeconds = [Math]::Max(0, $CaptureSeconds - $earlyCaptureDelay)
if ($remainingCaptureSeconds -gt 0) {
  Start-Sleep -Seconds $remainingCaptureSeconds
}

& $AdbExe logcat -d | Set-Content -Path (Join-Path $OutputDir 'logcat.txt') -Encoding UTF8
& $AdbExe shell dumpsys activity activities | Set-Content -Path (Join-Path $OutputDir 'activity.txt') -Encoding UTF8

$deviceEvidenceDir = "/sdcard/Android/data/$PackageId/files/QuestFlightLab/scenery_runtime"
$pullDir = Join-Path $OutputDir 'pulled_scenery_runtime'
New-Item -ItemType Directory -Force -Path $pullDir | Out-Null
& $AdbExe pull $deviceEvidenceDir $pullDir | Set-Content -Path (Join-Path $OutputDir 'adb_pull_evidence.txt') -Encoding UTF8

Write-Host "Quest splat runtime mode evidence: $OutputDir"
