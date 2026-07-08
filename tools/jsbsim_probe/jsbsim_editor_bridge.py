#!/usr/bin/env python3
"""Generate a JSBSim telemetry run for Unity Editor bridge validation.

The Unity editor bridge invokes this script as a sidecar process, then imports
the JSON samples and applies them to a Unity proxy object. This keeps JSBSim
out of the Quest runtime while proving the editor process/data bridge.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import pathlib
import sys
from dataclasses import asdict, dataclass


@dataclass
class BridgeControl:
    throttle: float
    elevator: float
    aileron: float
    rudder: float
    flaps: float
    trim: float


@dataclass
class BridgeSample:
    time_s: float
    east_m: float
    north_m: float
    agl_ft: float
    altitude_delta_ft: float
    airspeed_kt: float
    vertical_speed_fpm: float
    pitch_deg: float
    bank_deg: float
    heading_deg: float
    throttle: float
    elevator: float
    aileron: float
    rudder: float
    flaps: float
    trim: float


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--aircraft", default="c172x")
    parser.add_argument("--reset", default="reset00")
    parser.add_argument("--duration", type=float, default=45.0)
    parser.add_argument("--dt", type=float, default=1.0 / 120.0)
    parser.add_argument("--sample-step", type=float, default=0.1)
    args = parser.parse_args()

    try:
        import jsbsim  # type: ignore
    except Exception as exc:  # noqa: BLE001
        print(f"JSBSim import failed: {exc}", file=sys.stderr)
        return 2

    out = pathlib.Path(args.output_dir)
    out.mkdir(parents=True, exist_ok=True)

    samples = run_bridge_profile(jsbsim, args.aircraft, args.reset, args.duration, args.dt, args.sample_step)
    aggregate = {
        "sample_count": len(samples),
        "duration_s": args.duration,
        "max_airspeed_kt": max((s.airspeed_kt for s in samples), default=0.0),
        "max_agl_ft": max((s.agl_ft for s in samples), default=0.0),
        "max_abs_bank_deg": max((abs(s.bank_deg) for s in samples), default=0.0),
        "final_airspeed_kt": samples[-1].airspeed_kt if samples else 0.0,
        "final_agl_ft": samples[-1].agl_ft if samples else 0.0,
        "final_heading_deg": samples[-1].heading_deg if samples else 0.0,
        "ground_track_m": math.hypot(samples[-1].east_m, samples[-1].north_m) if samples else 0.0,
    }

    report = {
        "tool": "jsbsim_editor_bridge",
        "classification": "editor_sidecar_bridge",
        "jsbsim_version": getattr(jsbsim, "__version__", "unknown"),
        "aircraft": args.aircraft,
        "reset": args.reset,
        "profile": "takeoff_rotate_climb_shallow_turn",
        "samples": [asdict(s) for s in samples],
        "aggregate": aggregate,
        "limitations": [
            "Editor sidecar bridge only; Quest/Android runtime integration is not attempted.",
            "Position is integrated from JSBSim heading and ground-speed output for Unity visualization.",
            "This proves process/data bridge feasibility, not final C172 flight fidelity.",
        ],
    }

    (out / "jsbsim_editor_bridge_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_csv(out / "jsbsim_editor_bridge_samples.csv", samples)
    write_markdown(out / "jsbsim_editor_bridge_summary.md", report)
    print(json.dumps({"ok": True, "sample_count": len(samples), "output_dir": str(out)}, indent=2))
    return 0


def run_bridge_profile(jsbsim, aircraft: str, reset: str, duration: float, dt: float, sample_step: float) -> list[BridgeSample]:
    fdm = jsbsim.FGFDMExec(None)
    fdm.set_debug_level(0)
    fdm.set_dt(dt)
    if not fdm.load_model(aircraft):
        raise RuntimeError(f"Could not load JSBSim aircraft model {aircraft!r}")
    if reset and not fdm.load_ic(reset, True):
        raise RuntimeError(f"Could not load reset file {reset!r}")

    safe_set(fdm, "ic/psi-true-deg", 78.0)
    safe_set(fdm, "ic/theta-deg", 0.0)
    safe_set(fdm, "ic/phi-deg", 0.0)
    safe_set(fdm, "ic/vc-kts", 0.0)
    fdm.run_ic()
    start_engine(fdm)

    east_m = 0.0
    north_m = 0.0
    start_agl = get(fdm, "position/h-agl-ft")
    next_sample = 0.0
    samples: list[BridgeSample] = []

    while fdm.get_sim_time() < duration:
        t = fdm.get_sim_time()
        control = bridge_control(t)
        set_controls(fdm, control)
        fdm.run()

        heading = get(fdm, "attitude/psi-deg")
        speed_fps = get(fdm, "velocities/vg-fps")
        if not math.isfinite(speed_fps) or speed_fps <= 0.0:
            speed_fps = get(fdm, "velocities/vc-kts") * 1.68781
        distance_m = max(0.0, speed_fps) * 0.3048 * dt
        heading_rad = math.radians(heading)
        east_m += math.sin(heading_rad) * distance_m
        north_m += math.cos(heading_rad) * distance_m

        if fdm.get_sim_time() + 1e-6 >= next_sample:
            agl = get(fdm, "position/h-agl-ft")
            samples.append(
                BridgeSample(
                    time_s=get(fdm, "simulation/sim-time-sec"),
                    east_m=east_m,
                    north_m=north_m,
                    agl_ft=agl,
                    altitude_delta_ft=agl - start_agl,
                    airspeed_kt=get(fdm, "velocities/vc-kts"),
                    vertical_speed_fpm=get(fdm, "velocities/h-dot-fps") * 60.0,
                    pitch_deg=get(fdm, "attitude/theta-deg"),
                    bank_deg=get(fdm, "attitude/phi-deg"),
                    heading_deg=heading,
                    throttle=control.throttle,
                    elevator=control.elevator,
                    aileron=control.aileron,
                    rudder=control.rudder,
                    flaps=control.flaps,
                    trim=control.trim,
                )
            )
            next_sample += sample_step

    return samples


def bridge_control(time: float) -> BridgeControl:
    throttle = 1.0
    elevator = 0.0
    aileron = 0.0
    rudder = math.sin(time * 0.45) * 0.04
    flaps = 0.0
    trim = 0.10
    if time > 8.5:
        elevator = 0.16
    if time > 12.0:
        elevator = 0.24
    if time > 22.0:
        elevator = 0.10
        aileron = 0.09 if time < 30.0 else -0.06 if time < 38.0 else 0.0
        rudder = aileron * 0.25
    return BridgeControl(throttle, elevator, aileron, rudder, flaps, trim)


def set_controls(fdm, c: BridgeControl) -> None:
    for prop in ("fcs/throttle-cmd-norm", "fcs/throttle-cmd-norm[0]", "fcs/throttle-pos-norm", "fcs/throttle-pos-norm[0]"):
        safe_set(fdm, prop, c.throttle)
    for prop in ("fcs/mixture-cmd-norm", "fcs/mixture-cmd-norm[0]", "fcs/mixture-pos-norm", "fcs/mixture-pos-norm[0]"):
        safe_set(fdm, prop, 1.0)
    for prop in ("fcs/aileron-cmd-norm", "fcs/aileron-pos-norm"):
        safe_set(fdm, prop, c.aileron)
    for prop in ("fcs/elevator-cmd-norm", "fcs/elevator-pos-norm"):
        safe_set(fdm, prop, c.elevator)
    for prop in ("fcs/rudder-cmd-norm", "fcs/rudder-pos-norm"):
        safe_set(fdm, prop, c.rudder)
    for prop in ("fcs/flap-cmd-norm", "fcs/flap-pos-norm"):
        safe_set(fdm, prop, c.flaps)
    for prop in ("fcs/elevator-trim-cmd-norm", "fcs/elevator-trim-pos-norm"):
        safe_set(fdm, prop, c.trim)
    safe_set(fdm, "fcs/left-brake-cmd-norm", 0.0)
    safe_set(fdm, "fcs/right-brake-cmd-norm", 0.0)
    safe_set(fdm, "fcs/center-brake-cmd-norm", 0.0)


def start_engine(fdm) -> None:
    safe_set(fdm, "propulsion/magneto_cmd", 3.0)
    safe_set(fdm, "propulsion/starter_cmd", 1.0)
    safe_set(fdm, "propulsion/active_engine", 0.0)
    safe_set(fdm, "propulsion/engine/set-running", 1.0)
    safe_set(fdm, "fcs/mixture-cmd-norm", 1.0)
    safe_set(fdm, "fcs/mixture-cmd-norm[0]", 1.0)
    safe_set(fdm, "fcs/throttle-cmd-norm", 0.25)
    safe_set(fdm, "fcs/throttle-cmd-norm[0]", 0.25)


def safe_set(fdm, prop: str, value: float) -> None:
    try:
        fdm.set_property_value(prop, value)
    except Exception:
        pass


def get(fdm, prop: str) -> float:
    try:
        return float(fdm.get_property_value(prop))
    except Exception:
        return float("nan")


def write_csv(path: pathlib.Path, samples: list[BridgeSample]) -> None:
    if not samples:
        path.write_text("", encoding="utf-8")
        return
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(asdict(samples[0]).keys()))
        writer.writeheader()
        for sample in samples:
            writer.writerow(asdict(sample))


def write_markdown(path: pathlib.Path, report: dict) -> None:
    aggregate = report["aggregate"]
    lines = [
        "# JSBSim Editor Bridge Summary",
        "",
        f"- Classification: `{report['classification']}`",
        f"- JSBSim version: `{report['jsbsim_version']}`",
        f"- Aircraft: `{report['aircraft']}`",
        f"- Samples: {aggregate['sample_count']}",
        f"- Max airspeed: {aggregate['max_airspeed_kt']:.1f} kt",
        f"- Max AGL: {aggregate['max_agl_ft']:.1f} ft",
        f"- Ground track: {aggregate['ground_track_m']:.1f} m",
        "",
        "## Limitations",
        "",
    ]
    lines.extend(f"- {item}" for item in report["limitations"])
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
