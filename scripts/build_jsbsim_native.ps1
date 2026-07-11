param(
  [string]$UnityRoot = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1',
  [string]$ProjectPath = 'C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab',
  [string]$ArtifactDir = '',
  [switch]$SkipWindows,
  [switch]$SkipAndroid,
  [switch]$SkipUnityStaging
)

$ErrorActionPreference = 'Stop'
$Revision = '3b25f25e49b42d0489c04ac805674fc1450ca579'
$Version = '1.3.1'
$Repository = 'https://github.com/JSBSim-Team/jsbsim.git'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$BridgeSource = Join-Path $RepoRoot 'native\jsbsim_bridge'

if ([string]::IsNullOrWhiteSpace($ArtifactDir)) {
  $ArtifactDir = "C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\jsbsim_native_$(Get-Date -Format yyyyMMdd_HHmmss)"
}
$ArtifactDir = [System.IO.Path]::GetFullPath($ArtifactDir)
$SourceDir = Join-Path $ArtifactDir 'jsbsim-src'
$DataRoot = Join-Path $ProjectPath "Assets\StreamingAssets\JSBSim-$Version"
$CMake = Join-Path $UnityRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\cmake.exe'
$Ninja = Join-Path $UnityRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\ninja.exe'
$Ndk = Join-Path $UnityRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\NDK'
$Toolchain = Join-Path $Ndk 'build\cmake\android.toolchain.cmake'
$ReadElf = Join-Path $Ndk 'toolchains\llvm\prebuilt\windows-x86_64\bin\llvm-readelf.exe'
$Strip = Join-Path $Ndk 'toolchains\llvm\prebuilt\windows-x86_64\bin\llvm-strip.exe'

foreach ($required in @($BridgeSource, $ProjectPath, $CMake, $Ninja, $Toolchain, $ReadElf, $Strip)) {
  if (!(Test-Path $required)) { throw "Required native-build input not found: $required" }
}

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
if (!(Test-Path (Join-Path $SourceDir '.git'))) {
  git clone --filter=blob:none --no-checkout $Repository $SourceDir
}
git -C $SourceDir fetch --depth 1 origin $Revision
git -C $SourceDir checkout --detach $Revision
$ActualRevision = (git -C $SourceDir rev-parse HEAD).Trim()
if ($ActualRevision -ne $Revision) { throw "JSBSim revision mismatch: expected $Revision, got $ActualRevision" }
if ((git -C $SourceDir status --porcelain).Count -ne 0) { throw 'Pinned JSBSim source checkout is unexpectedly dirty.' }

function Copy-JSBSimRuntimeData {
  New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null
  $AircraftDestination = Join-Path $DataRoot 'aircraft\c172x'
  $EngineDestination = Join-Path $DataRoot 'engine'
  $SystemsDestination = Join-Path $DataRoot 'systems'
  New-Item -ItemType Directory -Force -Path $AircraftDestination,$EngineDestination,$SystemsDestination | Out-Null

  Copy-Item -LiteralPath (Join-Path $SourceDir 'aircraft\c172x\c172x.xml') -Destination $AircraftDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'aircraft\c172x\c172ap.xml') -Destination $AircraftDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'aircraft\c172x\elevator_doublet_init.xml') -Destination $AircraftDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'aircraft\c172x\output.xml') -Destination $AircraftDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'aircraft\c172x\reset00.xml') -Destination $AircraftDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'aircraft\c172x\reset01.xml') -Destination $AircraftDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'aircraft\c172x\reset_at_rest.xml') -Destination $AircraftDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'engine\eng_io320.xml') -Destination $EngineDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'engine\prop_75in2f.xml') -Destination $EngineDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'systems\GNCUtilities.xml') -Destination $SystemsDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'systems\Autopilot.xml') -Destination $SystemsDestination -Force
  Copy-Item -LiteralPath (Join-Path $SourceDir 'COPYING') -Destination (Join-Path $DataRoot 'COPYING.txt') -Force

  $ManifestFiles = @(
    'aircraft/c172x/c172x.xml',
    'aircraft/c172x/c172ap.xml',
    'aircraft/c172x/elevator_doublet_init.xml',
    'aircraft/c172x/output.xml',
    'aircraft/c172x/reset00.xml',
    'aircraft/c172x/reset01.xml',
    'aircraft/c172x/reset_at_rest.xml',
    'engine/eng_io320.xml',
    'engine/prop_75in2f.xml',
    'systems/GNCUtilities.xml',
    'systems/Autopilot.xml',
    'COPYING.txt',
    'SOURCE_REVISION.txt'
  )
  @(
    "JSBSim $Version",
    "revision=$Revision",
    "source=$Repository",
    'license=GNU LGPL-2.1-or-later',
    "staged_utc=$([DateTime]::UtcNow.ToString('O'))"
  ) | Set-Content -Path (Join-Path $DataRoot 'SOURCE_REVISION.txt') -Encoding UTF8
  $ManifestFiles | Set-Content -Path (Join-Path $DataRoot 'runtime_manifest.txt') -Encoding UTF8

  foreach ($relative in $ManifestFiles) {
    $path = Join-Path $DataRoot ($relative -replace '/', '\')
    if (!(Test-Path $path)) { throw "JSBSim runtime data staging missed $relative" }
  }
}

