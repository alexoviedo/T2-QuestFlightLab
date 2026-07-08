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
- Current Unity physics remains a prototype approximation.
- The probe is not calibrated to current Unity aircraft mass/aero/engine values.
- No Android/Quest JSBSim runtime work was attempted in this chunk.
- No FAA/training suitability is claimed.
