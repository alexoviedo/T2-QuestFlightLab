param(
  [ValidateSet('mesh','playable_visual_baseline','playable_demo','visual_fidelity_demo','scenic_mesh_enhanced','scenic_splat_medium')]
  [string]$Mode = 'visual_fidelity_demo',
  [ValidateSet('','short_playtest')]
  [string]$DemoMode = '',
  [switch]$SplatDiagnostic,
  [switch]$SeatCalibration,
  [switch]$ResetSeatCalibration,
  [switch]$ManualHeadPose,
  [switch]$CaptureLogcat,
  [int]$DurationSeconds = 30,
  [string]$CockpitEyeZ = '',
  [string]$PilotViewOffsetX = '',
  [string]$PilotViewOffsetY = '',
  [string]$PilotViewOffsetZ = '',
  [string]$CockpitYawDeg = '',
  [string]$AdbExe = 'C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe',
  [string]$PackageId = 'com.alexoviedo.t2.questflightlab',
  [string]$Activity = 'com.unity3d.player.UnityPlayerGameActivity',
  [string]$OutputDir = ''
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $AdbExe)) { throw "ADB not found: $AdbExe" }

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $OutputDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quest_playtest_$stamp"
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

& $AdbExe shell svc power stayon usb | Out-Null
& $AdbExe shell input keyevent KEYCODE_WAKEUP | Out-Null
if ($CaptureLogcat) { & $AdbExe logcat -c }

if (($Mode -eq 'playable_demo' -or $Mode -eq 'visual_fidelity_demo') -and [string]::IsNullOrWhiteSpace($DemoMode)) {
  $DemoMode = 'short_playtest'
}

$component = "$PackageId/$Activity"
$launchArgs = @(
  'shell', 'am', 'start', '-S',
  '-n', $component,
  '--es', 'qfl_scenery_mode', $Mode,
  '--es', 'qfl_playtest_hud', 'true'
)

if (![string]::IsNullOrWhiteSpace($DemoMode)) {
  $launchArgs += @('--es', 'qfl_demo_mode', $DemoMode)
}

if ($SplatDiagnostic) {
  $launchArgs += @('--es', 'qfl_splat_diagnostic', 'true')
}

if ($SeatCalibration) {
  $launchArgs += @('--es', 'qfl_seat_calibration', 'true')
}

if ($ResetSeatCalibration) {
  $launchArgs += @('--es', 'qfl_reset_seat_calibration', 'true')
}

if ($ManualHeadPose) {
  $launchArgs += @('--es', 'qfl_manual_head_pose', 'true')
}

if (![string]::IsNullOrWhiteSpace($CockpitEyeZ)) {
  $launchArgs += @('--es', 'qfl_cockpit_eye_z', $CockpitEyeZ)
}

if (![string]::IsNullOrWhiteSpace($PilotViewOffsetX)) {
  $launchArgs += @('--es', 'qfl_pilot_view_offset_x', $PilotViewOffsetX)
}

if (![string]::IsNullOrWhiteSpace($PilotViewOffsetY)) {
  $launchArgs += @('--es', 'qfl_pilot_view_offset_y', $PilotViewOffsetY)
}

if (![string]::IsNullOrWhiteSpace($PilotViewOffsetZ)) {
  $launchArgs += @('--es', 'qfl_pilot_view_offset_z', $PilotViewOffsetZ)
}

if (![string]::IsNullOrWhiteSpace($CockpitYawDeg)) {
  $launchArgs += @('--es', 'qfl_cockpit_yaw_deg', $CockpitYawDeg)
}

$optionEntries = @(
  @{ key = 'qfl_scenery_mode'; value = $Mode },
  @{ key = 'qfl_playtest_hud'; value = 'true' }
)

if (![string]::IsNullOrWhiteSpace($DemoMode)) {
  $optionEntries += @{ key = 'qfl_demo_mode'; value = $DemoMode }
}

if ($SplatDiagnostic) {
  $optionEntries += @{ key = 'qfl_splat_diagnostic'; value = 'true' }
}

