#!/usr/bin/env python3
"""Generate a lightweight C172-style visual asset with Blender when available.

Run with Blender:

  blender --background --python tools/generate_c172_style_assets.py -- --output out/c172_style_baseline.glb

Run with normal Python to produce a pipeline manifest only:

  python tools/generate_c172_style_assets.py --dry-run --output out/c172_style_baseline.glb

This script is intentionally a pipeline seed. It creates project-owned
placeholder geometry that can be iterated or replaced by OpenVSP/artist assets.
It does not claim final C172 shape fidelity.
"""

from __future__ import annotations

import argparse
import json
import math
import pathlib
import sys
from dataclasses import asdict, dataclass
from datetime import datetime, timezone


@dataclass
class PipelineManifest:
    generated_utc: str
    output: str
    blender_available: bool
    dry_run: bool
    asset_strategy: str
    components: list[str]
    limitations: list[str]


def parse_args(argv: list[str]) -> argparse.Namespace:
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True, help="Target .glb/.gltf path when Blender is available.")
    parser.add_argument("--manifest", default="", help="Optional manifest path. Defaults next to output.")
    parser.add_argument("--dry-run", action="store_true", help="Write manifest without requiring Blender.")
    parser.add_argument("--require-blender", action="store_true", help="Fail if bpy is unavailable.")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    output = pathlib.Path(args.output)
    manifest_path = pathlib.Path(args.manifest) if args.manifest else output.with_suffix(".manifest.json")
    output.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)

    try:
        import bpy  # type: ignore
    except Exception as exc:  # noqa: BLE001
        manifest = build_manifest(output, blender_available=False, dry_run=True)
        manifest_path.write_text(json.dumps(asdict(manifest), indent=2), encoding="utf-8")
        if args.require_blender and not args.dry_run:
            print(f"Blender Python module is unavailable: {exc}", file=sys.stderr)
            return 2
        print(json.dumps(asdict(manifest), indent=2))
        return 0

    if args.dry_run:
        manifest = build_manifest(output, blender_available=True, dry_run=True)
        manifest_path.write_text(json.dumps(asdict(manifest), indent=2), encoding="utf-8")
        print(json.dumps(asdict(manifest), indent=2))
        return 0

    build_scene(bpy)
    export_asset(bpy, output)
    manifest = build_manifest(output, blender_available=True, dry_run=False)
    manifest_path.write_text(json.dumps(asdict(manifest), indent=2), encoding="utf-8")
    print(json.dumps(asdict(manifest), indent=2))
    return 0


def build_manifest(output: pathlib.Path, blender_available: bool, dry_run: bool) -> PipelineManifest:
    return PipelineManifest(
        generated_utc=datetime.now(timezone.utc).isoformat(),
        output=str(output),
        blender_available=blender_available,
        dry_run=dry_run,
        asset_strategy="Blender-generated C172-style high-wing trainer baseline with OpenVSP/artist replacement path.",
        components=[
            "high-wing exterior silhouette",
            "fuselage/cowl/tail/struts/gear",
            "cockpit shell with transparent windshield and side-window openings",
            "left-seat pilot eye reference",
            "instrument panel, yokes, pedals, throttle/mixture controls",
            "Quest-sized material set: white paint, dark cockpit plastic, glass, metal, tires",
        ],
        limitations=[
            "Not a certified or final C172 model.",
            "Not generated from official Cessna CAD.",
            "Not an aerodynamic mesh; use OpenVSP/JSBSim for engineering geometry and physics references.",
            "Keep output optimized before committing to Unity Assets.",
        ],
    )


