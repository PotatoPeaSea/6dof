# 6dofPCB — Virtual Flux

A hardware-in-the-loop soldering simulator. A 6DOF arm (custom KiCad PCBs in `Hardware/`) streams pose over USB to a Unity desktop app (`Software/`) that simulates a heated iron acting on a virtual board: thermal model, flux activation, solder flow, deterministic pass/fail scoring.

## Repository layout

| Path                | Contents                                                          |
|---------------------|-------------------------------------------------------------------|
| `Hardware/MCU/`     | MCU board — KiCad project + Gerbers. Lives on the arm itself.     |
| `Hardware/Controller/` | Controller board — KiCad project + Gerbers.                    |
| `Hardware/Gerbers/` | Zipped Gerber bundles for fabrication.                            |
| `Hardware/assets.pretty/` | Shared KiCad footprint library.                             |
| `Hardware/Firmware/`| Placeholder. Firmware not implemented in V1.                      |
| `Software/`         | Unity 6 LTS project. Open this folder in Unity Hub.               |
| `docs/`             | Cross-cutting docs — start with `serial-protocol.md`.             |
| `CLAUDE.md`         | Working agreement for AI coding assistants.                       |
| `NOTES.md`          | Session handoff log.                                              |

## V1 scope

- Keyboard-driven virtual iron (6DOF) — arm firmware deferred.
- Sample PCB prefab + Gerber import for the MCU board.
- Heuristic thermal model, flux model (cold/active/burnt), grid-based solder flow.
- Toolbelt: iron / solder wire / flux pen / flux paste with explicit click-to-feed.
- Deterministic test cases (angle, dwell, peak temp, flux-before-heat, solder-at-tip).

## Out of V1

- Arm firmware (only the serial protocol is locked — see `docs/serial-protocol.md`).
- AI joint-quality evaluation.
- Haptics, VR, BLE.

## Quick start

1. **Hardware:** open `Hardware/MCU/MCU.kicad_pro` or `Hardware/Controller/Controller.kicad_pro` in KiCad 7+.
2. **Software:** open `Software/` in Unity 6 LTS. Load `Assets/Scenes/Workbench.unity` and press Play. See `Software/README.md` for controls.

## License

TBD.
