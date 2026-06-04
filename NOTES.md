# Notes

## 2026-05-27 — Software bootstrap

- Restructured root: PCB files moved under `Hardware/` (MCU project, Controller project, Gerbers zips, assets.pretty footprint lib, fp-lib-table now scoped to `Hardware/MCU/`). Added `Hardware/Firmware/` placeholder.
- Created `Software/` Unity 6 LTS skeleton: `Packages/manifest.json` (URP, Input System, Test Framework, UI Toolkit), `ProjectSettings/ProjectVersion.txt`, `.editorconfig`, README.
- Authored runtime C# under `Software/Assets/Scripts/`:
  - `Input/`: `IIronInputSource` + `KeyboardIronInput` (V1) + `SerialIronInput` (parser only; runtime stub for firmware).
  - `Hardware/IronController` — applies pose, ramps tip temp.
  - `Sim/`: `ThermalModel`, `FluxModel`, `SolderState`, `SolderFlow`, `Pad`, `ToolBelt`.
  - `Gerber/`: `GerberParser` (RS-274X subset), `GerberMeshBuilder`, `GerberLoadUI`.
  - `Eval/`: `TestCase` (ScriptableObject), `Evaluator`, `EvalHUD` (UI Toolkit).
  - `App/`: `BootstrapScene`, `LatencyHUD`.
- EditMode tests cover ThermalModel (exponential), FluxModel (state transitions), SolderFlow (wetting + cold-gap), SolderState (transitions), GerberParser (apertures/coords/bbox), SerialIronInput (parse), Evaluator (pass + 3 failure modes).
- Added top-level `README.md`, `CLAUDE.md`, `docs/serial-protocol.md`.
- `.gitignore` extended with Unity ignores; `MCU-backups/` rule updated to `Hardware/MCU/MCU-backups/`.

### What's verified

- File layout matches the plan (`git status` shows expected renames + new files only).
- All runtime C# compiles in isolation against the listed UPM packages (no external NuGet refs). Not yet opened in Unity, so the actual Library/ asset import is unverified — first Unity open will create `.meta` files and `Workbench.unity` per `Software/Assets/Scenes/README.md`.

### Open follow-ups

- Author `Assets/Scenes/Workbench.unity` and prefabs (`VirtualIron`, `SamplePCB`, `Pad`) in Unity. Scene YAML deliberately not hand-written.
- Wire `GerberLoadUI` to a real native file dialog (e.g. StandaloneFileBrowser UPM package) once we approve adding it.
- `IronController` currently exposes a simple `MoveTowards` temp ramp; consider modeling the heater PID against tip mass once firmware exists.
- `ToolBelt` assumes a unit-square pad mesh in local XZ — generalize once real pad meshes land.
- `SolderFlow.Step` triggers `FluxModel.Step` and writes a uniform temperature from `Pad.Tick`; per-cell temperature gradients (for partial-pad heating) are a future refinement.

## 2026-06-04 — Flux Visualization

- Integrated flux and oxidation visualization directly into `PadVisualizer.cs` to prevent conflicting `MaterialPropertyBlock` overwrites on the same renderer.
- Renders cell-by-cell state mapping (`None` / copper heat ramp, `Oxidation`, `Cold`, `Active`, `Burnt`) on a dynamic bilinear-filtered `Texture2D` in the renderer property block.
- Updated `WorkbenchSceneBuilder.cs` to set a default white texture on generated pad materials to ensure Unity pre-compiles URP texture-sampling keywords (`_BASE_MAP`, `_MAIN_TEX`).
- Changed cold and active flux colors to a highly visible greyish silver (`#99A6B2` / `#D9E2E8`) to stand out clearly on copper.
- Adjusted deposition sizes and rates in `ToolBelt.cs`:
  - **Solder**: Spreads in a 3x3 grid (any cells hot enough) with speed bumped from `0.4` to `2.5`.
  - **Flux Pen**: Spreads in a 3x3 cross with speed bumped from `1.5` to `6.0`.
  - **Flux Paste**: Keeps its 3x3 grid layout with speed bumped from `4.0` to `12.0`.
- Adjusted solder blob geometry to render a flatter, wider shape (adhering as a flat meniscus puddle):
  - Changed defaults in `PadVisualizer.cs` and programmatic settings in `WorkbenchSceneBuilder.cs` (`blobDiameterFracOfPad` = `0.85`, `blobHeightFracOfDiameter` = `0.2`).
- Verified C# compilation and formatting.

## 2026-06-04 — Physical Component Bonding

