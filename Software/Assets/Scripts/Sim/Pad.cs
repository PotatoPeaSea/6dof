using UnityEngine;

namespace VirtualFlux.Sim
{
    /// <summary>
    /// Authored on a pad mesh. Owns one ThermalModel + one SolderFlow grid. The runtime
    /// fills in tip contact and iron temperature from <see cref="Hardware.IronController"/>.
    /// </summary>
    public sealed class Pad : MonoBehaviour
    {
        [SerializeField] private float thermalMassRelative = 1f;
        [SerializeField] private float meltingPointC = SolderStateRules.DefaultMeltingPointC;
        [SerializeField] private float maxToleranceC = 380f;
        [SerializeField] private int gridWidth = 16;
        [SerializeField] private int gridHeight = 16;

        private ThermalModel _thermal;
        private SolderFlow _flow;
        private SolderPhase _phase = SolderPhase.Solid;
        private float _secondsAboveMaxTolerance;

        public ThermalModel Thermal => _thermal;
        public SolderFlow Flow => _flow;
        public SolderPhase Phase => _phase;
        public float TempC => _thermal?.TempC ?? 0f;

        private void Awake()
        {
            float tauHeat = 0.3f * thermalMassRelative;
            float tauCool = 1.2f * thermalMassRelative;
            _thermal = new ThermalModel(tauHeat, tauCool, ambientC: 25f, initialTempC: 25f);
            _flow = new SolderFlow(gridWidth, gridHeight, meltingPointC);
        }

        public void Tick(float dtSec, bool inContact, float ironTempC)
        {
            _thermal.Step(dtSec, inContact, ironTempC);

            float t = _thermal.TempC;
            for (int x = 0; x < _flow.Width; x++)
            {
                for (int y = 0; y < _flow.Height; y++)
                {
                    _flow.SetTemp(x, y, t);
                }
            }
            _flow.Step(dtSec);

            if (t >= maxToleranceC)
            {
                _secondsAboveMaxTolerance += dtSec;
            }
            else
            {
                _secondsAboveMaxTolerance = Mathf.Max(0f, _secondsAboveMaxTolerance - dtSec);
            }

            _phase = SolderStateRules.Transition(
                _phase,
                t,
                meltingPointC,
                maxToleranceC,
                _secondsAboveMaxTolerance,
                SolderStateRules.DefaultBurnSecondsAtMaxC,
                SolderStateRules.DefaultLiftSecondsBeyondBurn);
        }
    }
}