def build_scene(bpy) -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()

    white = material(bpy, "trainer_white", (0.82, 0.84, 0.80, 1.0), 0.05, 0.35)
    blue = material(bpy, "blue_trim", (0.05, 0.16, 0.62, 1.0), 0.05, 0.32)
    dark = material(bpy, "cockpit_dark_plastic", (0.015, 0.016, 0.018, 1.0), 0.0, 0.52)
    panel = material(bpy, "instrument_panel_matte", (0.04, 0.043, 0.045, 1.0), 0.0, 0.28)
    metal = material(bpy, "brushed_metal", (0.52, 0.53, 0.50, 1.0), 0.6, 0.38)
    tire = material(bpy, "black_rubber", (0.005, 0.005, 0.004, 1.0), 0.0, 0.18)
    glass = material(bpy, "slightly_blue_glass", (0.48, 0.72, 0.94, 0.34), 0.0, 0.92, alpha=0.34)
    cream = material(bpy, "interior_cream", (0.64, 0.61, 0.54, 1.0), 0.0, 0.34)

    root = empty(bpy, "C172StyleGeneratedRoot")
    exterior = empty(bpy, "Exterior", root)
    cockpit = empty(bpy, "Cockpit", root)

    add_cylinder(bpy, "Fuselage", exterior, (0, 0, 0.95), (0, math.pi / 2, 0), (0.78, 0.78, 6.5), white, vertices=48)
    add_cylinder(bpy, "EngineCowl", exterior, (-3.55, 0, 0.98), (0, math.pi / 2, 0), (0.72, 0.72, 1.05), white, vertices=48)
    add_cylinder(bpy, "Spinner", exterior, (-4.16, 0, 0.98), (0, math.pi / 2, 0), (0.42, 0.42, 0.38), metal, vertices=32)
    add_cube(bpy, "CabinBox", exterior, (-0.75, 0, 1.47), (0, 0, 0), (1.7, 1.62, 1.15), white)
    add_cube(bpy, "HighWing", exterior, (-0.8, 0, 2.35), (0, 0, 0), (5.1, 11.2, 0.18), white)
    add_cube(bpy, "BlueWingStripe", exterior, (-0.85, 0, 2.46), (0, 0, 0), (5.0, 10.7, 0.035), blue)
    add_cube(bpy, "LeftStrut", exterior, (-0.95, -3.0, 1.7), (0, 0.0, -0.34), (0.08, 3.35, 0.08), metal)
    add_cube(bpy, "RightStrut", exterior, (-0.95, 3.0, 1.7), (0, 0.0, 0.34), (0.08, 3.35, 0.08), metal)
    add_cube(bpy, "VerticalTail", exterior, (2.7, 0, 1.82), (0, 0, 0), (0.18, 0.12, 1.32), white)
    add_cube(bpy, "HorizontalTail", exterior, (2.68, 0, 1.42), (0, 0, 0), (1.3, 4.0, 0.11), white)
    add_cube(bpy, "PropBladeVertical", exterior, (-4.4, 0, 0.98), (0, 0, 0), (0.07, 0.12, 1.9), dark)
    add_cube(bpy, "PropBladeHorizontal", exterior, (-4.4, 0, 0.98), (0, 0, math.pi / 2), (0.07, 0.12, 1.9), dark)

    for name, loc in (
        ("NoseGear", (-3.35, 0, 0.2)),
        ("LeftMainGear", (-0.25, -0.92, 0.18)),
        ("RightMainGear", (-0.25, 0.92, 0.18)),
    ):
        add_cylinder(bpy, name + "Tire", exterior, loc, (math.pi / 2, 0, 0), (0.28, 0.28, 0.16), tire, vertices=28)
        add_cube(bpy, name + "Fork", exterior, (loc[0], loc[1], loc[2] + 0.42), (0, 0, 0), (0.07, 0.07, 0.82), metal)

    add_cube(bpy, "WindshieldGlass", cockpit, (-1.42, 0, 1.88), (0, 0.42, 0), (0.08, 1.42, 0.72), glass)
    add_cube(bpy, "LeftSideWindowGlass", cockpit, (-0.58, -0.84, 1.76), (0, 0, 0), (1.1, 0.045, 0.62), glass)
    add_cube(bpy, "RightSideWindowGlass", cockpit, (-0.58, 0.84, 1.76), (0, 0, 0), (1.1, 0.045, 0.62), glass)
    add_cube(bpy, "Dashboard", cockpit, (-1.74, 0, 1.22), (0, 0, 0), (0.28, 1.34, 0.38), panel)
    add_cube(bpy, "GlareShield", cockpit, (-1.88, 0, 1.48), (0, -0.08, 0), (0.64, 1.45, 0.12), dark)
    add_cube(bpy, "PilotSeat", cockpit, (0.18, -0.42, 0.82), (0, 0, 0), (0.55, 0.5, 0.38), cream)
    add_cube(bpy, "PilotSeatBack", cockpit, (0.4, -0.42, 1.22), (0, -0.18, 0), (0.14, 0.52, 0.72), cream)
    add_cube(bpy, "CoPilotSeat", cockpit, (0.18, 0.42, 0.82), (0, 0, 0), (0.55, 0.5, 0.38), cream)
    add_cube(bpy, "CoPilotSeatBack", cockpit, (0.4, 0.42, 1.22), (0, -0.18, 0), (0.14, 0.52, 0.72), cream)
    add_yoke(bpy, cockpit, "PilotYoke", (-1.42, -0.42, 1.12), dark, metal)
    add_yoke(bpy, cockpit, "CoPilotYoke", (-1.42, 0.42, 1.12), dark, metal)
    add_cube(bpy, "Throttle", cockpit, (-1.9, -0.08, 1.08), (0, 0, 0), (0.18, 0.06, 0.06), dark)
    add_cube(bpy, "Mixture", cockpit, (-1.9, 0.08, 1.08), (0, 0, 0), (0.18, 0.06, 0.06), blue)
    add_cube(bpy, "PilotEyeReferenceDoNotRender", cockpit, (-0.62, -0.42, 1.52), (0, 0, 0), (0.08, 0.08, 0.08), material(bpy, "eye_ref_magenta", (1, 0, 1, 1), 0, 0.2))

    for i in range(6):
        add_cylinder(bpy, f"Gauge_{i}", cockpit, (-1.92, -0.48 + i * 0.19, 1.28), (0, math.pi / 2, 0), (0.075, 0.075, 0.018), metal, vertices=32)


