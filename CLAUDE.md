# CLAUDE.md

This file tells Claude (and any other AI coding assistant) how to work in this repository. Read it at the start of every session. If anything here conflicts with a user instruction, ask before proceeding.

## Prime Directives

1. **Plan before coding.** For any non-trivial change, state the plan in plain English first and wait for confirmation.
2. **Small, reviewable diffs.** One concern per change. Never bundle refactors with features.
3. **Never silently rewrite code you weren't asked to touch.** If you think something nearby should change, mention it — don't just do it.
4. **No fake implementations.** No stub returns, no hardcoded "TODO" values pretending to work, no swallowed errors. If something can't be implemented, say so.
5. **Read before you write.** Inspect the relevant files and existing patterns before generating new code. Match the conventions already in the repo.

## Project Conventions

This repo is a hardware-in-the-loop soldering simulator. A physical 6DOF arm (KiCad-designed PCB + future firmware) streams pose to a Unity desktop app that simulates a soldering iron acting on a virtual PCB.

- **Hardware language/tools:** KiCad 7+ (`.kicad_pcb`, `.kicad_sch`, `.kicad_pro`). Gerber output is RS-274X.
- **Firmware (future):** C/C++ targeting the MCU on `Hardware/MCU/`. Not implemented in V1.
- **Software language/runtime:** C# 9+, Unity 6 LTS (6000.0.x) with Universal Render Pipeline. Falls back to 2022.3 LTS only if explicitly requested.
- **Software framework:** Unity Input System (new) for input, UI Toolkit (UXML/USS) for HUDs, Unity Test Framework for tests, Shader Graph for material effects.
- **Package manager:** Unity Package Manager (UPM) via `Software/Packages/manifest.json`. No NuGet, no manual DLL drops. Don't add packages without flagging it.
- **Testing:** Unity Test Framework — EditMode tests for pure logic (thermal model, evaluator, Gerber parser, flux model), PlayMode tests only when behavior depends on the Unity runtime loop.
- **Linting / formatting:** `dotnet format` against the generated `.sln` for C#. Repo uses an `.editorconfig` at `Software/.editorconfig` (LF, 4-space indent for C#, 2-space for JSON/YAML).
- **Naming (C#):** PascalCase for types, methods, properties, public fields, and constants; camelCase for locals and parameters; `_camelCase` for private fields; one public type per file matching the filename. Filenames use PascalCase (`ThermalModel.cs`).
- **Naming (KiCad/Gerbers):** keep KiCad's auto-generated filenames as-is — `<Board>-<Layer>.gbr`. Don't rename or hand-edit Gerbers.
- **Units:** millimeters for board geometry, °C for temperatures, seconds for time, degrees for angles. Pose CSV from the arm: `P,x_mm,y_mm,z_mm,pitch_deg,yaw_deg,roll_deg,tip_temp_c,seq\n`.

## Repository Layout

Top-level folders are fixed. Don't create new top-level folders without asking.

```
/Hardware       # all PCB design, exported Gerbers, KiCad footprint library
  /MCU          # MCU board (KiCad project + Gerbers)
  /Controller   # Controller board (KiCad project + Gerbers)
  /Gerbers      # zipped Gerber bundles for fabrication
  /assets.pretty# shared KiCad footprint library
  /Firmware     # placeholder for future MCU firmware
  fp-lib-table  # KiCad footprint library table
/Software       # Unity project — open this folder in Unity
  /Assets
    /Scripts    # C# source, grouped by subsystem (Input/Hardware/Sim/Gerber/Eval/App)
    /Scenes     # Workbench.unity is the primary play scene
    /Prefabs    # VirtualIron, SamplePCB, Pad, SolderBlob, etc.
    /Art        # meshes, materials, Shader Graph assets
    /Settings   # InputActions asset, URP assets
    /Tests
      /EditMode # pure-logic tests
  /Packages     # UPM manifest + lock
  /ProjectSettings
/docs           # cross-cutting docs (serial protocol, design notes)
CLAUDE.md       # this file
README.md       # top-level orientation
NOTES.md        # session handoff notes
```

## How to Work on a Feature

Follow this sequence. Do not skip steps.

1. **Confirm the goal.** Restate what you're about to build in one or two sentences.
2. **Identify the slice.** Build vertically: input → sim update → visual/HUD → tests for one user-facing capability at a time. Don't build horizontal layers in isolation. For sim features, the slice is usually: data on `Pad`/grid cell → update in `FixedUpdate` → visual reaction → test.
3. **Surface the plan.** List the files you'll create or edit and why. Wait for approval on anything touching more than ~3 files.
4. **Implement.** Make the change. Keep edits scoped to what you described.
5. **Test.** Run EditMode tests (`Window → General → Test Runner → EditMode → Run All`, or CLI: `Unity -batchmode -runTests -testPlatform editmode -projectPath Software`). For UI/sim behavior, play the Workbench scene and exercise the change manually; describe what you observed.
6. **Summarize.** End with a short summary of what changed, what's verified, and what's still open.

## What Not to Do

- Don't add new UPM packages without asking. Prefer what's already in `Software/Packages/manifest.json`.
- Don't change `Software/ProjectSettings/` outside the file you intentionally edit — Unity rewrites these aggressively and unrelated diffs are easy to introduce.
- Don't reformat or "clean up" files you're editing for unrelated reasons.
- Don't commit `Software/Library/`, `Software/Temp/`, `Software/Logs/`, `Software/UserSettings/`, or generated `.csproj`/`.sln` files. The `.gitignore` covers these.
- Don't hand-edit `.kicad_pcb` or `.kicad_sch` text — round-trip through KiCad. Don't hand-edit Gerbers at all.
- Don't generate large blocks of speculative code ("you might also want…"). Ask first.
- Don't delete tests to make them pass.
- Don't commit secrets, API keys, or `.env` contents. If you encounter one, stop and warn.

## Protected Areas

These require extra care. For any change in these paths, propose the diff and wait for explicit approval before applying:

- `Hardware/MCU/**`, `Hardware/Controller/**`, `Hardware/assets.pretty/**` — KiCad sources. Layout decisions belong to the hardware owner.
- `Hardware/Gerbers/**` — fabrication outputs. Only regenerate from KiCad; never edit by hand.
- `Software/ProjectSettings/**` — Unity project settings. Changing these affects every scene and the build.
- `Software/Packages/manifest.json` — package set. Adds/removes need approval.
- `docs/serial-protocol.md` — the host/firmware contract. Once firmware exists, breaking changes need both sides updated together.
- `Software/Assets/Scripts/Sim/ThermalModel.cs`, `FluxModel.cs`, `SolderFlow.cs` — the physics heuristics. Tuning constants are fine; structural changes need approval because tests and test cases depend on the model shape.

## Context Hygiene

- Prefer reading specific files over searching the whole repo.
- If the session has grown long or drifted, suggest starting a fresh one and write a brief handoff note in `NOTES.md`.
- When resuming work, check `NOTES.md` and recent commits before assuming state.
- Unity projects regenerate `Library/`, `Temp/`, and `*.csproj` constantly — ignore those when reading diffs.

## Testing Standards

- Every new feature ships with at least one EditMode test for the happy path, plus a test for any failure mode the evaluator should catch.
- Bug fixes ship with a regression test that fails before the fix and passes after.
- Don't write tests that just assert the implementation back to itself. Test behavior, not internals — for the thermal model, that means comparing against the analytic exponential solution, not re-deriving it inside the test.
- Sim code must be deterministic: no `Random` without an injected seed, no per-frame wall-clock reads — use `Time.fixedDeltaTime` (5 ms) so tests can step the simulation by hand.
- Run the full EditMode suite before declaring work complete.

## Code Review Self-Check

Before saying "done," verify each item:

- [ ] The diff only contains changes related to the stated goal.
- [ ] No `Debug.Log` spam, commented-out blocks, or scratch code left behind.
- [ ] No new UPM packages added without approval.
- [ ] No protected areas modified without approval.
- [ ] Tests written and passing.
- [ ] `dotnet format` clean on touched files.
- [ ] Errors are handled, not swallowed; user-visible failures surface in the HUD or as an evaluator rule.
- [ ] Inputs from the serial port, file dialog, or user input are validated before reaching the sim.
- [ ] No secrets in the diff.
- [ ] Sim changes are deterministic and covered by an EditMode test.

## Communication Style

- Be concise. Skip preamble like "Great question!" and get to the work.
- When uncertain, say so. Don't guess at Unity APIs, package versions, or KiCad behavior — read or ask.
- Surface tradeoffs explicitly when you make a non-obvious choice (e.g., grid resolution vs. perf, lumped vs. distributed thermal model).
- If a request is ambiguous, ask one clarifying question before coding.

## Maintenance Tasks

When asked to do maintenance work, treat each as its own focused session:

- **Refactors:** identify duplication and unclear naming. Propose changes; don't bundle with features.
- **Package updates:** one UPM package at a time. Read the package changelog. Run tests after each.
- **Documentation:** keep `README.md`, `docs/serial-protocol.md`, and `NOTES.md` in sync with code changes.
- **Hardware/software sync:** when pad geometry or board layout changes in KiCad, re-export the Gerber bundle and verify the `GerberParser` still loads it.

## Handoff Notes

At the end of a substantive session, append a short entry to `NOTES.md`:

```
## YYYY-MM-DD — <topic>
- What was built / changed
- What was verified
- Open questions or follow-ups
```

This is how future sessions (and future humans) catch up quickly.