function Import-VcVars {
  $VsWhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
  if (!(Test-Path $VsWhere)) { throw "vswhere not found: $VsWhere" }
  $VsPath = (& $VsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath).Trim()
  if ([string]::IsNullOrWhiteSpace($VsPath)) { throw 'Visual Studio C++ Build Tools were not found.' }
  $VcVars = Join-Path $VsPath 'VC\Auxiliary\Build\vcvars64.bat'
  $EnvironmentLines = cmd.exe /s /c "`"$VcVars`" >nul && set"
  foreach ($line in $EnvironmentLines) {
    $index = $line.IndexOf('=')
    if ($index -le 0) { continue }
    [System.Environment]::SetEnvironmentVariable($line.Substring(0, $index), $line.Substring($index + 1), 'Process')
  }
}

Copy-JSBSimRuntimeData
$WindowsResult = $null
$AndroidResult = $null

if (!$SkipWindows) {
  Import-VcVars
  $WindowsBuild = Join-Path $ArtifactDir 'build-windows-x64'
  & $CMake -S $BridgeSource -B $WindowsBuild -G Ninja `
    "-DCMAKE_MAKE_PROGRAM=$Ninja" `
    '-DCMAKE_BUILD_TYPE=Release' `
    "-DJSBSIM_SOURCE_DIR=$SourceDir"
  if ($LASTEXITCODE -ne 0) { throw "Windows x64 JSBSim CMake configure failed with exit code $LASTEXITCODE" }
  & $CMake --build $WindowsBuild --target qfl_jsbsim_native qfl_jsbsim_smoke
  if ($LASTEXITCODE -ne 0) { throw "Windows x64 JSBSim build failed with exit code $LASTEXITCODE" }

  $WrapperDll = Get-ChildItem $WindowsBuild -Recurse -File -Filter 'qfl_jsbsim_native.dll' | Select-Object -First 1
  $JsbsimDll = Get-ChildItem $WindowsBuild -Recurse -File -Filter 'JSBSim.dll' | Select-Object -First 1
  $SmokeExe = Get-ChildItem $WindowsBuild -Recurse -File -Filter 'qfl_jsbsim_smoke.exe' | Select-Object -First 1
  if (!$WrapperDll -or !$JsbsimDll -or !$SmokeExe) { throw 'Windows build did not emit the wrapper, JSBSim shared library, and smoke executable.' }

  $OldPath = $env:PATH
  $env:PATH = "$($WrapperDll.DirectoryName);$($JsbsimDll.DirectoryName);$env:PATH"
  try {
    $SmokeOutput = (& $SmokeExe.FullName $DataRoot | Select-Object -Last 1)
    if ($LASTEXITCODE -ne 0) { throw "Native smoke executable failed with exit code $LASTEXITCODE" }
  } finally {
    $env:PATH = $OldPath
  }
  $SmokeOutput | Set-Content -Path (Join-Path $ArtifactDir 'windows_native_smoke.json') -Encoding UTF8
  $WindowsResult = $SmokeOutput | ConvertFrom-Json

  $Dumpbin = Get-Command dumpbin.exe -ErrorAction Stop
  (& $Dumpbin.Source /headers /dependents /exports $WrapperDll.FullName) | Set-Content -Path (Join-Path $ArtifactDir 'windows_wrapper_inspection.txt') -Encoding UTF8
  (& $Dumpbin.Source /headers /exports $JsbsimDll.FullName) | Set-Content -Path (Join-Path $ArtifactDir 'windows_jsbsim_inspection.txt') -Encoding UTF8

  if (!$SkipUnityStaging) {
    $WindowsPluginDir = Join-Path $ProjectPath 'Assets\Plugins\JSBSim\x86_64'
    New-Item -ItemType Directory -Force -Path $WindowsPluginDir | Out-Null
    Copy-Item -LiteralPath $WrapperDll.FullName -Destination (Join-Path $WindowsPluginDir 'qfl_jsbsim_native.dll') -Force
    Copy-Item -LiteralPath $JsbsimDll.FullName -Destination (Join-Path $WindowsPluginDir 'JSBSim.dll') -Force
  }
}

