#!/usr/bin/env python3
"""Interactive JSON-lines JSBSim sidecar for Unity Editor validation.

Unity keeps ownership of the simulator scene and sends per-frame control
commands to this process. The sidecar advances JSBSim and returns the latest
pose/telemetry sample as one JSON object per line. This is intentionally an
Editor development bridge, not an Android/Quest runtime dependency.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from dataclasses import asdict, dataclass
from typing import Any


@dataclass
class LiveControl:
    throttle: float = 0.0
    elevator: float = 0.0
    aileron: float = 0.0
    rudder: float = 0.0
    flaps: float = 0.0
    trim: float = 0.0
    mixture: float = 1.0
    carb_heat: float = 0.0
    left_brake: float = 0.0
    right_brake: float = 0.0


@dataclass
class LiveSample:
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
    ground_speed_kt: float
    throttle: float
    elevator: float
    aileron: float
    rudder: float
    flaps: float
    trim: float
    mixture: float
    left_brake: float
    right_brake: float


class JSBSimLiveSession:
    def __init__(self, jsbsim: Any, aircraft: str, reset: str, fixed_dt: float, heading_deg: float) -> None:
        self.jsbsim = jsbsim
        self.aircraft = aircraft
        self.reset_name = reset
        self.fixed_dt = fixed_dt
        self.heading_deg = heading_deg
        self.fdm: Any | None = None
        self.east_m = 0.0
        self.north_m = 0.0
        self.start_agl_ft = 0.0
        self.reset(heading_deg)

    def reset(self, heading_deg: float | None = None) -> LiveSample:
        self.heading_deg = self.heading_deg if heading_deg is None else heading_deg
        self.fdm = self.jsbsim.FGFDMExec(None)
        self.fdm.set_debug_level(0)
        self.fdm.set_dt(self.fixed_dt)
        if not self.fdm.load_model(self.aircraft):
            raise RuntimeError(f"Could not load JSBSim aircraft model {self.aircraft!r}")
        if self.reset_name and not self.fdm.load_ic(self.reset_name, True):
            raise RuntimeError(f"Could not load JSBSim reset file {self.reset_name!r}")

        safe_set(self.fdm, "ic/psi-true-deg", self.heading_deg)
        safe_set(self.fdm, "ic/theta-deg", 0.0)
        safe_set(self.fdm, "ic/phi-deg", 0.0)
        safe_set(self.fdm, "ic/vc-kts", 0.0)
        self.fdm.run_ic()
        start_engine(self.fdm)
        self.east_m = 0.0
        self.north_m = 0.0
        self.start_agl_ft = get(self.fdm, "position/h-agl-ft")
        return self.read_sample(LiveControl())

    def step(self, requested_dt: float, control: LiveControl) -> LiveSample:
        if self.fdm is None:
            raise RuntimeError("JSBSim session is not initialized.")

        remaining = max(0.0, requested_dt)
        if remaining <= 0.0:
            return self.read_sample(control)

        while remaining > 1e-6:
            sub_dt = min(self.fixed_dt, remaining)
            self.fdm.set_dt(sub_dt)
            set_controls(self.fdm, control)
            self.fdm.run()
            self.integrate_position(sub_dt)
            remaining -= sub_dt
        return self.read_sample(control)

    def integrate_position(self, dt: float) -> None:
        assert self.fdm is not None
        heading = get(self.fdm, "attitude/psi-deg")
        speed_fps = get(self.fdm, "velocities/vg-fps")
        if not math.isfinite(speed_fps) or speed_fps <= 0.0:
            speed_fps = get(self.fdm, "velocities/vc-kts") * 1.68781
        distance_m = max(0.0, speed_fps) * 0.3048 * dt
        heading_rad = math.radians(heading)
        self.east_m += math.sin(heading_rad) * distance_m
        self.north_m += math.cos(heading_rad) * distance_m

    def read_sample(self, control: LiveControl) -> LiveSample:
        assert self.fdm is not None
        agl = get(self.fdm, "position/h-agl-ft")
        ground_speed_kt = get(self.fdm, "velocities/vg-fps") / 1.68781
        return LiveSample(
            time_s=get(self.fdm, "simulation/sim-time-sec"),
            east_m=self.east_m,
            north_m=self.north_m,
            agl_ft=agl,
            altitude_delta_ft=agl - self.start_agl_ft,
            airspeed_kt=get(self.fdm, "velocities/vc-kts"),
            vertical_speed_fpm=get(self.fdm, "velocities/h-dot-fps") * 60.0,
            pitch_deg=get(self.fdm, "attitude/theta-deg"),
            bank_deg=get(self.fdm, "attitude/phi-deg"),
            heading_deg=get(self.fdm, "attitude/psi-deg"),
            ground_speed_kt=ground_speed_kt if math.isfinite(ground_speed_kt) else 0.0,
            throttle=control.throttle,
            elevator=control.elevator,
            aileron=control.aileron,
            rudder=control.rudder,
            flaps=control.flaps,
            trim=control.trim,
            mixture=control.mixture,
            left_brake=control.left_brake,
            right_brake=control.right_brake,
        )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--aircraft", default="c172x")
    parser.add_argument("--reset", default="reset00")
    parser.add_argument("--dt", type=float, default=1.0 / 120.0)
    parser.add_argument("--heading-deg", type=float, default=90.0)
    args = parser.parse_args()

    try:
        import jsbsim  # type: ignore
    except Exception as exc:  # noqa: BLE001
        emit({"type": "error", "status": "FAIL", "message": f"JSBSim import failed: {exc}"})
        return 2

    try:
        session = JSBSimLiveSession(jsbsim, args.aircraft, args.reset, args.dt, args.heading_deg)
        emit(
            {
                "type": "hello",
                "status": "ready",
                "jsbsim_version": getattr(jsbsim, "__version__", "unknown"),
                "aircraft": args.aircraft,
                "reset": args.reset,
                "fixed_dt": args.dt,
                "sample": asdict(session.read_sample(LiveControl())),
            }
        )
    except Exception as exc:  # noqa: BLE001
        emit({"type": "error", "status": "FAIL", "message": str(exc)})
        return 3

    for raw_line in sys.stdin:
        raw_line = raw_line.strip()
        if not raw_line:
            continue
        try:
            request = json.loads(raw_line)
            command = request.get("command", "step")
            if command == "shutdown":
                emit({"type": "shutdown", "status": "ok"})
                return 0
            if command == "reset":
                heading = request.get("heading_deg")
                sample = session.reset(float(heading) if heading is not None else None)
                emit({"type": "reset", "status": "ok", "sample": asdict(sample)})
                continue
            if command != "step":
                emit({"type": "error", "status": "FAIL", "message": f"Unknown command {command!r}"})
                continue
            control = parse_control(request.get("control") or {})
            sample = session.step(float(request.get("dt", args.dt)), control)
            emit({"type": "sample", "status": "ok", "sample": asdict(sample)})
        except Exception as exc:  # noqa: BLE001
            emit({"type": "error", "status": "FAIL", "message": str(exc)})
    return 0


def parse_control(data: dict[str, Any]) -> LiveControl:
    return LiveControl(
        throttle=clamp(float(data.get("throttle", 0.0)), 0.0, 1.0),
        elevator=clamp(float(data.get("elevator", 0.0)), -1.0, 1.0),
        aileron=clamp(float(data.get("aileron", 0.0)), -1.0, 1.0),
        rudder=clamp(float(data.get("rudder", 0.0)), -1.0, 1.0),
        flaps=clamp(float(data.get("flaps", 0.0)), 0.0, 1.0),
        trim=clamp(float(data.get("trim", 0.0)), -1.0, 1.0),
        mixture=clamp(float(data.get("mixture", 1.0)), 0.0, 1.0),
        carb_heat=clamp(float(data.get("carb_heat", 0.0)), 0.0, 1.0),
        left_brake=clamp(float(data.get("left_brake", 0.0)), 0.0, 1.0),
        right_brake=clamp(float(data.get("right_brake", 0.0)), 0.0, 1.0),
    )


def set_controls(fdm: Any, c: LiveControl) -> None:
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


def start_engine(fdm: Any) -> None:
    safe_set(fdm, "propulsion/magneto_cmd", 3.0)
    safe_set(fdm, "propulsion/starter_cmd", 1.0)
    safe_set(fdm, "propulsion/active_engine", 0.0)
    safe_set(fdm, "propulsion/engine/set-running", 1.0)
    safe_set(fdm, "fcs/mixture-cmd-norm", 1.0)
    safe_set(fdm, "fcs/mixture-cmd-norm[0]", 1.0)
    safe_set(fdm, "fcs/throttle-cmd-norm", 0.25)
    safe_set(fdm, "fcs/throttle-cmd-norm[0]", 0.25)


def safe_set(fdm: Any, prop: str, value: float) -> None:
    try:
        fdm.set_property_value(prop, value)
    except Exception:
        pass


def get(fdm: Any, prop: str) -> float:
    try:
        return float(fdm.get_property_value(prop))
    except Exception:
        return float("nan")


def clamp(value: float, low: float, high: float) -> float:
    return min(high, max(low, value))


def emit(payload: dict[str, Any]) -> None:
    print(json.dumps(payload, separators=(",", ":")), flush=True)


if __name__ == "__main__":
    raise SystemExit(main())
