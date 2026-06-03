# Task: Build the Workbench Scene in Unity

You are setting up a Unity scene for a hardware-in-the-loop soldering simulator. All the C#
scripts described below **already exist in the project** as `MonoBehaviour` / `ScriptableObject`
components. **Do not write or modify any scripts** — your job is to create the scene, add these
existing components to GameObjects, and wire their serialized fields in the Inspector. This
document is self-contained; everything you need (component names, field names, defaults,
controls, constraints) is below.

**Environment:** Unity 6 LTS (`6000.0.x`), Universal Render Pipeline, new Input System.

**Approach: ship incremental results.** The milestones below are each independently playable.
Do them in order and verify each one runs before moving on. Early milestones use keyboard
(WASD) control so the sim works with no hardware. A physical 6DOF arm is a drop-in swap at the
very end.

---

## Prerequisites (do once)

- **Active Input Handling:** `Edit → Project Settings → Player → Other Settings → Active Input
  Handling` must be **Input System Package (New)** (or Both). The scripts use the new Input
  System; the old backend will not work.
- **UI Toolkit PanelSettings:** every HUD requires a `UIDocument`, and a `UIDocument` renders
  nothing unless a **PanelSettings** asset is assigned to it. Create one via `Assets → Create →
  UI Toolkit → Panel Settings` and reuse it across all HUDs. The HUDs build their UI from code,
  so no UXML files are needed.

---

## Milestone 1 — WASD iron flies around an empty board

**Goal:** press Play, move a soldering-iron object in 3D with the keyboard, and watch the tip
temperature warm up / cool down on a HUD. No pads, no scoring yet.

1. `File → New Scene`. Save it as `Assets/Scenes/Workbench.unity`.
2. Empty GameObject **Bootstrap** → add component `BootstrapScene`. (It sets the physics step
   to a fixed 5 ms for deterministic simulation. Field: `fixedTimestepSec` = `0.005`.)
3. Build the iron rig:
   - Empty GameObject **Iron**. Add components `KeyboardIronInput` and `IronController`.
   - Add a child empty GameObject **Tip**, positioned where the physical iron tip would be.
     Orient it so its local **+Z (forward)** points *down the iron shaft toward the tip* — a
     later milestone reads `Tip.forward` to measure soldering angle.
   - On `IronController`: set `inputSourceBehaviour` = the **KeyboardIronInput** component on
     this same GameObject; set `tip` = the **Tip** child Transform.
   - (Cosmetic) parent a placeholder mesh (stretched cube or cylinder) under **Iron** so it's
     visible. Real art comes later.
4. **StatusHUD:** empty GameObject → add `StatusHUD` (this auto-adds a `UIDocument`). Assign the
   PanelSettings to the UIDocument. Set the StatusHUD's `iron` field = the IronController. Leave
   its `toolBelt` field empty for now.
5. Add a Camera positioned to see the iron (a default Main Camera is fine).

**Controls (`KeyboardIronInput`):** `W/S` = forward/back, `A/D` = left/right, `Q/E` = down/up,
`I/K` `J/L` `U/O` = rotate, `1`–`5` = temperature presets, **Space** = energize (hold), `R` =
reset pose.

**Verify:** the iron moves with WASD; the HUD shows `tip: …°C` rising toward the selected preset
while Space is held and falling back to ambient (~25 °C) when released.

---

## Milestone 2 — One pad you can heat

**Goal:** hover the energized tip over a pad and have that pad heat up.

1. Create the PCB surface: a Plane or Quad.
2. Create a **Pad** GameObject sitting on the board:
   - **Critical mesh constraint:** the pad's mesh must be a **unit square in its local XZ
     plane, spanning −0.5 to +0.5 in both X and Z, centered at the origin** (Y is the surface
     normal). Contact detection and the tool grid both assume this exact layout. A default Unity
     Quad laid flat is 1×1 in its local plane — verify the axes line up, or author a matching
     quad. Scale the GameObject to set the pad's world size.
   - Add the `Pad` component. Defaults: `gridWidth`/`gridHeight` = 16, `meltingPointC` ≈ 183,
     `maxToleranceC` = 380.
   - Add a **Collider** (Mesh or Box). Required for tool raycasts in Milestone 3.