if (!$SkipAndroid) {
  $AndroidBuild = Join-Path $ArtifactDir 'build-android-arm64'
  & $CMake -S $BridgeSource -B $AndroidBuild -G Ninja `
    "-DCMAKE_MAKE_PROGRAM=$Ninja" `
    '-DCMAKE_BUILD_TYPE=Release' `
    "-DJSBSIM_SOURCE_DIR=$SourceDir" `
    "-DCMAKE_TOOLCHAIN_FILE=$Toolchain" `
    '-DANDROID_ABI=arm64-v8a' `
    '-DANDROID_PLATFORM=android-25' `
    '-DANDROID_STL=c++_shared'
  if ($LASTEXITCODE -ne 0) { throw "Android ARM64 JSBSim CMake configure failed with exit code $LASTEXITCODE" }
  & $CMake --build $AndroidBuild --target qfl_jsbsim_native
  if ($LASTEXITCODE -ne 0) { throw "Android ARM64 JSBSim build failed with exit code $LASTEXITCODE" }

  $WrapperSo = Get-ChildItem $AndroidBuild -Recurse -File -Filter 'libqfl_jsbsim_native.so' | Select-Object -First 1
  $JsbsimSo = Get-ChildItem $AndroidBuild -Recurse -File -Filter 'libJSBSim.so' | Select-Object -First 1
  if (!$WrapperSo -or !$JsbsimSo) { throw 'Android build did not emit both ARM64 JSBSim libraries.' }

  & $Strip --strip-unneeded $WrapperSo.FullName $JsbsimSo.FullName
  if ($LASTEXITCODE -ne 0) { throw "Android ARM64 strip failed with exit code $LASTEXITCODE" }

  (& $ReadElf -h -d -s $WrapperSo.FullName) | Set-Content -Path (Join-Path $ArtifactDir 'android_wrapper_inspection.txt') -Encoding UTF8
  (& $ReadElf -h -d -s $JsbsimSo.FullName) | Set-Content -Path (Join-Path $ArtifactDir 'android_jsbsim_inspection.txt') -Encoding UTF8
  $AndroidResult = [ordered]@{
    wrapper = $WrapperSo.FullName
    jsbsim = $JsbsimSo.FullName
    abi = 'arm64-v8a'
    requested_api = 25
    resolved_ndk_api = 24
  }

  if (!$SkipUnityStaging) {
    $AndroidPluginDir = Join-Path $ProjectPath 'Assets\Plugins\Android\libs\arm64-v8a'
    New-Item -ItemType Directory -Force -Path $AndroidPluginDir | Out-Null
    Copy-Item -LiteralPath $WrapperSo.FullName -Destination (Join-Path $AndroidPluginDir 'libqfl_jsbsim_native.so') -Force
    Copy-Item -LiteralPath $JsbsimSo.FullName -Destination (Join-Path $AndroidPluginDir 'libJSBSim.so') -Force
  }
}

$StagedFiles = @()
if (!$SkipUnityStaging) {
  $StagedFiles = Get-ChildItem (Join-Path $ProjectPath 'Assets\Plugins') -Recurse -File | Where-Object {
    $_.Extension -in @('.dll', '.so') -and $_.Name -match 'JSBSim|qfl_jsbsim'
  }
}
$Hashes = @($StagedFiles | ForEach-Object {
  [ordered]@{ path = $_.FullName; bytes = $_.Length; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$Report = [ordered]@{
  generated_utc = [DateTime]::UtcNow.ToString('O')
  status = 'PASS'
  jsbsim_version = $Version
  jsbsim_revision = $Revision
  jsbsim_license = 'GNU LGPL-2.1-or-later'
  jsbsim_source = $Repository
  unity_version = Split-Path -Leaf $UnityRoot
  ndk_revision = ((Get-Content (Join-Path $Ndk 'source.properties') | Select-String 'Pkg.Revision').ToString().Split('=')[1].Trim())
  windows = $WindowsResult
  android = $AndroidResult
  staged_plugin_files = $Hashes
  runtime_data_root = $DataRoot
  binary_size_note = 'Android libraries are llvm-strip --strip-unneeded Release outputs; JSBSim remains a separate replaceable LGPL shared library.'
  limitations = @(
    'Windows native timing is not Quest CPU timing.',
    'Android build and library inspection do not prove an on-headset native simulation run.',
    'The c172x model/control schedule is an integration reference, not final C172 fidelity.'
  )
}
$Report | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $ArtifactDir 'jsbsim_native_gate_report.json') -Encoding UTF8
Write-Host "JSBSim native gate artifacts: $ArtifactDir"
