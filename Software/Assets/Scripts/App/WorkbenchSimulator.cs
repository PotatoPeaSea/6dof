using System.Collections.Generic;
using UnityEngine;
using VirtualFlux.Eval;
using VirtualFlux.Hardware;
using VirtualFlux.Sim;

namespace VirtualFlux.App
{
    /// <summary>
    /// Central sim driver. Each FixedUpdate: identifies which target pads the iron tip
    /// is in contact with, ticks each pad's thermal/flow model, and feeds the Evaluator.
    /// Subscribes to ToolBelt deposition events and the EvalHUD's score/reset keys.
    ///
    /// Pad meshes are authored at unit-square local extents (-0.5..0.5 in X and Z), so
    /// "iron tip is over this pad" reduces to a local-space box test after
    /// <see cref="Transform.InverseTransformPoint"/>.
    /// </summary>
    public sealed class WorkbenchSimulator : MonoBehaviour
    {
        [SerializeField] private IronController iron;
        [SerializeField] private ToolBelt toolBelt;
        [SerializeField] private EvalHUD evalHUD;
        [SerializeField] private TestCase testCase;
        [SerializeField] private float contactDistanceMeters = 0.01f;
        [SerializeField] private float padHotThresholdC = 60f;

        private Evaluator _evaluator;
        private TestCase _runtimeCase;
        private readonly List<Pad> _pads = new List<Pad>();
        private SolderablePin[] _allPins;
        private SolderableComponent[] _allComponents;

        public Evaluator Evaluator => _evaluator;

        private void Start()
        {
            // Pads live in the scene; a TestCase asset can't hold references to scene objects,
            // so discover them here and feed a runtime copy of the tuning windows (carrying the
            // discovered pads) to the evaluator. If no TestCase is assigned, fall back to default
            // windows so the sim still runs (heating, scoring) instead of going inert.
            _pads.Clear();
            _pads.AddRange(FindObjectsByType<Pad>(FindObjectsSortMode.None));
            _runtimeCase = testCase != null ? Instantiate(testCase) : ScriptableObject.CreateInstance<TestCase>();
            _runtimeCase.TargetPads = new List<Pad>(_pads);
            _evaluator = new Evaluator(_runtimeCase);

            _allPins = FindObjectsByType<SolderablePin>(FindObjectsSortMode.None);
            _allComponents = FindObjectsByType<SolderableComponent>(FindObjectsSortMode.None);

            if (toolBelt != null)
            {
                toolBelt.FluxApplied += OnFluxApplied;
                toolBelt.SolderFed += OnSolderFed;
            }
            if (evalHUD != null)
            {
                evalHUD.ResetRequested += OnResetRequested;
            }
        }

        private void OnDestroy()
        {
            if (toolBelt != null)
            {
                toolBelt.FluxApplied -= OnFluxApplied;
                toolBelt.SolderFed -= OnSolderFed;
            }
            if (evalHUD != null)
            {
                evalHUD.ResetRequested -= OnResetRequested;
            }
            if (_runtimeCase != null) Destroy(_runtimeCase);
        }

        private void Update()
        {
            if (evalHUD == null) return;

            // Find active pad: closest to iron tip or tweezers cursor
            Pad activePad = null;
            float minDist = 0.05f; // within 5cm of iron tip
            var tipPos = iron != null && iron.Tip != null ? iron.Tip.position : (iron != null ? iron.transform.position : Vector3.zero);
            
            foreach (var p in _pads)
            {
                if (p == null) continue;
                float d = Vector3.Distance(p.transform.position, tipPos);
                if (d < minDist)
                {
                    minDist = d;
                    activePad = p;
                }
            }

            if (activePad == null)
            {
                // Fall back to mouse hover raycast
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null && Camera.main != null)
                {
                    var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
                    if (Physics.Raycast(ray, out var hit, 5f))
                    {
                        var pad = hit.collider.GetComponentInParent<Pad>();
                        if (pad != null) activePad = pad;
                    }
                }
            }

            evalHUD.RenderGuidance(activePad, _allPins, _allComponents);
        }

        private void FixedUpdate()
        {
            if (_evaluator == null || iron == null) return;

            float dt = Time.fixedDeltaTime;
            var tipPos = iron.Tip != null ? iron.Tip.position : iron.transform.position;
            var ironForward = iron.Tip != null ? iron.Tip.forward : iron.transform.forward;

            foreach (var pad in _pads)
            {
                if (pad == null) continue;

                var local = pad.transform.InverseTransformPoint(tipPos);
                bool overPad = local.x >= -0.5f && local.x <= 0.5f && local.z >= -0.5f && local.z <= 0.5f;
                bool nearSurface = Mathf.Abs(local.y) <= contactDistanceMeters;
                bool inContact = iron.Energized && overPad && nearSurface;

                pad.Tick(dt, inContact, iron.TipTempC);

                if (inContact)
                {
                    var normalWorld = pad.transform.up.normalized;
                    var fromIron = (-ironForward).normalized;
                    float cos = Mathf.Clamp(Vector3.Dot(fromIron, normalWorld), -1f, 1f);
                    float angleDeg = Mathf.Acos(cos) * Mathf.Rad2Deg;
                    _evaluator.RecordContact(pad, dt, angleDeg, pad.TempC);
                }
            }
        }

        private void OnFluxApplied(ToolDepositEvent e)
        {
            if (_evaluator == null || e.Pad == null) return;
            bool hot = e.Pad.TempC >= padHotThresholdC;
            _evaluator.RecordFluxApplied(e.Pad, padWasHotAlready: hot);
        }

        private void OnSolderFed(ToolDepositEvent e)
        {
            if (_evaluator == null || e.Pad == null) return;
            _evaluator.RecordSolderFed(e.Pad, e.IronTipOnSameCell);
        }

        private void OnResetRequested()
        {
            _evaluator?.Reset();
            evalHUD?.Clear();
        }

        private static float SumSolderVolume(Pad pad)
        {
            var flow = pad.Flow;
            if (flow == null) return 0f;
            float total = 0f;
            for (int x = 0; x < flow.Width; x++)
            {
                for (int y = 0; y < flow.Height; y++)
                {
                    total += flow.GetSolder(x, y);
                }
            }
            return total;
        }
    }
}
