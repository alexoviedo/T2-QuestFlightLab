param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir = '',
  [int]$TimeoutSeconds = 420
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $ArtifactDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_runtime_samples_$stamp"
}

if (!(Test-Path $UnityExe)) { throw "Unity not found: $UnityExe" }
if (!(Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
$sampleDir = Join-Path $ArtifactDir 'synthetic_samples'
New-Item -ItemType Directory -Force -Path $sampleDir | Out-Null

python .\tools\generate_tiny_splat_samples.py --output-dir $sampleDir --counts 5000 50000 100000 --schema unity-3dgs-binary |
  Set-Content -Path (Join-Path $ArtifactDir 'sample_generation.log') -Encoding UTF8

$env:QFL_REAL_SPLAT_SAMPLE_DIR = $sampleDir
$log = Join-Path $ArtifactDir 'unity_build_runtime_splat_samples.log'
$args = @(
  '-batchmode',
  '-quit',
  '-projectPath', $ProjectPath,
  '-executeMethod', 'QuestFlightLab.Experimental.Splats.Editor.QuestSplatRuntimeAssetBuilder.BuildRuntimeSamples',
  '-logFile', $log
)

$process = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
  Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
  throw "Unity runtime splat sample build exceeded ${TimeoutSeconds}s and was stopped. Log: $log"
}

$exitCode = [int]$process.ExitCode
if (Test-Path $log) {
  $logText = Get-Content -Raw -Path $log
  if ($logText -match 'Application will terminate with return code ([1-9][0-9]*)') {
    $exitCode = [int]$Matches[1]
  }
}

if ($exitCode -ne 0) {
  throw "Unity runtime splat sample build failed with exit code $exitCode. Log: $log"
}

Get-ChildItem '.\QuestFlightLab\Assets\Resources\QuestFlightLab\Splats' -Recurse |
  Select-Object FullName,Length |
  ConvertTo-Json -Depth 3 |
  Set-Content -Path (Join-Path $ArtifactDir 'runtime_asset_manifest.json') -Encoding UTF8

Write-Host "Runtime splat sample assets prepared. Evidence: $ArtifactDir"