if ($SeatCalibration) {
  $optionEntries += @{ key = 'qfl_seat_calibration'; value = 'true' }
}

if ($ResetSeatCalibration) {
  $optionEntries += @{ key = 'qfl_reset_seat_calibration'; value = 'true' }
}

if ($ManualHeadPose) {
  $optionEntries += @{ key = 'qfl_manual_head_pose'; value = 'true' }
}

if (![string]::IsNullOrWhiteSpace($CockpitEyeZ)) {
  $optionEntries += @{ key = 'qfl_cockpit_eye_z'; value = $CockpitEyeZ }
}

if (![string]::IsNullOrWhiteSpace($PilotViewOffsetX)) {
  $optionEntries += @{ key = 'qfl_pilot_view_offset_x'; value = $PilotViewOffsetX }
}

if (![string]::IsNullOrWhiteSpace($PilotViewOffsetY)) {
  $optionEntries += @{ key = 'qfl_pilot_view_offset_y'; value = $PilotViewOffsetY }
}

if (![string]::IsNullOrWhiteSpace($PilotViewOffsetZ)) {
  $optionEntries += @{ key = 'qfl_pilot_view_offset_z'; value = $PilotViewOffsetZ }
}

if (![string]::IsNullOrWhiteSpace($CockpitYawDeg)) {
  $optionEntries += @{ key = 'qfl_cockpit_yaw_deg'; value = $CockpitYawDeg }
}

$launchOptionsPath = Join-Path $OutputDir 'launch_options.json'
@{ options = $optionEntries } | ConvertTo-Json -Depth 4 | Set-Content -Path $launchOptionsPath -Encoding UTF8
& $AdbExe shell "mkdir -p /sdcard/Android/data/$PackageId/files/QuestFlightLab" | Out-Null
if ($ResetSeatCalibration) {
  & $AdbExe shell "rm -f /sdcard/Android/data/$PackageId/files/QuestFlightLab/seat_calibration/seat_calibration_current.json" | Out-Null
}
& $AdbExe push $launchOptionsPath "/sdcard/Android/data/$PackageId/files/QuestFlightLab/launch_options.json" |
  Set-Content -Path (Join-Path $OutputDir 'adb_push_launch_options.txt') -Encoding UTF8

$launch = (& $AdbExe @launchArgs | Out-String)
Set-Content -Path (Join-Path $OutputDir 'app_launch.txt') -Value $launch -Encoding UTF8

$screenshotDelay = [Math]::Max(3, [Math]::Min(12, [Math]::Floor($DurationSeconds / 2)))
Start-Sleep -Seconds $screenshotDelay

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

$remaining = [Math]::Max(0, $DurationSeconds - $screenshotDelay)
if ($remaining -gt 0) {
  Start-Sleep -Seconds $remaining
}

if ($CaptureLogcat) {
  & $AdbExe logcat -d | Set-Content -Path (Join-Path $OutputDir 'logcat.txt') -Encoding UTF8
}
& $AdbExe shell dumpsys activity activities | Set-Content -Path (Join-Path $OutputDir 'activity.txt') -Encoding UTF8
& $AdbExe shell dumpsys power | Set-Content -Path (Join-Path $OutputDir 'power.txt') -Encoding UTF8
& $AdbExe shell dumpsys window windows | Set-Content -Path (Join-Path $OutputDir 'window.txt') -Encoding UTF8

$pullRoots = @(
  'scenery_runtime',
  'first_view_diagnostics',
  'demo_pilot',
  'seat_calibration'
)

foreach ($root in $pullRoots) {
  $deviceDir = "/sdcard/Android/data/$PackageId/files/QuestFlightLab/$root"
  $pullDir = Join-Path $OutputDir "pulled_$root"
  New-Item -ItemType Directory -Force -Path $pullDir | Out-Null
  & $AdbExe pull $deviceDir $pullDir | Set-Content -Path (Join-Path $OutputDir "adb_pull_$root.txt") -Encoding UTF8
}

Write-Host "Quest playtest launch evidence: $OutputDir"
