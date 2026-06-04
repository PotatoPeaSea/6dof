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
            var padMaterial = CreateUrpMaterial("PadMaterial", new Color(0.72f, 0.45f, 0.20f));
            var boardMaterial = CreateUrpMaterial("BoardMaterial", new Color(0.10f, 0.30f, 0.16f));
            var ironMaterial = CreateUrpMaterial("IronMaterial", new Color(0.55f, 0.57f, 0.60f));
            var panel = GetOrCreatePanelSettings();
            var testCase = CreateTestCase();
            AssetDatabase.SaveAssets();

            // --- fresh scene with the default camera + light ---
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 0.18f, -0.18f);
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

            // --- Pad (unit-square local XZ mesh + collider + Pad sim) ---
            var pad = new GameObject("Pad");
            pad.transform.position = new Vector3(0f, 0.0005f, 0f); // just above the board
            pad.transform.localScale = new Vector3(0.02f, 1f, 0.02f); // 2 cm pad
            pad.AddComponent<MeshFilter>().sharedMesh = padMesh;
            pad.AddComponent<MeshRenderer>().sharedMaterial = padMaterial;
            pad.AddComponent<MeshCollider>().sharedMesh = padMesh;
            pad.AddComponent<Pad>();
            pad.AddComponent<PadVisualizer>();

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

            // --- HUDs (each needs a UIDocument + PanelSettings) ---
            var statusHud = AddHud<StatusHUD>("StatusHUD", panel);
            var evalHud = AddHud<EvalHUD>("EvalHUD", panel);
            AddHud<LatencyHUD>("LatencyHUD", panel);

            // --- Sim driver ---
            var sim = new GameObject("WorkbenchSimulator").AddComponent<WorkbenchSimulator>();

            // --- wire every serialized reference ---
            SetRef(ironCtl, "inputSourceBehaviour", keyboard);
            SetRef(ironCtl, "tip", tip.transform);
            SetRef(toolBelt, "iron", ironCtl);
            SetRef(toolBelt, "worldCamera", cam);
            SetRef(statusHud, "toolBelt", toolBelt);
            SetRef(statusHud, "iron", ironCtl);
            SetRef(sim, "iron", ironCtl);
            SetRef(sim, "toolBelt", toolBelt);
            SetRef(sim, "evalHUD", evalHud);
            SetRef(sim, "testCase", testCase);

            // --- save ---
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

        private static Material CreateUrpMaterial(string assetName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = assetName };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, GenFolder + "/" + assetName + ".mat");
            return mat;
        }

        private static PanelSettings GetOrCreatePanelSettings()
        {
            var theme = GetOrCreateRuntimeTheme();

            // Prefer an existing, already-themed PanelSettings: it carries Unity's correct default
            // initialization (scale, DPI, scale mode) that a raw CreateInstance can miss, which
            // otherwise leaves the HUDs not rendering.
            foreach (var guid in AssetDatabase.FindAssets("t:PanelSettings"))
            {
                var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing != null && existing.themeStyleSheet != null) return existing;
            }

            const string path = GenFolder + "/WorkbenchPanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.name = "WorkbenchPanelSettings";
                AssetDatabase.CreateAsset(ps, path);
            }
            ps.themeStyleSheet = theme;
            EditorUtility.SetDirty(ps);
            AssetDatabase.SaveAssetIfDirty(ps);
            return ps;
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

        // ----- helpers -----

        private static T AddHud<T>(string name, PanelSettings panel) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            var doc = go.AddComponent<UIDocument>();
            // Assign via SerializedObject (not the property) so it persists to the saved scene.
            SetRef(doc, "m_PanelSettings", panel);
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

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
