# 6dofPCB — Virtual Flux (Product Requirements & Overview)

## 1. Product Vision
**Virtual Flux** is a high-fidelity, hardware-in-the-loop soldering simulator. It bridges physical motion and virtual physics to create a realistic training and evaluation environment for hand soldering. By using a physical 6DOF (Six Degrees of Freedom) robotic arm that acts as the soldering iron (currently in development, with full keyboard/mouse fallbacks available now), users can practice their technique on a virtual PCB rendered in a Unity desktop application. The simulation provides deterministic, physics-based reactions—including thermodynamics, flux activation, and solder wetting—coupled with real-time evaluation.

## 2. Target Audience & Use Cases
- **Soldering Trainees & Students**: Learn proper technique (angle, dwell time, temperature control, flux application) without consuming physical consumables or risking damage to expensive boards.
- **Instructors & Evaluators**: Provide standardized, objective, and deterministic grading of a student's soldering joints.
- **Hardware Enthusiasts**: Experiment with a novel hardware-software interface.

## 3. Core Features & Requirements (V1)

### 3.1 Physics & Simulation Engine
The core of Virtual Flux is its deterministic, grid-based simulation running at a fixed 5 ms timestep in Unity.
- **Thermal Model**: Heuristic exponential heat transfer from the iron tip to the pad, propagating across a grid.
- **Flux Dynamics**: Tracks states of flux (Cold -> Active -> Burnt) based on temperature thresholds. Flux cleans oxidation and enables solder flow.
- **Solder Flow & Wetting**: Grid-based solder deposition that flows to heated cells. Features visual metallic rendering and dynamic fillet capillary action morphing towards component pins.
- **Component Bonding**: Solderable pins detect solid vs. molten solder. A wetting reflow state machine ensures pins only bond if they contact molten solder that subsequently cools and solidifies.

### 3.2 User Interaction & Tooling
While the physical arm is the primary input, V1 supports full keyboard/mouse fallbacks.
- **6DOF Iron**: Translates and rotates in 3D space. Reports tip temperature and spatial pose.
- **Toolbelt**:
  - **Solder Wire**: Click-to-feed solder deposition.
  - **Flux Pen / Paste**: Apply flux to pads prior to heating.
  - **Tweezers**: Drag-and-drop SMD component placement (e.g., resistors) with snap-to-surface raycasting and kinematic stability.

### 3.3 Evaluation & HUD
- **Real-Time Guidance**: Dynamic UI panel tracking the active pad. Displays a collapsing step-by-step checklist (placement, flux, heat, solder, cool).
- **Deterministic Scoring**: Evaluates the joint based on strict tolerances:
  - Soldering angle (e.g., 40–55°)
  - Dwell time (e.g., 1.5–3 s)
  - Peak temperature (e.g., 250–360 °C)
  - Flux sequence (must flux before heating)
  - Solder volume and tip-feeding requirements.
- **Status & Latency**: Heads-up displays for iron temperature, active tool, board security status, and simulation frame timing.

## 4. System Architecture

The project is split into three tightly coupled domains:

### 4.1 Hardware (KiCad)
The physical 6DOF arm is driven by custom PCBs.
- **MCU Board**: The brains of the arm.
- **Controller Board**: Interfacing and power.
- *Located in `Hardware/` as KiCad 7+ projects and RS-274X Gerbers.*

### 4.2 Firmware (C/C++)
- **PlatformIO Project**: Runs on the MCU board. Solves arm kinematics and streams the tool tip's pose and temperature over USB-serial to the PC.
- *Located in `Firmware/6DoF Firmware/` (currently in progress).*
- *Protocol locked and documented in `docs/serial-protocol.md`.*

### 4.3 Software (Unity)
- **Unity 6 LTS (6000.0.x)** using the Universal Render Pipeline (URP) and the New Input System.
- Receives serial pose data (`P,x,y,z,pitch,yaw,roll,temp,seq`) and applies it to the virtual iron.
- Parses real Gerber files to generate the virtual copper PCB meshes.
- *Located in `Software/`.*

## 5. Repository Layout

| Path                | Contents                                                          |
|---------------------|-------------------------------------------------------------------|
| `Hardware/MCU/`     | MCU board — KiCad project + Gerbers. Lives on the arm itself.     |
| `Hardware/Controller/` | Controller board — KiCad project + Gerbers.                    |
| `Hardware/Gerbers/` | Zipped Gerber bundles for fabrication.                            |
| `Hardware/assets.pretty/` | Shared KiCad footprint library.                             |
| `Firmware/`         | PlatformIO firmware project for the 6DOF arm.                     |
| `Software/`         | Unity 6 LTS project. Open this folder in Unity Hub.               |
| `docs/`             | Cross-cutting docs — start with `serial-protocol.md`.             |
| `CLAUDE.md`         | Working agreement for AI coding assistants.                       |
| `NOTES.md`          | Session handoff log & historical milestones.                      |

## 6. Quick Start

1. **Hardware**: Open `Hardware/MCU/MCU.kicad_pro` or `Hardware/Controller/Controller.kicad_pro` in KiCad 7+.
2. **Software**: Open `Software/` in Unity 6 LTS. Load `Assets/Scenes/Workbench.unity` and press Play. See `Software/README.md` for detailed keyboard/mouse controls.

## 7. Future Roadmap (Post-V1)

- **Arm Firmware Completion**: Finish the C/C++ kinematics solver and hardware integration.
- **AI Joint-Quality Evaluation**: Machine learning models to assess the visual quality of the final solder fillet.
- **Advanced Hardware**: Haptic feedback, VR integration, and BLE wireless connectivity.

## License
TBD.
