# Production Vertical Slice V2 extraction manifest

Decision: retain `T2-QuestFlightLab` as the R&D/reference repository and create a minimal product repository from the explicit production dependency closure. Do not copy the whole Unity project.

## Extract unchanged or with only namespace cleanup

### Authored composition

- `QuestFlightLab/Assets/Scenes/ProductionVerticalSlice.unity` and `.meta`
- `QuestFlightLab/Assets/Resources/QuestFlightLab/Production/ProductionWorldRoot.prefab` and `.meta`
- `QuestFlightLab/Assets/Production/Prefabs/ProductionC172AircraftRig.prefab` and `.meta`
- `QuestFlightLab/Assets/Production/Profiles/ImportedC172PilotSeatProfile.asset` and `.meta`
- `QuestFlightLab/Assets/Production/Materials/SeatCalibrationPanel.mat` and `.meta`
- `QuestFlightLab/Assets/Scripts/Runtime/ProductionVerticalSliceRoot.cs`
- `QuestFlightLab/Assets/Scripts/Runtime/PilotSeatProfile.cs`
- `QuestFlightLab/Assets/Scripts/Runtime/ProductionSeatCalibrationController.cs`
- `QuestFlightLab/Assets/Scripts/Runtime/AircraftReferenceFrameRig.cs`, after removing any legacy-only discovery path from the product assembly
- `QuestFlightLab/Assets/Scripts/Runtime/TrackedXrControllerPoseDrivers.cs`

Copy every paired `.meta` file so serialized GUID references remain stable.

### Aircraft presentation

- `QuestFlightLab/Assets/Scripts/Aircraft/AircraftVisibilityProfileController.cs`
- `QuestFlightLab/Assets/Scripts/Aircraft/ProductionAircraftControlAnimator.cs`
- `QuestFlightLab/Assets/Scripts/Aircraft/ProductionAircraftLightingController.cs`
- `QuestFlightLab/Assets/Scripts/Aircraft/ProductionAircraftViewController.cs`
- only the material/texture dependencies reached from `ProductionC172AircraftRig.prefab`

The imported C172 GLB and `SOURCE.txt` are **conditional**, not an automatic product extraction. The current source note describes it as a personal prototype placeholder. Resolve redistribution and commercial-product rights first; if not clearly acceptable, replace the visual asset while preserving the seat/CG/control binding contracts.

### Input and flight authority

- `QuestFlightLab/Assets/Scripts/Input/GamepadInputReader.cs`
- `QuestFlightLab/Assets/Scripts/Input/DeterministicGamepadInputSource.cs`
- the exact state/config dependencies referenced by those two components
- `QuestFlightLab/Assets/Scripts/Flight/SimpleAircraftPhysics.cs`
- `QuestFlightLab/Assets/Scripts/Flight/Backends/IFlightDynamicsBackend.cs`
- `QuestFlightLab/Assets/Scripts/Flight/Backends/FlightDynamicsAuthority.cs`
- `QuestFlightLab/Assets/Scripts/Flight/Backends/FlightDynamicsCoordinator.cs`
- `QuestFlightLab/Assets/Scripts/Flight/Backends/FlightDynamicsTypes.cs`
- `QuestFlightLab/Assets/Scripts/Flight/Backends/FlightDynamicsInitialConditionProvider.cs`
- Unity prototype backend/config dependencies reached by the coordinator

Do not extract `Usb2BleInputMapper`, HOTAS integrations, sidecar backends, or `FlightDynamicsRuntimeBootstrap`. The product scene must serialize one backend and must not add authorities at runtime.

### Production environment and provenance

- `QuestFlightLab/Assets/Production/Environment/**`
- `QuestFlightLab/Assets/Scripts/Environment/ProductionEnvironmentRoot.cs`
- `QuestFlightLab/Assets/Resources/QuestFlightLab/Environment/Shaders/ProductionMacroGround.shader`
- `QuestFlightLab/Assets/Resources/QuestFlightLab/Environment/Shaders/ProductionRunwayMarking.shader`
- `QuestFlightLab/Assets/Resources/QuestFlightLab/Environment/Shaders/ProductionStableWater.shader`
- the three Poly Haven CC0 maps under `Assets/Resources/QuestFlightLab/Environment/GroundMaterials/`
- `QuestFlightLab/Assets/Resources/QuestFlightLab/Environment/KBDU/kbdu_terrain_rings.json`
- `QuestFlightLab/Assets/Resources/QuestFlightLab/Environment/KBDU/kbdu_reference_context.json`
- `tools/kbdu_environment/**`
- `tools/kbdu_environment/kbdu_source_manifest.json`
- `docs/ASSET_SOURCES.md` and `docs/ENVIRONMENT_DATA_SOURCES.md`

Retain the OSM attribution `© OpenStreetMap contributors` and ODbL derivative record. Retain FAA/BTS and USGS source dates/hashes. Do not copy raw downloads or artifact caches.

`ProductionEnvironmentPrefabBaker.cs` and its deterministic tests may live in a product-only Editor assembly if the product repo will rebake source derivatives. If the first product iteration ships only pinned baked assets, keep the baker in a separate import/tooling package so it cannot become a runtime dependency.

### QA and build gates

