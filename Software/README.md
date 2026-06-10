# Virtual Flux — Unity Project

Open this folder in Unity Hub with **Unity 6 LTS (6000.0.x)**. URP is the active render pipeline.

## Scene

`Assets/Scenes/Workbench.unity` is the only V1 scene. It contains the virtual workbench, the `SamplePCB` prefab, the `VirtualIron` prefab, and the HUD overlay.

## Controls (V1 — keyboard/mouse)

| Action          | Binding             |
|-----------------|---------------------|
| Translate XY    | WASD                |
| Translate Z     | Q / E               |
| Pitch           | I / K               |
| Yaw             | J / L               |
| Roll            | U / O               |
| Energize        | hold Space          |
| Temp preset     | digits 1–5          |
| Reset pose      | R                   |
| Mouse-look      | hold RMB + mouse XY |
| Select tool     | 1 iron / 2 wire / 3 flux pen / 4 flux paste / 5 tweezers |
| Feed / apply    | hold LMB            |
| Inspect cell    | RMB click (when not held for mouse-look)    |

The 6DOF arm is not yet wired up. When firmware ships, `SerialIronInput` replaces `KeyboardIronInput` without any other changes — both implement `IIronInputSource`.

## Code layout

```
Assets/Scripts/
├── Input/    # IIronInputSource, KeyboardIronInput, SerialIronInput
├── Hardware/ # IronController
├── Sim/      # ThermalModel, Pad, SolderState, SolderFlow, FluxModel, ToolBelt
├── Gerber/   # GerberParser, GerberMeshBuilder, GerberLoadUI
├── Eval/     # TestCase, Evaluator, EvalHUD
└── App/      # BootstrapScene, LatencyHUD
```

## Tests

EditMode tests live in `Assets/Tests/EditMode/`. Run from `Window → General → Test Runner → EditMode → Run All`, or via CLI:

```
Unity -batchmode -runTests -testPlatform editmode -projectPath Software
```

## Determinism

Sim is integrated at a fixed 5 ms (`Time.fixedDeltaTime = 0.005`). Never sample wall-clock or `UnityEngine.Random` inside `Sim/` — pass a seed or step count.
