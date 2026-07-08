# JSBSim Integration Plan

## Status

JSBSim is feasible as the serious flight-dynamics reference path, but not yet as a drop-in Quest runtime backend.

Local probe result:

- Python package: `jsbsim 1.3.1`
- Bundled aircraft found: `c172p`, `c172r`, `c172x`
- Best current standalone probe: `c172x` with `reset00`
- Output artifacts: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309\jsbsim_probe\run_c172x_reset_final`
- Result shape: finite 170 s run, 341 samples, max 86.6 kt, max 34.1 ft AGL, max absolute bank 65.7 deg

This proves that JSBSim can run locally and produce C172-like telemetry. It does not prove the current Unity aircraft is calibrated to JSBSim, and it does not prove Android/Quest runtime integration.

## Probe Script

Run:

```powershell
python .\tools\jsbsim_probe\run_c172_probe.py --output-dir <artifact-root>\jsbsim_probe\run
```

The script writes:

- `jsbsim_c172_probe.json`
- `jsbsim_c172_probe.csv`
- `jsbsim_c172_probe_summary.md`

The profile covers:

- idle/ground,
- control sweep,
- takeoff roll,
- rotate/climb,
- shallow left/right turns,
- approach preview,
- go-around placeholder.

## v1 Unity Comparator

The v1 production visual/physics upgrade adds an offline comparator:

```powershell
python .\tools\jsbsim_probe\compare_jsbsim_unity.py `
  --jsbsim-json <artifact-root>\jsbsim_probe\v1_reference\jsbsim_c172_probe.json `
  --jsbsim-csv <artifact-root>\jsbsim_probe\v1_reference\jsbsim_c172_probe.csv `
  --unity-scenario-csv <artifact-root>\after_editor_scenarios\scenario_results.csv `
  --output-dir <artifact-root>\jsbsim_unity_comparison_after
```

Latest classification: `reference_oracle_only`.

The current open-loop JSBSim reference and Unity scenario suite are not matched-control profiles yet, but they expose useful trend gaps:

- Unity takeoff-roll and go-around speeds are materially higher than the current JSBSim open-loop reference.
- Unity rotation/climb altitude gain is materially higher than the current JSBSim open-loop reference.
- Unity shallow-turn bank response is much milder than the current JSBSim open-loop reference.
- Approach speed is the closest current trend match.

The Unity config was tuned only modestly in v1 to move toward a heavier, more damped C172-style trainer. This is not a JSBSim-backed runtime and not a final fidelity claim.

## v2 Calibration Pass

The v2 KBDU environment/physics pass keeps JSBSim as an offline reference oracle and tunes the Unity prototype more conservatively against the same open-loop comparison.

Latest v2 artifacts:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\baseline_jsbsim_comparison
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\after_jsbsim_comparison
```

Current before/after deltas:

| Phase | Baseline Delta | v2 Delta | Direction |
| --- | ---: | ---: | --- |
| Takeoff roll speed | +23.4 kt | +22.2 kt | slightly closer |
| Rotation/climb altitude | +91.6 ft | +77.2 ft | closer |
| Shallow turn bank | -48.2 deg | -45.2 deg | slightly closer |
| Approach speed | +8.7 kt | +7.6 kt | closer |
| Go-around speed | +36.4 kt | +35.1 kt | slightly closer |

This is useful trend evidence, not a fidelity pass/fail. The next serious physics chunk should create matched-control JSBSim scenarios for the same Unity scenario definitions before making larger aerodynamic changes.

## v2.1 Matched-Control Comparator

The v2.1 pass adds a matched-control comparator:

```powershell
python .\tools\jsbsim_probe\run_matched_jsbsim_comparison.py `
  --unity-scenario-json <artifact-root>\after_editor_scenarios_final2\scenario_results.json `
  --output-dir <artifact-root>\after_matched_jsbsim_comparison
```

This runner defines paired schedules for takeoff roll, rotation/climb, Vy climb, shallow turns, stabilized approach, and go-around, then compares JSBSim `c172x` output against Unity scenario telemetry.

