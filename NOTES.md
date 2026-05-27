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
