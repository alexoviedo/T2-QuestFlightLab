param(
  [string]$ApkPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab\Builds\Android\QuestFlightLab-v0.1-dev.apk',
  [string]$ReportPath = ''
)

$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $ApkPath)) { throw "APK not found: $ApkPath" }
$ApkPath = [System.IO.Path]::GetFullPath($ApkPath)
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
  $ReportPath = [System.IO.Path]::ChangeExtension($ApkPath, '.jsbsim-inclusion.json')
}
$ReportPath = [System.IO.Path]::GetFullPath($ReportPath)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($ApkPath)
try {
  $entries = @{}
  foreach ($entry in $archive.Entries) { $entries[$entry.FullName] = $entry }

  $repoRoot = Split-Path $PSScriptRoot -Parent
  $streamingSource = Join-Path $repoRoot 'QuestFlightLab\Assets\StreamingAssets\JSBSim-1.3.1'
  $streamingRoot = if ($entries.ContainsKey('assets/JSBSim-1.3.1/runtime_manifest.txt')) {
    'assets/JSBSim-1.3.1'
  } else {
    'assets/bin/Data/StreamingAssets/JSBSim-1.3.1'
  }
  $runtimeFiles = @('runtime_manifest.txt') + @(Get-Content -LiteralPath (Join-Path $streamingSource 'runtime_manifest.txt') | Where-Object { ![string]::IsNullOrWhiteSpace($_) })
  $required = @(
    'lib/arm64-v8a/libJSBSim.so',
    'lib/arm64-v8a/libqfl_jsbsim_native.so',
    'lib/arm64-v8a/libc++_shared.so'
  ) + @($runtimeFiles | ForEach-Object { "$streamingRoot/$($_.Replace('\', '/'))" })
  $forbidden = @(
    'lib/armeabi-v7a/libJSBSim.so',
    'lib/x86/libJSBSim.so',
    'lib/x86_64/libJSBSim.so',
    'assets/bin/Data/StreamingAssets/JSBSim-1.3.1/JSBSim.dll',
    'assets/bin/Data/StreamingAssets/JSBSim-1.3.1/qfl_jsbsim_native.dll',
    'assets/JSBSim-1.3.1/JSBSim.dll',
    'assets/JSBSim-1.3.1/qfl_jsbsim_native.dll'
  )

  $missing = @($required | Where-Object { !$entries.ContainsKey($_) })
  $unexpected = @($forbidden | Where-Object { $entries.ContainsKey($_) })
  $included = @()
  $hashMismatches = @()
  foreach ($name in $required) {
    if (!$entries.ContainsKey($name)) { continue }
    $entry = $entries[$name]
    $stream = $entry.Open()
    try {
      $sha = [System.Security.Cryptography.SHA256]::Create()
      try { $digest = [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
      finally { $sha.Dispose() }
    }
    finally { $stream.Dispose() }
    $sourcePath = $null
    if ($name -eq 'lib/arm64-v8a/libJSBSim.so') {
      $sourcePath = Join-Path $repoRoot 'QuestFlightLab\Assets\Plugins\Android\libs\arm64-v8a\libJSBSim.so'
    } elseif ($name -eq 'lib/arm64-v8a/libqfl_jsbsim_native.so') {
      $sourcePath = Join-Path $repoRoot 'QuestFlightLab\Assets\Plugins\Android\libs\arm64-v8a\libqfl_jsbsim_native.so'
    } elseif ($name.StartsWith("$streamingRoot/")) {
      $relative = $name.Substring($streamingRoot.Length + 1).Replace('/', '\')
      $sourcePath = Join-Path $streamingSource $relative
    }
    $sourceDigest = if ($sourcePath -and (Test-Path -LiteralPath $sourcePath)) {
      (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    } else { '' }
    $matchesSource = [string]::IsNullOrWhiteSpace($sourceDigest) -or $sourceDigest -eq $digest
    if (!$matchesSource) { $hashMismatches += $name }

    $included += [ordered]@{
      path = $name
      uncompressed_bytes = $entry.Length
      compressed_bytes = $entry.CompressedLength
      sha256 = $digest
      source_sha256 = $sourceDigest
      matches_staged_source = $matchesSource
    }
  }

  $apkHash = (Get-FileHash -LiteralPath $ApkPath -Algorithm SHA256).Hash.ToLowerInvariant()
  $report = [ordered]@{
    generated_utc = [DateTime]::UtcNow.ToString('O')
    status = if ($missing.Count -eq 0 -and $unexpected.Count -eq 0 -and $hashMismatches.Count -eq 0) { 'PASS' } else { 'FAIL' }
    apk_path = $ApkPath
    apk_bytes = (Get-Item -LiteralPath $ApkPath).Length
    apk_sha256 = $apkHash
    required_entries = $required
    streaming_assets_layout = $streamingRoot
    included_entries = $included
    missing_entries = $missing
    staged_source_hash_mismatches = $hashMismatches
    unexpected_architecture_or_windows_entries = $unexpected
  }
  $directory = Split-Path -Parent $ReportPath
  if (![string]::IsNullOrWhiteSpace($directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
  $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding utf8
  if ($report.status -ne 'PASS') {
    throw "JSBSim APK inclusion failed. Missing=$($missing -join ', '); unexpected=$($unexpected -join ', '); hashMismatch=$($hashMismatches -join ', '). Report: $ReportPath"
  }
  Write-Host "JSBSim APK inclusion PASS: $ReportPath"
}
finally {
  $archive.Dispose()
}
