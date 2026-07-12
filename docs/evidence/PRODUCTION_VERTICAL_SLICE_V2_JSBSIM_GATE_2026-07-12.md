# Production Vertical Slice V2 — JSBSim Gate

Date: 2026-07-12

## Decision

`JSBSimNativeFlightBackend` is **not production-qualified**. The authored Production Vertical Slice remains serialized to `UnityPrototypeFlightBackend` as its sole pose authority.

The final native gate passed 10/11 scenarios and 9/10 critical scenarios. The only failure was the unchanged go-around requirement for at least 75 ft of altitude recovery during the second half: JSBSim recovered 67.93 ft. No threshold was relaxed or reframed to turn that result into a pass.

## Baseline failure record

The pre-change native report passed 1/10 scenarios. This report was captured before changing schedules or acceptance logic at:

`production_vertical_slice_v2_20260712_020951/baseline_jsbsim/`

| Baseline scenario | Result | Principal failure |
| --- | --- | --- |
| Ground idle and brake hold | PASS | — |
| Takeoff roll | FAIL | 41.8° runway-heading departure |
| Rotation near Vr | FAIL | liftoff timing and climb |
| Vy climb | FAIL | 804 ft loss and extreme bank |
| Level flight trim | FAIL | 982 ft loss, excessive pitch/bank |
| Shallow left/right turn | FAIL | incorrect left response and altitude loss |
| Slow flight | FAIL | speed, altitude, and bank envelopes |
| Stall approach/recovery | FAIL | no required deceleration; excessive loss/bank |
| Final approach | FAIL | excessive descent, speed/VSI, and bank |
| Go-around | FAIL | speed and bank envelopes |

The intermediate sign-correction run also preserved a real non-finite failure at Vy-climb step 2101. That showed the old fixed-deflection schedule was invalid once pitch direction was corrected; state was not clamped or sanitized.

## Corrective audit

- Native reset now clears stale AP, FCS, trim, flap, and brake commands before `RunIC`.
- KBDU latitude, longitude, MSL altitude, terrain elevation, true runway heading, airspeed, pitch, bank, and flight-path angle are set from one initial-condition contract.
- One piston engine is direct-started before `RunIC`, with both magnetos, mixture rich, starter released, and fresh idle controls.
- Project-positive elevator and trim mean nose-up; the stock c172x moment convention requires both ABI commands to be inverted.
- Project-positive rudder means right yaw; the stock c172x rudder moment also requires ABI inversion.
- Aileron remains pass-through.
- Every required case uses bounded, rate-limited scripted-pilot corrections. The physical envelopes, 120 Hz step, and zero-allocation requirement were not loosened.
- No opaque `DoTrim` result is used as hidden acceptance input. Actual pitch trim moves at no more than 0.12 normalized units/s.

## Direct stock-c172x sign contracts

| Axis | Project-positive response | Negative pulse | Positive pulse | Paired delta / required | Result |
| --- | --- | ---: | ---: | ---: | --- |
| Elevator | nose up | +1.23° pitch | +15.40° pitch | 14.17° / 5.00° | PASS |
| Pitch trim | nose-up trim | +0.97° pitch | +15.40° pitch | 14.43° / 5.00° | PASS |
| Aileron | right bank | +0.78° bank | +21.82° bank | 21.04° / 8.00° | PASS |
| Rudder | right yaw | +0.49° heading | +5.02° heading | 4.53° / 2.00° | PASS |

## Final 11-scenario result

Evidence: `production_vertical_slice_v2_20260712_020951/workstream_c_jsbsim/production_v2_gate_try2/`