Latest v2.1 artifacts:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\baseline_matched_jsbsim_comparison
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\after_matched_jsbsim_comparison
```

Aggregate before/after:

| Metric | Baseline | v2.1 | Direction |
| --- | ---: | ---: | --- |
| Mean weighted error score | 142.14 | 141.99 | slightly closer |
| Mean airspeed RMSE | 26.9 kt | 26.7 kt | slightly closer |
| Mean altitude-delta RMSE | 184.0 ft | 183.7 ft | slightly closer |
| Mean bank RMSE | 57.0 deg | 57.0 deg | unchanged |

The modest improvement came from a small thrust reduction that preserves all existing Unity scenario gates. More aggressive config-only tuning broke the traffic-pattern/stabilized-approach scenarios, so deeper realism should move toward a JSBSim runtime/bridge feasibility path instead of overfitting this prototype Unity model.

## Quality Gate v1 Editor Bridge

The quality-gate pass proves a narrow Editor-side JSBSim bridge:

```powershell
.\scripts\run_jsbsim_editor_bridge.ps1 -ArtifactDir <artifact-root>\jsbsim_editor_bridge
```

Bridge pieces:

- Unity runner: `QuestFlightLab.Editor.JSBSimEditorBridgeRunner.RunBridge`
- Python sidecar: `tools/jsbsim_probe/jsbsim_editor_bridge.py`
- JSBSim aircraft: `c172x`
- Result classification: `editor_sidecar_bridge_unity_applied`
- Samples imported: 451
- Unity proxy poses applied: 451
- Max airspeed: 101.5 kt
- Max AGL: 49.3 ft / 15.0 m
- Ground track: 936.6 m

This proves Unity Editor can invoke JSBSim, import telemetry, convert it into Unity-space poses, and apply those poses to a proxy object. It does not prove interactive runtime control, Android/Quest native integration, final coordinate conversion, or final C172 fidelity.

Next backend step: make the Editor bridge interactive, with Unity controls advancing JSBSim step-by-step and JSBSim state driving the visible aircraft. Only after that should the project evaluate an Android ARM64 native plugin or other Quest runtime bridge.

## JSBSim Live Editor Driver

The live-driver pass promotes the batch/import proof into an Editor-only frame loop:

```powershell
.\scripts\run_jsbsim_live_editor_driver.ps1 -ArtifactDir <artifact-root>\live_driver_final
```

Driver pieces:

- Unity runner: `QuestFlightLab.Editor.JSBSimLiveEditorDriverRunner.RunLiveDriver`
- Python sidecar: `tools/jsbsim_probe/jsbsim_live_sidecar.py`
- JSBSim aircraft: `c172x`
- Result classification: `editor_interactive_sidecar_frame_loop`
- Controls/timesteps sent from Unity to JSBSim: 1,801
- JSBSim poses immediately applied to the visible Unity aircraft root: 1,801
- Current imported C172 visual asset: preserved and driven by the JSBSim state
- KBDU-inspired world: built in the same Editor evidence scene

Latest artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\jsbsim_live_driver_20260708_012959\live_driver_final
```

The accepted run proves interactive process/data/pose plumbing: Unity sends each frame's controls and timestep, JSBSim advances, and Unity applies the returned aircraft state before capturing evidence. It does not prove a stable final control law. The conservative takeoff/climb schedule reached 1,801 applied samples, 879.4 m ground track, 9.9 m max AGL, and 89.7 kt max airspeed. The JSBSim `c172x` / `reset00` / simple open-loop control setup still shows heading and bank transients, so controlled turns and runtime replacement need a proper initialization and control-law pass.

## Integration Path

### Short Term: Offline Reference Oracle

Use JSBSim outputs to compare Unity scenario trends:

- throttle and acceleration shape,
- rotation and initial climb timing,
- pitch/bank/heading response,
- approach/go-around control response,
- stall and slow-flight trend once a stable profile exists.

This can be added to editor scenario reports without changing the Quest runtime.

### Medium Term: Bridge / Native Plugin Feasibility

Evaluate:

- JSBSim native library build on Windows for editor tooling,
- server/process bridge for editor-only comparisons,
- Android ARM64 native plugin feasibility,
- property mapping between Unity controls and JSBSim controls,
- transform/unit/coordinate conversion,
- runtime determinism and frame-step ownership.

### Long Term: Runtime Backend Candidate

Only consider JSBSim as the actual flight-dynamics backend after:

- stable aircraft XML/config is selected or authored,
- Unity visuals can be driven by JSBSim state,
- controls, engine, trim, flaps, brakes, and ground handling are mapped,
- Android build and Quest performance are proven,
- scenario tests pass against reference envelopes.

## Current Limitations

- Open-loop probe is not a stable autopilot.
- Matched-control profiles still diverge strongly in airborne cases because initialization, aircraft definitions, and control mappings are not equivalent yet.
- Quality Gate v1 bridge is sidecar/import/apply only, not an interactive flight backend.
- Current Unity physics remains a prototype approximation.
- The probe is not calibrated to current Unity aircraft mass/aero/engine values.
- No Android/Quest JSBSim runtime work was attempted in this chunk.
- No FAA/training suitability is claimed.
