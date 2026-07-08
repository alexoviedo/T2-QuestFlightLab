#!/usr/bin/env python3
"""Run a small JSBSim C172P feasibility profile and export JSON/CSV.

This is intentionally standalone and outside Unity runtime. It is a gate for
using JSBSim as a reference oracle before considering a Quest runtime backend.
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
class Sample:
    time_s: float
    phase: str
    throttle: float
    aileron: float
    elevator: float
    rudder: float
    altitude_ft: float
    agl_ft: float
    airspeed_kt: float
    groundspeed_fps: float
    pitch_deg: float
    bank_deg: float
    heading_deg: float
    latitude_deg: float
    longitude_deg: float
    distance_from_start_ft: float

    def is_finite(self) -> bool:
        return all(
            math.isfinite(v)
            for v in (
                self.altitude_ft,
                self.agl_ft,
                self.airspeed_kt,
                self.groundspeed_fps,
                self.pitch_deg,
                self.bank_deg,
                self.heading_deg,
                self.latitude_deg,
                self.longitude_deg,
                self.distance_from_start_ft,
            )
        )


def controls_for_time(t: float) -> tuple[str, float, float, float, float]:
    if t < 3.0:
        return "idle/ground", 0.08, 0.0, 0.0, 0.0
    if t < 12.0:
        sweep = math.sin((t - 3.0) * 1.25)
        return "control-sweep", 0.20, 0.35 * sweep, -0.18 * sweep, 0.22 * sweep
    if t < 32.0:
        return "takeoff-roll", 1.0, 0.0, 0.02, 0.03 * math.sin(t * 0.8)
    if t < 58.0:
        return "rotate/climb", 1.0, 0.0, 0.16, 0.02 * math.sin(t * 0.7)
    if t < 88.0:
        return "shallow-left-turn", 0.92, -0.025, 0.035, -0.015
    if t < 118.0:
        return "shallow-right-turn", 0.90, 0.025, 0.025, 0.015
    if t < 145.0:
        return "approach-preview", 0.48, 0.0, -0.02, 0.0
    return "go-around-placeholder", 1.0, 0.0, 0.10, 0.02


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--duration", type=float, default=170.0)
    parser.add_argument("--dt", type=float, default=1.0 / 120.0)
    parser.add_argument("--aircraft", default="c172x")
    parser.add_argument("--reset", default="reset00")
    args = parser.parse_args()

    try:
        import jsbsim  # type: ignore
    except Exception as exc:  # noqa: BLE001
        print(f"JSBSim import failed: {exc}", file=sys.stderr)
        print("Install into a local venv with: python -m pip install jsbsim", file=sys.stderr)
        return 2

    out = pathlib.Path(args.output_dir)
    out.mkdir(parents=True, exist_ok=True)

    fdm = jsbsim.FGFDMExec(None)
    fdm.set_debug_level(0)
    fdm.set_dt(args.dt)
    if not fdm.load_model(args.aircraft):
        raise RuntimeError(f"Could not load JSBSim aircraft model {args.aircraft!r}")

    if args.reset and not fdm.load_ic(args.reset, True):
        raise RuntimeError(f"Could not load reset file {args.reset!r} for aircraft {args.aircraft!r}")
    fdm.run_ic()
    start_engine(fdm)

    start_lat = fdm.get_property_value("position/lat-gc-deg")
    start_lon = fdm.get_property_value("position/long-gc-deg")
    samples: list[Sample] = []
    next_sample = 0.0
    stopped_reason = "duration_complete"

    while fdm.get_sim_time() < args.duration:
        t = fdm.get_sim_time()
        phase, throttle, aileron, elevator, rudder = controls_for_time(t)
        set_controls(fdm, throttle, aileron, elevator, rudder)
        fdm.run()

        if fdm.get_sim_time() + 1e-6 >= next_sample:
            sample = read_sample(fdm, phase, throttle, aileron, elevator, rudder, start_lat, start_lon)
            samples.append(sample)
            if not sample.is_finite():
                stopped_reason = "non_finite_state"
                break
            next_sample += 0.5

    report = {
        "tool": "JSBSim Python package",
        "jsbsim_version": getattr(jsbsim, "__version__", "unknown"),
        "aircraft": args.aircraft,
        "reset": args.reset,
        "duration_s": args.duration,
        "dt_s": args.dt,
        "sample_count": len(samples),
        "stopped_reason": stopped_reason,
        "final": asdict(samples[-1]) if samples else None,
        "max_airspeed_kt": max((s.airspeed_kt for s in samples), default=0.0),
        "max_agl_ft": max((s.agl_ft for s in samples), default=0.0),
        "max_abs_bank_deg": max((abs(s.bank_deg) for s in samples), default=0.0),
        "finite": stopped_reason != "non_finite_state",
        "phases": sorted({s.phase for s in samples}),
        "integration_recommendation": "Use JSBSim first as an offline reference oracle for Unity scenario tests; defer Quest runtime integration until native plugin/server bridge cost is scoped.",
        "limitations": [
            "This is not calibrated to the current Unity prototype aircraft.",
            "The input profile is open-loop and not a stable autopilot.",
            "This does not prove Android/Quest runtime integration.",
        ],
    }

    (out / "jsbsim_c172_probe.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    with (out / "jsbsim_c172_probe.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(asdict(samples[0]).keys()))
        writer.writeheader()
        for sample in samples:
            writer.writerow(asdict(sample))

    write_markdown(out / "jsbsim_c172_probe_summary.md", report)
    print(json.dumps(report, indent=2))
    return 0


def set_controls(fdm, throttle: float, aileron: float, elevator: float, rudder: float) -> None:
    for prop in ("fcs/throttle-cmd-norm", "fcs/throttle-cmd-norm[0]", "fcs/throttle-pos-norm", "fcs/throttle-pos-norm[0]"):
        safe_set(fdm, prop, throttle)
    for prop in ("fcs/mixture-cmd-norm", "fcs/mixture-cmd-norm[0]", "fcs/mixture-pos-norm", "fcs/mixture-pos-norm[0]"):
        safe_set(fdm, prop, 1.0)
    for prop in ("fcs/aileron-cmd-norm", "fcs/aileron-pos-norm"):
        safe_set(fdm, prop, aileron)
    for prop in ("fcs/elevator-cmd-norm", "fcs/elevator-pos-norm"):
        safe_set(fdm, prop, elevator)
    for prop in ("fcs/rudder-cmd-norm", "fcs/rudder-pos-norm"):
        safe_set(fdm, prop, rudder)
    safe_set(fdm, "fcs/flap-cmd-norm", 0.0)
    safe_set(fdm, "fcs/center-brake-cmd-norm", 0.0)
    safe_set(fdm, "fcs/left-brake-cmd-norm", 0.0)
    safe_set(fdm, "fcs/right-brake-cmd-norm", 0.0)


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


def read_sample(fdm, phase: str, throttle: float, aileron: float, elevator: float, rudder: float, start_lat: float, start_lon: float) -> Sample:
    lat = get(fdm, "position/lat-gc-deg")
    lon = get(fdm, "position/long-gc-deg")
    return Sample(
        time_s=get(fdm, "simulation/sim-time-sec"),
        phase=phase,
        throttle=throttle,
        aileron=aileron,
        elevator=elevator,
        rudder=rudder,
        altitude_ft=get(fdm, "position/h-sl-ft"),
        agl_ft=get(fdm, "position/h-agl-ft"),
        airspeed_kt=get(fdm, "velocities/vc-kts"),
        groundspeed_fps=get(fdm, "velocities/vg-fps"),
        pitch_deg=get(fdm, "attitude/theta-deg"),
        bank_deg=get(fdm, "attitude/phi-deg"),
        heading_deg=get(fdm, "attitude/psi-deg"),
        latitude_deg=lat,
        longitude_deg=lon,
        distance_from_start_ft=latlon_distance_ft(start_lat, start_lon, lat, lon),
    )


def get(fdm, prop: str) -> float:
    try:
        return float(fdm.get_property_value(prop))
    except Exception:
        return float("nan")


def latlon_distance_ft(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    radius_ft = 20_925_524.9
    p1 = math.radians(lat1)
    p2 = math.radians(lat2)
    dp = math.radians(lat2 - lat1)
    dl = math.radians(lon2 - lon1)
    a = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return radius_ft * 2 * math.atan2(math.sqrt(a), math.sqrt(max(0.0, 1.0 - a)))


def write_markdown(path: pathlib.Path, report: dict) -> None:
    final = report.get("final") or {}
    lines = [
        "# JSBSim C172 Feasibility Probe",
        "",
        f"- JSBSim version: {report['jsbsim_version']}",
        f"- Aircraft: `{report['aircraft']}`",
        f"- Duration: {report['duration_s']} s",
        f"- Samples: {report['sample_count']}",
        f"- Max airspeed: {report['max_airspeed_kt']:.1f} kt",
        f"- Max AGL: {report['max_agl_ft']:.1f} ft",
        f"- Max bank: {report['max_abs_bank_deg']:.1f} deg",
        f"- Final phase: {final.get('phase', '')}",
        f"- Final altitude: {final.get('altitude_ft', 0):.1f} ft",
        f"- Final heading: {final.get('heading_deg', 0):.1f} deg",
        "",
        "## Recommendation",
        "",
        report["integration_recommendation"],
        "",
        "## Limitations",
        "",
    ]
    lines.extend(f"- {item}" for item in report["limitations"])
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