- `QuestFlightLab/Assets/Editor/ProductionVisualQaBatchRunner.cs`
- `QuestFlightLab/Assets/Editor/ProductionEnvironmentEditorTests.cs`
- `QuestFlightLab/Assets/Tests/EditMode/ProductionEnvironmentEditModeTests.cs`
- `QuestFlightLab/Assets/Tests/PlayMode/ProductionVerticalSliceAcceptancePlayModeTests.cs`
- `QuestFlightLab/Assets/Tests/PlayMode/ProductionVerticalSliceAircraftPlayModeTests.cs`
- `QuestFlightLab/Assets/Scripts/Environment/MountainTemporalStabilityProbe.cs`
- `QuestFlightLab/Assets/Scripts/Runtime/QuestTemporalVisualGateRecorder.cs`
- `scripts/run_production_visual_qa.ps1`
- `scripts/run_quest_temporal_visual_gate.ps1`
- `scripts/build_production_quest.ps1`
- `scripts/run_unity_tests.ps1`
- `tools/visual_qa_analyze.py`
- the production-scene build method from `QuestFlightLab/Assets/Editor/QuestBuild.cs`, separated from legacy build modes

Keep the evidence boundary strings that distinguish machine identity checks, presentation-pose images, on-device timing, and human headset acceptance.

### Unity project configuration

Start from a new Unity 6000.3.8f1 project. Copy only packages actually required by the serialized scene and scripts, then copy the production XR/OpenXR Android settings, input system settings, 4x MSAA profile, quality settings, and the production scene entry in build settings. Recreate these settings explicitly; do not copy the entire legacy `ProjectSettings` directory without review.

## Conditional R&D package: native JSBSim

Keep the following in the R&D repository until promotion gates pass:

- `QuestFlightLab/Assets/Scripts/Flight/Backends/JSBSimNativeFlightBackend.cs`
- `QuestFlightLab/Assets/Plugins/JSBSim/**`
- `QuestFlightLab/Assets/Plugins/Android/libs/arm64-v8a/**JSBSim**`
- `native/jsbsim_bridge/**`
- `scripts/build_jsbsim_native.ps1`
- `scripts/run_jsbsim_native_validation.ps1`
- `QuestFlightLab/Assets/Editor/FlightBackends/JSBSimNativeScenarioGateV2.cs`

Promote this package only after the unchanged go-around recovery gate passes, manual handling is evaluated, Android native simulation is timed on-device, and both ARM64 libraries are rebuilt with 16 KB page alignment.

## Explicitly exclude from the product repository

- `Assets/Scenes/InputLab.unity`
- `QuestFirstViewRuntimeRepair` and legacy first-view/debug bootstraps
- `KbduInspiredWorldBuilder` as the normal environment path
- legacy `RealKbduEnvironmentBuilder` runtime construction path
- Gaussian splat package/assets/providers and scenic splat samples
- legacy water cubes/ribbons and overlapping runway/terrain repair layers
- training lessons/UI and verbose Playtest HUD/evidence overlays
- JSBSim Editor sidecar and runtime backend-selection UI
- USB2BLE, HOTAS, and unrelated hardware integrations
- all `Library`, `Temp`, `Obj`, `Logs`, `Builds`, APK, raw screenshot/video/logcat, package-cache, and raw geospatial/download directories

## Extraction procedure

1. Freeze the integrated R&D source at the delivered SHA and tag the evidence witness.
2. Create a new empty Unity 6000.3.8f1 repository with Git LFS rules reviewed before importing any binary.
3. Copy the authored scene/prefabs with their `.meta` files and use Unity dependency inspection to enumerate the closure; compare every discovered dependency against this allowlist.
4. Copy the production runtime, environment, QA, and build modules above. Fix namespaces/assembly definitions without changing serialized GUIDs or the scene hierarchy contract.
5. Resolve the imported-aircraft rights gate before copying the GLB. If replacement is required, preserve the `PilotSeatProfile`, CG, visibility-profile, and control-binding interfaces.
6. Run license/source verification and inspect every binary, texture, mesh, and native library before the first product commit.
7. Prove there are no missing references and that `ProductionVerticalSlice` opens with exactly one XR Origin, one physics authority, no runtime repair, and no legacy environment/UI roots.
8. Run EditMode, PlayMode, 16-view Visual QA, production APK build, three cold launches, and the worn 90+60-second Quest gate.

## Clean-product milestone acceptance

The extraction milestone is complete only when:

- the clean repo contains no legacy scene/bootstrap/splat/training dependency;
- the authored hierarchy and runway/water/mountain invariants pass unchanged;
- the aircraft asset has documented product-use rights;
- ground, water, mountains, airport context, and cockpit lighting score at least 6/10 in lead inspection before headset testing;
- a wearer confirms default seat position, stereo mountain/water stability, and control presentation across three cold launches;
- Quest 3 measurement after 90 seconds warmup records p95 `<= 13.89 ms`, no more than 5% over-budget frames, no crash/ANR/OOM/thermal warning, and traces separate CPU and GPU time;
- Unity prototype remains the only authority unless the native 11/11 gate and on-device checks pass.

If these conditions fail after one measured optimization/art pass, evaluate PCVR. Do not reintroduce the legacy runtime-repair architecture into the clean product repository.
