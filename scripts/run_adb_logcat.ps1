param(
  [string]$AdbExe = 'C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe',
  [string]$OutDir = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts'
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $AdbExe)) { throw "ADB not found: $AdbExe" }

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$dir = Join-Path $OutDir "logcat_$stamp"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$log = Join-Path $dir 'questflightlab_logcat.txt'

Write-Host "Writing logcat to $log. Press Ctrl+C to stop."
& $AdbExe logcat -v time Unity QuestFlightLab InputSystem '*:S' | Tee-Object -FilePath $log