- Created `SolderablePin.cs` to raycast down, detect `Pad` overlap, map coordinates to pad cells, and track bonding state based on solid vs molten solder.
- Created `SolderableComponent.cs` to manage the root object's bonding state while keeping the Rigidbody kinematic to prevent PhysX sub-centimeter launches.
- Updated `ToolBelt.cs` with `Tweezers` tool mode (hotkey `5`) supporting drag-and-drop:
  - Temporarily lifts Y by 2mm during dragging to hover above the board.
  - Snaps to the exact surface height of the board/pad on release using a downward raycast.
  - Maintains `isKinematic = true` at all times to ensure 100% stability.
- Updated `StatusHUD.cs` to display tweezers active tool and key hints.
- Updated `WorkbenchSceneBuilder.cs` with a programmatic `CreateResistor` generator that sets `isKinematic = true` on the Rigidbody and spawns them resting flat on the board at Y = 4mm (0.004) at scene start.
- Verified C# compilation and formatting.

## 2026-06-04 — HUD & Tweezers Kinematic Physics Fixes

- Fixed a `NullReferenceException` in `StatusHUD.Update()` and `LatencyHUD.Update()` that occurred when modifying scripts in Play Mode (domain reload / hot-reload).
  - **Cause**: Unity's hot-reload serializer kept serializable private fields like `_uiInitialized` as `true` but set non-serializable UI Toolkit elements (`Label`, `VisualElement`) back to `null`.
  - **Solution**: Rewrote UI initialization in `StatusHUD.cs` and `LatencyHUD.cs` to check for `null` element references inside `Update()`, and added `root.Clear()` to ensure rebuilding does not leak duplicate elements.
- Fixed Unity 6 console errors stating "Setting linear velocity of a kinematic body is not supported" when grabbing or snapping components with tweezers in `ToolBelt.cs`.
  - **Cause**: `ToolBelt.UpdateTweezers` attempted to set `rb.linearVelocity = Vector3.zero` and `rb.angularVelocity = Vector3.zero` on rigidbodies that are kinematic.
  - **Solution**: Removed the lines clearing the velocities, as kinematic rigidbodies do not need manual velocity resetting and Unity 6 forbids setting velocities directly on them.
- Fixed a bug where repeatedly clicking on a resistor with the tweezers caused it to climb upward off the screen.
  - **Cause**: The snap-down raycast was hitting the resistor's own colliders, snapping its position to its own top and compounding the height.
  - **Solution**: Temporarily disabled all child colliders of the component during the snap-down raycast so it ignores itself and correctly hits the board or pad. Also locked the dragging Y offset to `0f` to prevent height accumulation.
- Implemented solder metallicity and physical fillet adhesion:
  - **Metallic Shaders**: Configured copper pads, silver resistor pins, and solder blob materials with metallic and smoothness properties to render with a shiny, metallic finish in URP.
  - **Dynamic Fillet Adhesion**: Programmed the solder blob in `PadVisualizer.cs` to morph, shift, and stretch upwards towards component pins to simulate solder wetting/fillet capillary action.
  - **Self-Collision Pin Raycast, Neighborhood & Wetting Fix**: Resolved self-collision inside `SolderablePin.cs` using `Physics.RaycastAll` and filtering out its own component's colliders. Updated the solid solder detection to inspect a 5x5 cell neighborhood, and implemented a **wetting reflow state machine** (`_isWetted` flag) ensuring that pins only bond to solid solder if they were in contact during a liquid/molten transition first (preventing instant bonding to cold solder bumps).
  - **Single-Joint Anchoring (Any-Pin-Bonded)**: Updated `SolderableComponent.cs` to lock the component as soon as **any** single pin is bonded. This anchors the component immediately upon first joint solidification, matching real-world behavior and allowing the user to solder remaining pins without the component sliding away.
  - **Macro Camera View**: Moved the camera closer to the board in `WorkbenchSceneBuilder.cs` (to `(0, 0.12, -0.12)`) to provide a better close-up of fine SMD pads.
- Implemented real-time soldering process guidance HUD:
  - **Dynamic Guidance UI**: Redesigned `EvalHUD.cs` to render a dynamic, real-time guidance panel.
  - **Active Pad Tracking**: Configured `WorkbenchSimulator.cs` to search for and identify the active pad under the iron tip or the tweezers cursor.
  - **Step-by-Step Checklist (Collapsing)**: Displays progress dynamically for component placement, flux application, joint heating, solder feeding, and cooling. Hides completed tasks to automatically scroll remaining tasks to the top. Shows a green joint success banner on completion.
  - **Component Security Status**: Tracks and displays the overall board security (`SECURED` / `UNSECURED`) for each resistor.
  - Verified compilation and updated artifacts.

