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

            if (toolBelt != null)
            {
                toolBelt.FluxApplied += OnFluxApplied;
                toolBelt.SolderFed += OnSolderFed;
            }
            if (evalHUD != null)
            {
                evalHUD.ScoreRequested += OnScoreRequested;
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
                evalHUD.ScoreRequested -= OnScoreRequested;
                evalHUD.ResetRequested -= OnResetRequested;
            }
            if (_runtimeCase != null) Destroy(_runtimeCase);
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

        private void OnScoreRequested()
        {
            if (_evaluator == null) return;
            foreach (var pad in _pads)
            {
                if (pad == null) continue;
                _evaluator.RecordSolderVolume(pad, SumSolderVolume(pad));
            }
            evalHUD?.Render(_evaluator.Score());
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
