param(
  [string]$ArtifactDir = '',
  [int]$Width = 1280,
  [int]$Height = 720,
  [int]$TimeoutSeconds = 360
)

$ErrorActionPreference = 'Stop'

$runner = Join-Path $PSScriptRoot 'run_visual_qa.ps1'
& $runner -ArtifactDir $ArtifactDir -Width $Width -Height $Height -TimeoutSeconds $TimeoutSeconds
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}
