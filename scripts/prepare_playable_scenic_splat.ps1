param(
  [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir = '',
  [int]$TimeoutSeconds = 480
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $ArtifactDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\playable_scenic_splat_$stamp"
}

if (!(Test-Path $UnityExe)) { throw "Unity not found: $UnityExe" }
if (!(Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
$scenicDir = Join-Path $ArtifactDir 'procedural_scenic_samples'
New-Item -ItemType Directory -Force -Path $scenicDir | Out-Null

python .\tools\generate_scenic_splat_patch.py --output-dir $scenicDir |
  Set-Content -Path (Join-Path $ArtifactDir 'scenic_sample_generation.log') -Encoding UTF8

$env:QFL_SCENIC_SPLAT_SAMPLE_DIR = $scenicDir
$log = Join-Path $ArtifactDir 'unity_import_scenic_splat_samples.log'
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
  throw "Unity scenic splat sample import exceeded ${TimeoutSeconds}s and was stopped. Log: $log"
}

$exitCode = [int]$process.ExitCode
if (Test-Path $log) {
  $logText = Get-Content -Raw -Path $log
  if ($logText -match 'Application will terminate with return code ([1-9][0-9]*)') {
    $exitCode = [int]$Matches[1]
  }
}

if ($exitCode -ne 0) {
  throw "Unity scenic splat sample import failed with exit code $exitCode. Log: $log"
}

Get-ChildItem '.\QuestFlightLab\Assets\Resources\QuestFlightLab\Splats' -Recurse |
  Select-Object FullName,Length |
  ConvertTo-Json -Depth 3 |
  Set-Content -Path (Join-Path $ArtifactDir 'runtime_asset_manifest.json') -Encoding UTF8

Write-Host "Playable scenic splat assets prepared. Evidence: $ArtifactDir"
