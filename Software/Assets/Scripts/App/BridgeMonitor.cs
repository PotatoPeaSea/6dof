using System.Collections.Generic;
using UnityEngine;
using VirtualFlux.Sim;

namespace VirtualFlux.App
{
    /// <summary>
    /// Derives solder-bridge state between adjacent pads — read-only over each Pad's SolderFlow —
    /// shows a bar across bridged gaps, and reports bridges to the evaluator. Touches no physics.
    /// </summary>
    public sealed class BridgeMonitor : MonoBehaviour
    {
        [SerializeField] private WorkbenchSimulator simulator;
        [SerializeField] private float maxGapMeters = 0.004f;
        [SerializeField] private float formVolume = BridgeRules.DefaultFormVolume;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private sealed class Pair
        {
            public Pad A;
            public Pad B;
            public Vector3 DirAToB; // world, from A toward B
            public Transform Bar;
            public Renderer BarRenderer;
        }

        private readonly List<Pair> _pairs = new List<Pair>();
        private Material _barMaterial;

        private void Start()
        {
            var pads = Object.FindObjectsByType<Pad>(FindObjectsSortMode.None);
            BuildBarMaterial(pads);

            for (int i = 0; i < pads.Length; i++)
            {
                for (int j = i + 1; j < pads.Length; j++)
                {
                    var a = pads[i];
                    var b = pads[j];
                    float halfA = 0.5f * a.transform.lossyScale.x;
                    float halfB = 0.5f * b.transform.lossyScale.x;
                    float surfaceGap = Vector3.Distance(a.transform.position, b.transform.position) - halfA - halfB;
                    if (surfaceGap > maxGapMeters || surfaceGap < -Mathf.Min(halfA, halfB)) continue;

                    _pairs.Add(CreatePair(a, b, surfaceGap, Mathf.Min(2f * halfA, 2f * halfB)));
                }
            }
        }

        private void OnDestroy()
        {
            if (_barMaterial != null) Destroy(_barMaterial);
            foreach (var p in _pairs)
            {
                if (p.Bar != null) Destroy(p.Bar.gameObject);
            }
        }

        private void FixedUpdate()
        {
            var evaluator = simulator != null ? simulator.Evaluator : null;
            foreach (var p in _pairs)
            {
                var (solderA, moltenA) = FacingEdge(p.A, p.DirAToB);
                var (solderB, moltenB) = FacingEdge(p.B, -p.DirAToB);
                bool bridged = BridgeRules.Bridged(solderA, solderB, moltenA, moltenB, formVolume);

                if (p.BarRenderer.enabled != bridged) p.BarRenderer.enabled = bridged;
                if (bridged) evaluator?.RecordBridge(p.A, p.B);
            }
        }

        private Pair CreatePair(Pad a, Pad b, float surfaceGap, float padSize)
        {
            var dir = (b.transform.position - a.transform.position).normalized;
            var mid = 0.5f * (a.transform.position + b.transform.position);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Bridge_{a.name}_{b.name}";
            var col = cube.GetComponent<Collider>();
            if (col != null) Destroy(col);

            float length = Mathf.Max(surfaceGap, 0f) + 0.4f * padSize; // spans the gap, slightly onto each pad
            float thickness = 0.3f * padSize;
            cube.transform.position = mid + Vector3.up * (thickness * 0.5f);
            cube.transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
            cube.transform.localScale = new Vector3(length, thickness, 0.4f * padSize);

            var r = cube.GetComponent<Renderer>();
            r.sharedMaterial = _barMaterial;
            r.enabled = false;

            return new Pair { A = a, B = b, DirAToB = dir, Bar = cube.transform, BarRenderer = r };
        }

        private void BuildBarMaterial(Pad[] pads)
        {
            Shader shader = null;
            foreach (var pad in pads)
            {
                var rend = pad.GetComponent<Renderer>();
                if (rend != null && rend.sharedMaterial != null) { shader = rend.sharedMaterial.shader; break; }
            }
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var warn = new Color(0.90f, 0.50f, 0.45f); // solder with a red warning cast
            _barMaterial = new Material(shader) { name = "SolderBridgeMaterial", color = warn };
            if (_barMaterial.HasProperty(BaseColorId)) _barMaterial.SetColor(BaseColorId, warn);
        }

        private static (float solder, bool molten) FacingEdge(Pad pad, Vector3 worldDirToNeighbor)
        {
            var flow = pad.Flow;
            if (flow == null) return (0f, false);

            var local = pad.transform.InverseTransformDirection(worldDirToNeighbor);
            float solder = 0f;
            bool molten = false;
            if (Mathf.Abs(local.x) >= Mathf.Abs(local.z))
            {
                int col = local.x >= 0f ? flow.Width - 1 : 0;
                for (int y = 0; y < flow.Height; y++)
                {
                    solder += flow.GetSolder(col, y);
                    if (flow.GetTemp(col, y) >= flow.MeltingPointC) molten = true;
                }
            }
            else
            {
                int row = local.z >= 0f ? flow.Height - 1 : 0;
                for (int x = 0; x < flow.Width; x++)
                {
                    solder += flow.GetSolder(x, row);
                    if (flow.GetTemp(x, row) >= flow.MeltingPointC) molten = true;
                }
            }
            return (solder, molten);
        }
    }
}
