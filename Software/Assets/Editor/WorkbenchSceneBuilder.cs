using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VirtualFlux.App;
using VirtualFlux.Eval;
using VirtualFlux.Hardware;
using VirtualFlux.Input;
using VirtualFlux.Sim;

namespace VirtualFlux.Editor
{
    /// <summary>
    /// One-click scaffold for the Workbench scene. Creates the GameObject hierarchy described
    /// in handoff.md, wires every serialized reference, and generates the supporting assets
    /// (unit pad mesh, pad material, UI Toolkit PanelSettings, a TestCase for the scoring
    /// windows). Pads are auto-discovered at runtime by <see cref="WorkbenchSimulator"/>, so
    /// the TestCase asset only carries the tuning windows.
    ///
    /// Re-runnable: it overwrites Assets/Scenes/Workbench.unity and the assets under
    /// Assets/Workbench/. Art is placeholder primitives — swap in real meshes later.
    /// </summary>
    internal static class WorkbenchSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Workbench.unity";
        private const string GenFolder = "Assets/Workbench";

        [MenuItem("VirtualFlux/Build Workbench Scene")]
        private static void BuildWorkbenchScene()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Build Workbench Scene",
                "This creates/overwrites:\n\n" +
                "  • " + ScenePath + "\n" +
                "  • generated assets under " + GenFolder + "/\n\n" +
                "Continue?",
                "Build", "Cancel");
            if (!ok) return;

            EnsureFolder("Assets", "Workbench");
            EnsureFolder("Assets", "Scenes");

            // --- supporting assets (created first so the scene can reference them) ---
            var padMesh = CreateUnitPadMesh();
            var padMaterial = CreateUrpMaterial("PadMaterial", new Color(0.72f, 0.45f, 0.20f), 0.8f, 0.5f);
            var boardMaterial = CreateUrpMaterial("BoardMaterial", new Color(0.10f, 0.30f, 0.16f));
            var ironMaterial = CreateUrpMaterial("IronMaterial", new Color(0.55f, 0.57f, 0.60f));
            EnsureResourcesPanel();
            var testCase = CreateTestCase();
            AssetDatabase.SaveAssets();

            // --- fresh scene with the default camera + light ---
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 0.12f, -0.12f);
                cam.transform.rotation = Quaternion.LookRotation((Vector3.zero - cam.transform.position).normalized, Vector3.up);
                cam.nearClipPlane = 0.01f;
                cam.fieldOfView = 55f;
            }

            // --- Bootstrap (fixed 5 ms timestep) ---
            var bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<BootstrapScene>();

            // --- Board (placeholder surface) ---
            var board = GameObject.CreatePrimitive(PrimitiveType.Plane);
            board.name = "Board";
            board.transform.position = Vector3.zero;
            board.transform.localScale = new Vector3(0.02f, 1f, 0.02f); // Plane is 10 u → 0.2 m
            board.GetComponent<MeshRenderer>().sharedMaterial = boardMaterial;

            // --- Pads: a short SMD-style row so solder can bridge the gaps ---
            const int padCount = 3;
            const float padSize = 0.008f;    // 8 mm pad
            const float padSpacing = 0.011f; // 3 mm gap between pads
            for (int i = 0; i < padCount; i++)
            {
                float x = (i - (padCount - 1) * 0.5f) * padSpacing;
                var pad = new GameObject($"Pad_{i}");
                pad.transform.position = new Vector3(x, 0.0005f, 0f); // just above the board
                pad.transform.localScale = new Vector3(padSize, 1f, padSize);
                pad.AddComponent<MeshFilter>().sharedMesh = padMesh;
                pad.AddComponent<MeshRenderer>().sharedMaterial = padMaterial;
                pad.AddComponent<MeshCollider>().sharedMesh = padMesh;
                pad.AddComponent<Pad>();
                var padVis = pad.AddComponent<PadVisualizer>();
                SetFloat(padVis, "blobDiameterFracOfPad", 0.85f);
                SetFloat(padVis, "blobHeightFracOfDiameter", 0.2f);
            }

            // --- Resistors (Solderable Components) ---
            var resistorBodyMaterial = CreateUrpMaterial("ResistorBodyMaterial", new Color(0.12f, 0.45f, 0.70f)); // nice blue body
            var resistorPinMaterial = CreateUrpMaterial("ResistorPinMaterial", new Color(0.85f, 0.85f, 0.88f), 0.9f, 0.8f);  // shiny silver pins
            CreateResistor("Resistor_0", new Vector3(-0.012f, 0.004f, 0.015f), resistorBodyMaterial, resistorPinMaterial);
            CreateResistor("Resistor_1", new Vector3(0.012f, 0.004f, 0.015f), resistorBodyMaterial, resistorPinMaterial);

            // --- Iron rig: KeyboardIronInput + IronController, a cosmetic body, and a Tip ---
            var iron = new GameObject("Iron");
            var keyboard = iron.AddComponent<KeyboardIronInput>();
            var ironCtl = iron.AddComponent<IronController>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>()); // must not block raycasts/contact
            body.transform.SetParent(iron.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // length along local Z
            body.transform.localScale = new Vector3(0.006f, 0.04f, 0.006f);
            body.GetComponent<MeshRenderer>().sharedMaterial = ironMaterial;

            var tip = new GameObject("Tip");
            tip.transform.SetParent(iron.transform, false);
            // Heating/contact point at the iron's controlled origin, which is the cylinder's
            // front face — so the hot spot is where the iron visibly is, not floating ahead of it.
            tip.transform.localPosition = Vector3.zero;
            tip.transform.localRotation = Quaternion.identity;

            // --- ToolBelt ---
            var toolBelt = new GameObject("ToolBelt").AddComponent<ToolBelt>();

            // --- HUDs (UIDocuments; panel bound at runtime by HudPanelBinder from Resources) ---
            var statusHud = AddHud<StatusHUD>("StatusHUD");
            var evalHud = AddHud<EvalHUD>("EvalHUD");
            AddHud<LatencyHUD>("LatencyHUD");
            new GameObject("HudPanelBinder").AddComponent<HudPanelBinder>();

            // --- Sim driver + bridge monitor ---
            var sim = new GameObject("WorkbenchSimulator").AddComponent<WorkbenchSimulator>();
            var bridge = new GameObject("BridgeMonitor").AddComponent<BridgeMonitor>();

            // --- wire scene-object references ---
            SetRef(ironCtl, "inputSourceBehaviour", keyboard);
            SetRef(ironCtl, "tip", tip.transform);
            SetRef(toolBelt, "iron", ironCtl);
            SetRef(toolBelt, "worldCamera", cam);
            SetRef(statusHud, "toolBelt", toolBelt);
            SetRef(statusHud, "iron", ironCtl);
            SetRef(sim, "iron", ironCtl);
            SetRef(sim, "toolBelt", toolBelt);
            SetRef(sim, "evalHUD", evalHud);
            SetRef(bridge, "simulator", sim);
            // TestCase ref may not persist from a freshly-built scene; WorkbenchSimulator falls
            // back to default windows if so. The HUD panel is bound at runtime by HudPanelBinder.
            SetRef(sim, "testCase", testCase);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Workbench scene built at {ScenePath}. Press Play, then: WASD/QE move, " +
                      "IJKL/UO rotate, scroll to set temp, hold Space to energize, 1–4 tools, " +
                      "Enter to score, T to reset.");
        }

        // ----- asset builders -----

        private static Mesh CreateUnitPadMesh()
        {
            var mesh = new Mesh { name = "PadQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
            };
            var up = Vector3.up;
            mesh.normals = new[] { up, up, up, up };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            // Double-sided so the pad is visible from above regardless of winding convention.
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, GenFolder + "/PadQuad.asset");
            return mesh;
        }

        private static Texture2D GetOrCreateWhiteTexture()
        {
            const string path = GenFolder + "/DefaultWhite.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                tex = new Texture2D(2, 2);
                var colors = new[] { Color.white, Color.white, Color.white, Color.white };
                tex.SetPixels(colors);
                tex.Apply();
                AssetDatabase.CreateAsset(tex, path);
            }
            return tex;
        }

        private static Material CreateUrpMaterial(string assetName, Color color, float metallic = 0f, float smoothness = 0.5f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = assetName };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness); // Standard fallback
            // Ensure texture keywords are enabled by assigning a saved white texture asset
            var whiteTex = GetOrCreateWhiteTexture();
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", whiteTex);
                mat.EnableKeyword("_BASE_MAP");
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", whiteTex);
                mat.EnableKeyword("_MAIN_TEX");
            }
            AssetDatabase.CreateAsset(mat, GenFolder + "/" + assetName + ".mat");
            return mat;
        }

        // Panel lives under Resources/ so HudPanelBinder can load it by name at runtime.
        private static void EnsureResourcesPanel()
        {
            var theme = GetOrCreateRuntimeTheme();
            EnsureFolder("Assets", "Resources");
            const string path = "Assets/Resources/WorkbenchPanelSettings.asset";

            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.name = "WorkbenchPanelSettings";
                AssetDatabase.CreateAsset(ps, path);
            }
            if (ps.themeStyleSheet == null) ps.themeStyleSheet = theme;
            EditorUtility.SetDirty(ps);
            AssetDatabase.SaveAssetIfDirty(ps);
        }

        private static ThemeStyleSheet GetOrCreateRuntimeTheme()
        {
            // Reuse any theme already in the project.
            var found = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            if (found.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(found[0]));
            }

            // Otherwise generate the default runtime theme — the same one-line .tss that
            // Assets ▸ Create ▸ UI Toolkit ▸ Panel Settings produces.
            EnsureFolder("Assets", "UI Toolkit");
            EnsureFolder("Assets/UI Toolkit", "UnityThemes");
            const string themePath = "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";
            if (!File.Exists(themePath))
            {
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
        }

        private static TestCase CreateTestCase()
        {
            var tc = ScriptableObject.CreateInstance<TestCase>();
            tc.DisplayName = "Basic SMD Joint";
            // Scoring windows keep their field defaults. TargetPads stays empty —
            // WorkbenchSimulator discovers the scene's pads at runtime.
            AssetDatabase.CreateAsset(tc, GenFolder + "/BasicJoint.asset");
            return tc;
        }

        private static void CreateResistor(string name, Vector3 startPos, Material bodyMat, Material pinMat)
        {
            var root = new GameObject(name);
            root.transform.position = startPos;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.isKinematic = true; // Kinematic by default to prevent violent sub-centimeter PhysX collision launches
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            root.AddComponent<SolderableComponent>();

            // Body mesh (cylinder horizontal in local X)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // horizontal along X
            body.transform.localScale = new Vector3(0.002f, 0.004f, 0.002f); // 2mm dia, 8mm length
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            // Remove default cylinder collider on visual body
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Object.DestroyImmediate(bodyCol);

            // Left Pin
            var pinL = new GameObject("Pin_Left");
            pinL.transform.SetParent(root.transform, false);
            pinL.transform.localPosition = new Vector3(-0.005f, -0.002f, 0f); // extend down 2mm
            var visualL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualL.name = "Visual";
            visualL.transform.SetParent(pinL.transform, false);
            visualL.transform.localPosition = new Vector3(0f, 0.001f, 0f); // center visual
            visualL.transform.localScale = new Vector3(0.0006f, 0.001f, 0.0006f);
            visualL.GetComponent<Renderer>().sharedMaterial = pinMat;
            Object.DestroyImmediate(visualL.GetComponent<Collider>());
            pinL.AddComponent<SolderablePin>();

            // Right Pin
            var pinR = new GameObject("Pin_Right");
            pinR.transform.SetParent(root.transform, false);
            pinR.transform.localPosition = new Vector3(0.005f, -0.002f, 0f); // extend down 2mm
            var visualR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualR.name = "Visual";
            visualR.transform.SetParent(pinR.transform, false);
            visualR.transform.localPosition = new Vector3(0f, 0.001f, 0f); // center visual
            visualR.transform.localScale = new Vector3(0.0006f, 0.001f, 0.0006f);
            visualR.GetComponent<Renderer>().sharedMaterial = pinMat;
            Object.DestroyImmediate(visualR.GetComponent<Collider>());
            pinR.AddComponent<SolderablePin>();

            // Root box collider for tweezers dragging & collision
            var boxCol = root.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(0.012f, 0.005f, 0.003f);
            boxCol.center = new Vector3(0f, -0.001f, 0f);
        }

        // ----- helpers -----

        private static T AddHud<T>(string name) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            go.AddComponent<UIDocument>(); // panel bound at runtime by HudPanelBinder
            return go.AddComponent<T>();
        }

        private static void SetRef(Component target, string propertyName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"WorkbenchSceneBuilder: '{target.GetType().Name}' has no serialized " +
                               $"field '{propertyName}'. Scene wiring is incomplete.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target); // ensure asset references (panel, testCase) persist on save
        }

        private static void SetFloat(Component target, string propertyName, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"WorkbenchSceneBuilder: '{target.GetType().Name}' has no serialized " +
                               $"field '{propertyName}'.");
                return;
            }
            prop.floatValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