def add_yoke(bpy, parent, name: str, loc: tuple[float, float, float], dark, metal) -> None:
    yoke = empty(bpy, name, parent)
    add_cube(bpy, name + "_Column", yoke, loc, (0, 0.34, 0), (0.055, 0.055, 0.58), metal)
    add_cube(bpy, name + "_GripLeft", yoke, (loc[0] - 0.28, loc[1], loc[2] + 0.16), (0, 0, 0.25), (0.09, 0.055, 0.46), dark)
    add_cube(bpy, name + "_GripRight", yoke, (loc[0] + 0.02, loc[1], loc[2] + 0.16), (0, 0, -0.25), (0.09, 0.055, 0.46), dark)
    add_cube(bpy, name + "_Crossbar", yoke, (loc[0] - 0.13, loc[1], loc[2] + 0.28), (0, 0, math.pi / 2), (0.06, 0.055, 0.36), dark)


def export_asset(bpy, output: pathlib.Path) -> None:
    if output.suffix.lower() in (".glb", ".gltf"):
        bpy.ops.export_scene.gltf(filepath=str(output), export_format="GLB" if output.suffix.lower() == ".glb" else "GLTF_SEPARATE")
    elif output.suffix.lower() == ".fbx":
        bpy.ops.export_scene.fbx(filepath=str(output))
    else:
        raise ValueError(f"Unsupported output format: {output.suffix}")


def material(bpy, name: str, color: tuple[float, float, float, float], metallic: float, roughness: float, alpha: float = 1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    principled = mat.node_tree.nodes.get("Principled BSDF")
    if principled:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness
        principled.inputs["Alpha"].default_value = alpha
    if alpha < 1.0:
        mat.blend_method = "BLEND"
        mat.use_screen_refraction = True
    return mat


def empty(bpy, name: str, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    if parent:
        obj.parent = parent
    return obj


def add_cube(bpy, name: str, parent, loc, rot, scale, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    if parent:
        obj.parent = parent
    obj.data.materials.append(mat)
    return obj


def add_cylinder(bpy, name: str, parent, loc, rot, scale, mat, vertices: int = 32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=1, depth=1, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    if parent:
        obj.parent = parent
    obj.data.materials.append(mat)
    return obj


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
