#!/usr/bin/env python3
"""Run matched-control JSBSim C172 profiles and compare to Unity scenario samples.

This is an offline calibration aid. It intentionally keeps JSBSim outside the
Unity/Quest runtime while making the reference profiles closer to the Unity
scenario schedules than the older broad open-loop probe.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import pathlib
import statistics
import sys
from dataclasses import asdict, dataclass
from typing import Callable


@dataclass
class Control:
    throttle: float = 0.2
    aileron: float = 0.0
    elevator: float = 0.0
    rudder: float = 0.0
    flaps: float = 0.0
    trim: float = 0.0
    mixture: float = 1.0
    left_brake: float = 0.0
    right_brake: float = 0.0


@dataclass
class ScenarioProfile:
    scenario_id: str
    label: str
    duration_s: float
    initial_airspeed_kt: float
    initial_agl_ft: float
    start_on_runway: bool
    schedule_name: str


@dataclass
class JsSample:
    scenario_id: str
    time_s: float
    throttle: float
    aileron: float
    elevator: float
    rudder: float
    flaps: float
    trim: float
    airspeed_kt: float
    altitude_delta_ft: float
    agl_ft: float
    vertical_speed_fpm: float
    pitch_deg: float
    bank_deg: float
    heading_delta_deg: float
    heading_deg: float


PROFILES = [
    ScenarioProfile("takeoff_roll_to_vr", "takeoff roll", 17.0, 0.0, 0.0, True, "takeoff_roll_to_vr"),
    ScenarioProfile("rotation_climb_to_altitude", "rotation/climb", 28.0, 0.0, 0.0, True, "rotation_climb_to_altitude"),
    ScenarioProfile("vy_climb_stabilization", "Vy climb", 18.0, 74.0, 600.0, False, "vy_climb_stabilization"),
    ScenarioProfile("shallow_left_right_turns", "shallow turns", 18.0, 82.0, 1000.0, False, "shallow_left_right_turns"),
    ScenarioProfile("stabilized_final_approach", "stabilized approach", 34.0, 68.0, 480.0, False, "stable_approach"),
    ScenarioProfile("go_around_sequence", "go-around", 32.0, 62.0, 240.0, False, "goaround_sequence"),
]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--unity-scenario-json", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--aircraft", default="c172x")
    parser.add_argument("--reset", default="reset00")
    parser.add_argument("--dt", type=float, default=1.0 / 120.0)
    parser.add_argument("--sample-step", type=float, default=0.1)
    args = parser.parse_args()

    try:
        import jsbsim  # type: ignore
    except Exception as exc:  # noqa: BLE001
        print(f"JSBSim import failed: {exc}", file=sys.stderr)
        print("Install with: python -m pip install jsbsim", file=sys.stderr)
        return 2

    out = pathlib.Path(args.output_dir)
    out.mkdir(parents=True, exist_ok=True)

    unity_report = json.loads(pathlib.Path(args.unity_scenario_json).read_text(encoding="utf-8"))
    unity_by_id = {scenario["id"]: scenario for scenario in unity_report.get("scenarios", [])}

    js_samples: list[JsSample] = []
    rows = []
    for profile in PROFILES:
        if profile.scenario_id not in unity_by_id:
            rows.append({"scenario_id": profile.scenario_id, "error": "missing Unity scenario"})
            continue

        scenario_samples = run_jsbsim_profile(
            jsbsim,
            profile,
            aircraft=args.aircraft,
            reset=args.reset,
            dt=args.dt,
            sample_step=args.sample_step,
        )
        js_samples.extend(scenario_samples)
        rows.append(compare_profile(profile, scenario_samples, unity_by_id[profile.scenario_id]))

    aggregate = aggregate_rows(rows)
    report = {
        "tool": "matched-control JSBSim/Unity comparator",
        "classification": "reference_oracle_only",
        "jsbsim_version": getattr(jsbsim, "__version__", "unknown"),
        "aircraft": args.aircraft,
        "reset": args.reset,
        "unity_scenario_json": str(pathlib.Path(args.unity_scenario_json)),
        "profiles": [asdict(p) for p in PROFILES],
        "rows": rows,
        "aggregate": aggregate,
        "limitations": [
            "JSBSim is an offline reference oracle only; Unity runtime is not JSBSim-backed.",
            "The control schedules are matched by scenario intent but the two simulators still have different aircraft definitions, coordinate frames, and initialization details.",
            "This does not claim final C172 fidelity, production training suitability, or Quest runtime performance.",
        ],
    }

    (out / "matched_jsbsim_unity_comparison.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_samples_csv(out / "matched_jsbsim_samples.csv", js_samples)
    write_rows_csv(out / "matched_jsbsim_unity_comparison.csv", rows)
    write_markdown(out / "matched_jsbsim_unity_comparison.md", report)
    print(json.dumps(report, indent=2))
    return 0


def run_jsbsim_profile(jsbsim, profile: ScenarioProfile, aircraft: str, reset: str, dt: float, sample_step: float) -> list[JsSample]:
    fdm = jsbsim.FGFDMExec(None)
    fdm.set_debug_level(0)
    fdm.set_dt(dt)
    if not fdm.load_model(aircraft):
        raise RuntimeError(f"Could not load JSBSim aircraft model {aircraft!r}")
    if reset and not fdm.load_ic(reset, True):
        raise RuntimeError(f"Could not load reset file {reset!r}")

    safe_set(fdm, "ic/psi-true-deg", 78.0)
    safe_set(fdm, "ic/theta-deg", 0.0 if profile.start_on_runway else 2.0)
    safe_set(fdm, "ic/phi-deg", 0.0)
    safe_set(fdm, "ic/vc-kts", profile.initial_airspeed_kt)
    if not profile.start_on_runway:
        safe_set(fdm, "ic/h-agl-ft", profile.initial_agl_ft)
    fdm.run_ic()
    start_engine(fdm)
    start_agl = get(fdm, "position/h-agl-ft")
    start_heading = get(fdm, "attitude/psi-deg")

    samples: list[JsSample] = []
    next_sample = 0.0
    while fdm.get_sim_time() < profile.duration_s:
        t = fdm.get_sim_time()
        control = control_for_profile(profile.schedule_name, t)
        set_controls(fdm, control)
        fdm.run()
        if fdm.get_sim_time() + 1e-6 >= next_sample:
            samples.append(read_js_sample(fdm, profile.scenario_id, control, start_agl, start_heading))
            next_sample += sample_step
    return samples


def control_for_profile(name: str, time: float) -> Control:
    c = Control()
    c.mixture = 1.0
    if name == "takeoff_roll_to_vr":
        c.throttle = 1.0
        c.rudder = math.sin(time * 0.55) * 0.06
    elif name == "rotation_climb_to_altitude":
        c.throttle = 1.0
        c.rudder = math.sin(time * 0.42) * 0.05
        c.elevator = 0.27 if time > 11.5 else 0.16 if time > 8.5 else 0.0
        c.trim = 0.1
    elif name == "vy_climb_stabilization":
        c.throttle = 0.92
        c.elevator = 0.04
        c.trim = 0.16
    elif name == "shallow_left_right_turns":
        c.throttle = 0.72
        c.elevator = 0.1
        c.aileron = 0.16 if time < 2.2 else 0.02 if time < 7.0 else -0.2 if time < 9.2 else -0.02 if time < 14.5 else 0.1
        c.rudder = c.aileron * 0.25
    elif name == "stable_approach":
        apply_approach_controls(c, time, "stable")
    elif name == "goaround_sequence":
        apply_approach_controls(c, time, "goaround_sequence")
    return c


def apply_approach_controls(c: Control, time: float, profile: str) -> None:
    c.mixture = 1.0
    c.flaps = 1.0
    c.trim = 0.08
    c.rudder = 0.02
    goaround_now = "goaround" in profile and time > 6.0
    if goaround_now:
        c.throttle = 1.0
        c.elevator = 0.16 if time < 10.0 else 0.08
        c.trim = 0.12
        c.flaps = 0.66 if time < 10.0 else 0.33 if time < 16.0 else 0.0
        c.rudder = 0.05
        return
    if profile == "stable":
        c.throttle = 0.42
        c.elevator = 0.03
        c.trim = 0.07
        c.flaps = 1.0
    else:
        c.throttle = 0.42
        c.elevator = 0.03
        c.flaps = 1.0


def set_controls(fdm, c: Control) -> None:
    for prop in ("fcs/throttle-cmd-norm", "fcs/throttle-cmd-norm[0]", "fcs/throttle-pos-norm", "fcs/throttle-pos-norm[0]"):
        safe_set(fdm, prop, c.throttle)
    for prop in ("fcs/mixture-cmd-norm", "fcs/mixture-cmd-norm[0]", "fcs/mixture-pos-norm", "fcs/mixture-pos-norm[0]"):
        safe_set(fdm, prop, c.mixture)
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
    safe_set(fdm, "fcs/left-brake-cmd-norm", c.left_brake)
    safe_set(fdm, "fcs/right-brake-cmd-norm", c.right_brake)
    safe_set(fdm, "fcs/center-brake-cmd-norm", max(c.left_brake, c.right_brake))


def start_engine(fdm) -> None:
    safe_set(fdm, "propulsion/magneto_cmd", 3.0)
    safe_set(fdm, "propulsion/starter_cmd", 1.0)
    safe_set(fdm, "propulsion/active_engine", 0.0)
    safe_set(fdm, "propulsion/engine/set-running", 1.0)
    safe_set(fdm, "fcs/mixture-cmd-norm", 1.0)
    safe_set(fdm, "fcs/mixture-cmd-norm[0]", 1.0)
    safe_set(fdm, "fcs/throttle-cmd-norm", 0.25)
    safe_set(fdm, "fcs/throttle-cmd-norm[0]", 0.25)


def read_js_sample(fdm, scenario_id: str, c: Control, start_agl: float, start_heading: float) -> JsSample:
    heading = get(fdm, "attitude/psi-deg")
    agl = get(fdm, "position/h-agl-ft")
    return JsSample(
        scenario_id=scenario_id,
        time_s=get(fdm, "simulation/sim-time-sec"),
        throttle=c.throttle,
        aileron=c.aileron,
        elevator=c.elevator,
        rudder=c.rudder,
        flaps=c.flaps,
        trim=c.trim,
        airspeed_kt=get(fdm, "velocities/vc-kts"),
        altitude_delta_ft=agl - start_agl,
        agl_ft=agl,
        vertical_speed_fpm=get(fdm, "velocities/h-dot-fps") * 60.0,
        pitch_deg=get(fdm, "attitude/theta-deg"),
        bank_deg=get(fdm, "attitude/phi-deg"),
        heading_delta_deg=abs(delta_angle(start_heading, heading)),
        heading_deg=heading,
    )


def compare_profile(profile: ScenarioProfile, js_samples: list[JsSample], unity_scenario: dict) -> dict:
    unity_samples = unity_scenario.get("samples", [])
    pairs = pair_samples(js_samples, unity_samples)
    row = {
        "scenario_id": profile.scenario_id,
        "label": profile.label,
        "sample_pairs": len(pairs),
        "classification": "matched_control_reference",
    }
    for key in ("airspeed_kt", "altitude_delta_ft", "vertical_speed_fpm", "pitch_deg", "bank_deg", "heading_delta_deg"):
        diffs = [unity_value(u, key) - getattr(j, key) for j, u in pairs]
        row[f"{key}_rmse"] = rmse(diffs)
        row[f"{key}_mean_error"] = statistics.fmean(diffs) if diffs else 0.0
        row[f"{key}_max_abs_error"] = max((abs(d) for d in diffs), default=0.0)
    if pairs:
        final_js, final_unity = pairs[-1]
        row["final_airspeed_error_kt"] = unity_value(final_unity, "airspeed_kt") - final_js.airspeed_kt
        row["final_altitude_delta_error_ft"] = unity_value(final_unity, "altitude_delta_ft") - final_js.altitude_delta_ft
        row["max_abs_bank_jsbsim_deg"] = max(abs(s.bank_deg) for s in js_samples)
        row["max_abs_bank_unity_deg"] = max(abs(unity_value(u, "bank_deg")) for _, u in pairs)
    row["weighted_error_score"] = (
        row.get("airspeed_kt_rmse", 0.0)
        + 0.035 * row.get("altitude_delta_ft_rmse", 0.0)
        + 0.015 * row.get("vertical_speed_fpm_rmse", 0.0)
        + 0.8 * row.get("pitch_deg_rmse", 0.0)
        + 0.8 * row.get("bank_deg_rmse", 0.0)
        + 0.25 * row.get("heading_delta_deg_rmse", 0.0)
    )
    return row


def pair_samples(js_samples: list[JsSample], unity_samples: list[dict]) -> list[tuple[JsSample, dict]]:
    if not js_samples or not unity_samples:
        return []
    pairs = []
    js_by_time = sorted(js_samples, key=lambda s: s.time_s)
    index = 0
    for unity in unity_samples:
        t = float(unity.get("timestamp", 0.0))
        while index + 1 < len(js_by_time) and abs(js_by_time[index + 1].time_s - t) <= abs(js_by_time[index].time_s - t):
            index += 1
        if abs(js_by_time[index].time_s - t) <= 0.08:
            pairs.append((js_by_time[index], unity))
    return pairs


def unity_value(sample: dict, key: str) -> float:
    flight = sample.get("flight", {})
    if key == "airspeed_kt":
        return float(flight.get("airspeedKts", 0.0))
    if key == "altitude_delta_ft":
        return float(flight.get("altitudeFt", 0.0)) - unity_value.first_altitude
    if key == "vertical_speed_fpm":
        return float(flight.get("verticalSpeedFpm", 0.0))
    if key == "pitch_deg":
        return float(flight.get("pitchDeg", 0.0))
    if key == "bank_deg":
        return float(flight.get("bankDeg", 0.0))
    if key == "heading_delta_deg":
        return abs(delta_angle(unity_value.first_heading, float(flight.get("headingDeg", 0.0))))
    return 0.0


def pair_samples_with_baseline(js_samples: list[JsSample], unity_samples: list[dict]) -> list[tuple[JsSample, dict]]:
    return pair_samples(js_samples, unity_samples)


def aggregate_rows(rows: list[dict]) -> dict:
    valid = [r for r in rows if "weighted_error_score" in r]
    return {
        "profile_count": len(valid),
        "mean_weighted_error_score": statistics.fmean([r["weighted_error_score"] for r in valid]) if valid else 0.0,
        "mean_airspeed_rmse_kt": statistics.fmean([r["airspeed_kt_rmse"] for r in valid]) if valid else 0.0,
        "mean_altitude_delta_rmse_ft": statistics.fmean([r["altitude_delta_ft_rmse"] for r in valid]) if valid else 0.0,
        "mean_bank_rmse_deg": statistics.fmean([r["bank_deg_rmse"] for r in valid]) if valid else 0.0,
    }


def rmse(values: list[float]) -> float:
    return math.sqrt(sum(v * v for v in values) / len(values)) if values else 0.0


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


def delta_angle(from_deg: float, to_deg: float) -> float:
    return (to_deg - from_deg + 540.0) % 360.0 - 180.0


def write_samples_csv(path: pathlib.Path, samples: list[JsSample]) -> None:
    if not samples:
        path.write_text("", encoding="utf-8")
        return
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(asdict(samples[0]).keys()))
        writer.writeheader()
        for sample in samples:
            writer.writerow(asdict(sample))


def write_rows_csv(path: pathlib.Path, rows: list[dict]) -> None:
    fieldnames = sorted({key for row in rows for key in row})
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def write_markdown(path: pathlib.Path, report: dict) -> None:
    lines = [
        "# Matched-Control JSBSim / Unity Comparison",
        "",
        f"- Classification: `{report['classification']}`",
        f"- JSBSim version: `{report['jsbsim_version']}`",
        f"- Aircraft: `{report['aircraft']}`",
        f"- Mean weighted error score: {report['aggregate']['mean_weighted_error_score']:.2f}",
        f"- Mean airspeed RMSE: {report['aggregate']['mean_airspeed_rmse_kt']:.1f} kt",
        f"- Mean altitude-delta RMSE: {report['aggregate']['mean_altitude_delta_rmse_ft']:.1f} ft",
        f"- Mean bank RMSE: {report['aggregate']['mean_bank_rmse_deg']:.1f} deg",
        "",
        "| Scenario | Samples | Airspeed RMSE | Altitude RMSE | Bank RMSE | Final Speed Err | Final Alt Err | Score |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for row in report["rows"]:
        if "weighted_error_score" not in row:
            lines.append(f"| `{row.get('scenario_id', '')}` | 0 | n/a | n/a | n/a | n/a | n/a | n/a |")
            continue
        lines.append(
            f"| `{row['scenario_id']}` | {row['sample_pairs']} | {row['airspeed_kt_rmse']:.1f} kt | "
            f"{row['altitude_delta_ft_rmse']:.1f} ft | {row['bank_deg_rmse']:.1f} deg | "
            f"{row.get('final_airspeed_error_kt', 0.0):+.1f} kt | {row.get('final_altitude_delta_error_ft', 0.0):+.1f} ft | "
            f"{row['weighted_error_score']:.2f} |"
        )
    lines.extend(["", "## Limitations", ""])
    lines.extend(f"- {item}" for item in report["limitations"])
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


# These mutable baselines are updated per pair-set inside compare_profile.
def _set_unity_baseline(unity_samples: list[dict]) -> None:
    first = unity_samples[0].get("flight", {}) if unity_samples else {}
    unity_value.first_altitude = float(first.get("altitudeFt", 0.0))
    unity_value.first_heading = float(first.get("headingDeg", 0.0))


original_compare_profile = compare_profile


def compare_profile(profile: ScenarioProfile, js_samples: list[JsSample], unity_scenario: dict) -> dict:  # type: ignore[no-redef]
    _set_unity_baseline(unity_scenario.get("samples", []))
    return original_compare_profile(profile, js_samples, unity_scenario)


unity_value.first_altitude = 0.0  # type: ignore[attr-defined]
unity_value.first_heading = 0.0  # type: ignore[attr-defined]


if __name__ == "__main__":
    raise SystemExit(main())
