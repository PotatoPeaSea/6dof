# Scenes

`Workbench.unity` should be created on first open in Unity:

1. Open `Software/` in Unity 6 LTS.
2. `File → New Scene → Basic (URP)`. Save as `Assets/Scenes/Workbench.unity`.
3. Add an empty GameObject "App" with `BootstrapScene`, `LatencyHUD` (needs a `UIDocument`).
4. Drop the `VirtualIron` and `SamplePCB` prefabs into the scene (build these from `Assets/Prefabs/` on first run).
5. Wire `IronController.inputSourceBehaviour` → the `KeyboardIronInput` component on `VirtualIron`.

The scene file is intentionally not committed yet — Unity's YAML scene format is version-sensitive, and hand-authoring it outside the editor is error-prone. Commit the scene the first time it's saved from Unity.