3. Create the sim driver: empty GameObject **WorkbenchSimulator** → add `WorkbenchSimulator`.
   - Set `iron` = the IronController.
   - **Important behavior:** this driver does nothing until it has a `testCase` whose
     `TargetPads` list contains your pad — it only ticks pads that are listed as targets. So
     create the TestCase asset now (Milestone 4, steps 1–2) and assign it here, even though you
     won't read the scorecard until later. Defaults you can leave: `contactDistanceMeters` =
     0.005, `padHotThresholdC` = 60.

**Contact rule (reference):** a pad counts as "in contact" when the iron is **energized**, the
tip is inside the pad's local XZ box, **and** the tip's local-Y distance to the pad surface is
≤ `contactDistanceMeters` (5 mm default).

**Verify:** moving the energized tip onto the pad heats it; moving away lets it cool. (It's
easier to see once you add a temperature-driven color in Milestone 5.)

---

## Milestone 3 — Tools: feed solder and flux

**Goal:** select tools and deposit flux / solder onto the pad with the mouse.

1. **ToolBelt** GameObject → add `ToolBelt`. Set `iron` = IronController, `worldCamera` = your
   Camera. Defaults: `solderFeedRatePerSec` = 0.4, `fluxPenAmountPerSec` = 1.5,
   `fluxPasteAmountPerSec` = 4, `ironSnapRadiusMeters` = 0.003.
2. Set the StatusHUD's `toolBelt` field = this ToolBelt so the active tool displays.

**Controls (`ToolBelt`):** `1` = iron, `2` = solder wire, `3` = flux pen, `4` = flux paste. Hold
**left mouse** over the pad to deposit. Solder only takes on cells already at/above melting
point. A "feed at iron tip" snap applies when the cursor is within `ironSnapRadiusMeters` of the
tip.

**Verify:** flux and solder land on the cell under the cursor. (Pads must have colliders or the
raycast misses.)

---

## Milestone 4 — Scoring: evaluator + EvalHUD

**Goal:** perform a full flux → heat → feed-solder sequence and get a PASS/FAIL scorecard.

1. Create a **TestCase** asset: `Assets → Create → VirtualFlux/Test Case`. Save it anywhere
   under `Assets/` (e.g. `Assets/Settings/`).
2. Add your Pad to the asset's **TargetPads** list. Tunable windows (defaults): angle 40–55°,
   dwell 1.5–3 s, peak temp 250–360 °C, solder volume 0.2–1.5. Process toggles default on:
   `RequireFluxBeforeHeat`, `RequireSolderFedAtIronTip`, `ForbidBurnt`, `ForbidLifted`.
3. **EvalHUD** GameObject → add `EvalHUD` (auto-adds `UIDocument`). Assign PanelSettings.
4. On **WorkbenchSimulator**, set `toolBelt`, `evalHUD`, and `testCase`. (If you already
   assigned `testCase` in Milestone 2, just fill in the other two.)

**Controls (`EvalHUD`):** `R` = score (render the scorecard), `T` = reset observations.

**Verify:** a clean run — apply flux while the pad is cold, then hold the energized tip at the
target angle/temperature for the dwell window, then feed solder at the tip — renders **PASS**.
Breaking any rule (flux after heating, wrong angle, no solder, wrong volume) renders **FAIL**
with the failing rule highlighted.

---

## Milestone 5 — Visual polish + Gerber import (optional)

- **LatencyHUD:** add `LatencyHUD` to a GameObject (auto-adds `UIDocument`; assign
  PanelSettings) for a frame-time / fixed-step readout. Field: `updateIntervalSec` = 0.25.