| Scenario | Critical | Result | Initial/final speed | Initial/final AGL | VSI range | Heading change |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| Idle/brake hold | Yes | PASS | 0.0/0.0 kt | 4/4 ft | -20/80 fpm | 0.0° |
| Taxi acceleration/braking | Yes | PASS | 0.0/0.0 kt | 4/4 ft | -24/80 fpm | +1.4° |
| Takeoff roll | Yes | PASS | 0.0/47.4 kt | 4/4 ft | -24/80 fpm | +5.4° |
| Rotation | Yes | PASS | 0.0/56.5 kt | 4/61 ft | -24/450 fpm | +10.4° |
| Vy climb | Yes | PASS | 74.0/67.9 kt | 1000/1236 ft | 342/1169 fpm | +4.9° |
| Trimmed level flight | Yes | PASS | 100.0/90.6 kt | 1200/1385 ft | 159/1248 fpm | +1.8° |
| Shallow left turn | Yes | PASS | 95.0/89.7 kt | 1200/1291 ft | -130/1022 fpm | -49.5° |
| Shallow right turn | Yes | PASS | 95.0/90.1 kt | 1200/1263 ft | -221/958 fpm | +64.6° |
| Slow flight/stall warning/recovery | No | PASS | 58.0/67.5 kt | 1800/1733 ft | -1092/557 fpm | +6.6° |
| Approach | Yes | PASS | 70.0/65.7 kt | 900/756 ft | -557/125 fpm | +2.4° |
| Go-around | Yes | **FAIL** | 65.0/63.4 kt | 300/479 ft | 97/943 fpm | +6.5° |

Go-around gained 179.04 ft overall and ended at 63.4 kt, +710 fpm, and 10.2° pitch. Its second-half gain was 67.93 ft, 7.07 ft short of the required 75 ft. This is the exact native promotion blocker.

## Timing and allocations

- Fixed step: 120 Hz
- Completed native scenario steps: 27,360
- Windows average native step: 0.0232 ms
- Worst scenario p95: 0.0813 ms
- Managed steady-state allocations: zero in every scenario
- Non-finite state: none in the final run
- Windows timing is not Quest CPU timing.

## Production authority and reset integration

- `ProductionC172AircraftRig.prefab` serializes `requestedBackend: 0` (`UnityPrototype`).
- The production scene test finds exactly one `FlightDynamicsCoordinator` and one `SimpleAircraftPhysics` authority.
- `FlightDynamicsRuntimeBootstrap` exits when `ProductionVerticalSliceRoot` is active, so launch options cannot add or mutate a production backend.
- `FlightDynamicsInitialConditionProvider` supplies the authored runway spawn. The coordinator caches it at initialization and uses the same state for reset.
- `LastAppliedControls` is an allocation-free value snapshot published at the exact backend `SetControls` boundary for aircraft animation.

## Native binary parity and inspection

Both wrappers were rebuilt from JSBSim 1.3.1 revision `3b25f25e49b42d0489c04ac805674fc1450ca579` and bridge source SHA-256 `4f3d45296baad9875994cb8bff84e9e47bea0092c734c58ea3bf8570b934ad3d`.

| Target | Bytes | SHA-256 | Inspection |
| --- | ---: | --- | --- |
| Windows x64 `qfl_jsbsim_native.dll` | 37,888 | `9d6bb4232aa3e5bb097958a3ae79b029f3de12d1c6dc5014a0a7ff5848ac90ab` | PE x64; all 10 ABI exports; depends on `JSBSim.dll` |
| Android arm64 `libqfl_jsbsim_native.so` | 49,000 | `8a307338b2b58d26ca1d6bc494d032f04db8e6611255daf2cccf369c2b37c28b` | stripped ELF64 AArch64; SONAME present; depends on unversioned `libJSBSim.so` |

Toolchains: MSVC 14.44.35207 Release; Unity 6000.3.8f1 Android NDK 27.2.12479018, `arm64-v8a`, Android platform 25, Release.

The non-Unity Windows ABI smoke passed 7,200 steps at 120 Hz: 0.008071 ms average and 0.009200 ms p95. Android ELF inspection is not an on-device native simulation run.

## Exact follow-up milestone

Keep Unity prototype as the sole Production Vertical Slice authority. If native fidelity remains a product priority, schedule a focused JSBSim specialist milestone to reconcile c172x go-around energy/configuration and manual handling, rerun this unchanged 11-scenario gate, and then collect an on-device native timing/handling run before reconsidering promotion.
