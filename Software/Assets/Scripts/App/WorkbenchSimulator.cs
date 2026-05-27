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
        [SerializeField] private float contactDistanceMeters = 0.005f;
        [SerializeField] private float padHotThresholdC = 60f;

        private Evaluator _evaluator;

        public Evaluator Evaluator => _evaluator;

        private void Start()
        {
            if (testCase == null) return;
            _evaluator = new Evaluator(testCase);
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
        }

        private void FixedUpdate()
        {
            if (_evaluator == null || iron == null || testCase == null) return;

            float dt = Time.fixedDeltaTime;
            var tipPos = iron.Tip != null ? iron.Tip.position : iron.transform.position;
            var ironForward = iron.Tip != null ? iron.Tip.forward : iron.transform.forward;

            foreach (var pad in testCase.TargetPads)
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
            if (_evaluator == null || testCase == null) return;
            foreach (var pad in testCase.TargetPads)
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
