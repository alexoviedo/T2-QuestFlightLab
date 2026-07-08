# Aircraft Asset Pipeline

## Goal

Replace the current blocky/imported placeholder look with a reproducible C172-style aircraft/cockpit pipeline that is good enough for a credible Quest visual demo while remaining lightweight and owned or clearly sourced.

## Current State

- Current visual QA can load an imported C172 placeholder from `Resources/QuestFlightLab/ImportedAssets/Cessna172KogThorns/cessna172.glb`.
- Runtime repair also generates self-owned C172-style cockpit/exterior cues.
- The current asset is still a placeholder and must not be presented as final C172 fidelity.

## Preferred Pipeline

1. **Blender procedural/artist pipeline**
   - Use `tools/generate_c172_style_assets.py` to seed a Blender-generated high-wing trainer model.
   - Iterate cockpit shell, transparent windows, windshield frame, panel, yokes, pedals, throttle/mixture controls, seats, wing/cowl references, and exterior silhouette.
   - Export GLB/FBX, optimize meshes/materials/textures, and inspect through visual QA.

2. **OpenVSP geometry gate**
   - Install OpenVSP and generate a parametric high-wing trainer exterior.
   - Export OBJ/STL/GLTF-friendly geometry.
   - Use it as an exterior reference or import candidate, not as the cockpit-detail solution.

3. **Unity import and Quest optimization**
   - Keep triangle/material/texture counts small.
   - Use transparent materials only where needed for windows.
   - Maintain a named pilot eye reference and left-seat coordinate convention.
   - Validate with `scripts/run_visual_qa.ps1`, PlayMode tests, editor scenarios, and APK build before making a new asset default.

## Script

Run a manifest-only check without Blender:

```powershell
python .\tools\generate_c172_style_assets.py --dry-run --output <artifact-root>\c172_style_baseline.glb
```

Run with Blender when installed:

```powershell
blender --background --python .\tools\generate_c172_style_assets.py -- --output <artifact-root>\c172_style_baseline.glb
```

The script creates a C172-style high-wing trainer baseline with:

- fuselage/cowl/tail/prop/gear,
- high wing and struts,
- cockpit shell and transparent windows,
- left-seat pilot eye reference,
- dashboard, yokes, pedals, throttle/mixture controls,
- small Quest-oriented material set.

## Asset Rules

- Do not commit large raw downloads.
- Do not commit unclear-license production assets.
- Prefer generated Blender/OpenVSP sources and optimized Unity-ready exports.
- Record source, license, author/tool, import date, and optimization decisions in `docs/ASSET_SOURCES.md`.
- Treat any personal placeholder as temporary and non-redistributable unless license terms are clear.

## Next Gate

Install/use Blender and OpenVSP, generate or export one improved C172-style exterior/cockpit candidate, import it into Unity, and compare it to the current placeholder with the visual QA contact sheet.