- **Temperature / solder visuals:** drive the pad's material color from its temperature and
  solder amount so heating and wetting are visible. This is what makes Milestones 2–4 readable.
- **Real PCB copper:** add `GerberLoadUI` to a GameObject. Set `defaultPath` to a `.gbr` file or
  a zipped Gerber bundle (or set the `VIRTUAL_FLUX_GERBER` environment variable to that path),
  and assign a `copperMaterial`. On play it parses the copper layer and builds a board mesh.
- **Art pass:** real iron, board, and workbench meshes. (None exist in the project yet.)

---

## Milestone 6 — Swap WASD for the physical 6DOF arm (future)

The input is abstracted behind an interface (`IIronInputSource`), so swapping to hardware
touches nothing in the sim:

- A `SerialIronInput` component already exists and implements the same interface. It reads the
  arm's pose over USB-serial in this line format:
  `P,x_mm,y_mm,z_mm,pitch_deg,yaw_deg,roll_deg,tip_temp_c,seq`. (Its serial read loop is a stub
  pending firmware; the line parser is done.)
- When firmware is ready: on **IronController**, change `inputSourceBehaviour` from
  `KeyboardIronInput` to `SerialIronInput`. Nothing else in the scene changes — the arm's
  firmware solves its own kinematics and streams the solved tip pose; Unity just applies it.
  (The arm's link lengths and joint angles live in firmware, not Unity. No arm linkage is
  modeled or rendered on the Unity side.)

---

## Component wiring reference

| Component (already in project) | Fields to assign | Notes |
|---|---|---|
| `BootstrapScene` | `fixedTimestepSec` = 0.005 | Deterministic 5 ms physics step. |
| `KeyboardIronInput` | speeds, initial pose, `tempPresetsC[]` (optional) | Implements `IIronInputSource`. WASD/QE move, IJKL/UO rotate, 1–5 temp, Space energize, R reset. |
| `IronController` | `inputSourceBehaviour`, `tip` | `inputSourceBehaviour` must be a component implementing `IIronInputSource` (the KeyboardIronInput). |
| `Pad` | grid size, temps (optional) | **Mesh must be unit-square in local XZ, −0.5..0.5, centered, Y = normal.** Needs a Collider. |
| `WorkbenchSimulator` | `iron`, `toolBelt`, `evalHUD`, `testCase` | Does nothing until `iron` + a `testCase` listing the pad are set. |
| `ToolBelt` | `iron`, `worldCamera` | Keys 1–4 select tool; left-mouse deposits. Pads need colliders. |
| `StatusHUD` | `toolBelt`, `iron` | Needs UIDocument + PanelSettings. |
| `EvalHUD` | — | Needs UIDocument + PanelSettings. R = score, T = reset. |
| `LatencyHUD` | `updateIntervalSec` (optional) | Needs UIDocument + PanelSettings. |
| `GerberLoadUI` | `defaultPath`, `copperMaterial` | Path also settable via `VIRTUAL_FLUX_GERBER` env var. |
| `TestCase` (ScriptableObject asset) | `TargetPads` + scoring windows | Create via `Assets → Create → VirtualFlux/Test Case`. |

## Known gotchas

- **Pad mesh axes** are the #1 trap. If contact never registers: the mesh isn't a unit square in
  local XZ centered at the origin, or the tip's local-Y distance to the surface exceeds
  `contactDistanceMeters`.
- **Tip orientation:** the Tip child's local +Z (forward) must point down the shaft toward the
  tip, or the soldering-angle rule scores wrong.
- **UIDocument with no PanelSettings renders nothing and reports no error.** Always assign it.
- **WorkbenchSimulator stays idle without a TestCase** whose `TargetPads` includes your pad —
  assign it early.
- **Tools need pad colliders** for the mouse raycast to land.
