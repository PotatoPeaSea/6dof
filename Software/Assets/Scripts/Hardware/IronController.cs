using UnityEngine;
using VirtualFlux.Input;

namespace VirtualFlux.Hardware
{
    /// <summary>
    /// Applies the latest <see cref="IronSample"/> from an <see cref="IIronInputSource"/>
    /// to the iron transform, and exposes the tip transform + current temperature setpoint
    /// to the sim layer.
    /// </summary>
    public sealed class IronController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSourceBehaviour;
        [SerializeField] private Transform tip;
        [SerializeField] private float ambientC = 25f;
        [SerializeField] private float warmupRateCPerSec = 80f;
        [SerializeField] private float cooldownRateCPerSec = 30f;

        private IIronInputSource _inputSource;

        public Transform Tip => tip;
        public float TipTempC { get; private set; }
        public bool Energized { get; private set; }

        private void Awake()
        {
            _inputSource = inputSourceBehaviour as IIronInputSource;
            TipTempC = ambientC;
        }

        private void Update()
        {
            if (_inputSource == null)
            {
                return;
            }

            _inputSource.Tick(Time.deltaTime);
            var sample = _inputSource.Latest;

            transform.SetPositionAndRotation(sample.Position, sample.Rotation);

            Energized = sample.Energized;
            var target = sample.Energized ? sample.TipTempSetpointC : ambientC;
            var rate = target > TipTempC ? warmupRateCPerSec : cooldownRateCPerSec;
            TipTempC = Mathf.MoveTowards(TipTempC, target, rate * Time.deltaTime);
        }
    }
}
