using UnityEngine;

namespace VirtualFlux.Sim
{
    /// <summary>
    /// First-order lumped capacitance heat model. Pure logic, no Unity dependencies
    /// beyond Mathf so it can be exercised from EditMode tests without a scene.
    ///
    /// Heating  (tip in contact): dT/dt = (T_iron  − T_pad) / tauHeat
    /// Cooling  (no contact):     dT/dt = (T_amb   − T_pad) / tauCool
    /// </summary>
    public sealed class ThermalModel
    {
        public float TauHeatSec { get; }
        public float TauCoolSec { get; }
        public float AmbientC { get; }

        public float TempC { get; private set; }

        public ThermalModel(float tauHeatSec, float tauCoolSec, float ambientC, float initialTempC)
        {
            if (tauHeatSec <= 0f) tauHeatSec = 0.01f;
            if (tauCoolSec <= 0f) tauCoolSec = 0.01f;
            TauHeatSec = tauHeatSec;
            TauCoolSec = tauCoolSec;
            AmbientC = ambientC;
            TempC = initialTempC;
        }

        public void Step(float dtSec, bool inContact, float ironTempC)
        {
            if (dtSec <= 0f) return;

            float target = inContact ? ironTempC : AmbientC;
            float tau = inContact ? TauHeatSec : TauCoolSec;

            // Exact exponential update: T_{n+1} = target + (T_n - target) * exp(-dt/tau)
            float decay = Mathf.Exp(-dtSec / tau);
            TempC = target + (TempC - target) * decay;
        }

        public void Reset(float tempC)
        {
            TempC = tempC;
        }
    }
}
