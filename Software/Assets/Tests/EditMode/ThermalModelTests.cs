using NUnit.Framework;
using UnityEngine;
using VirtualFlux.Sim;

namespace VirtualFlux.Tests
{
    public sealed class ThermalModelTests
    {
        [Test]
        public void HeatingMatchesAnalyticExponential()
        {
            const float tau = 0.5f;
            const float ironC = 350f;
            const float startC = 25f;
            var m = new ThermalModel(tau, tau, ambientC: 25f, initialTempC: startC);

            const float dt = 0.005f;
            const float horizonSec = 1.0f;
            int steps = Mathf.RoundToInt(horizonSec / dt);
            for (int i = 0; i < steps; i++)
            {
                m.Step(dt, inContact: true, ironTempC: ironC);
            }

            float expected = ironC - (ironC - startC) * Mathf.Exp(-horizonSec / tau);
            Assert.That(m.TempC, Is.EqualTo(expected).Within(0.01f));
        }

        [Test]
        public void CoolingDecaysToAmbient()
        {
            var m = new ThermalModel(0.5f, 0.5f, ambientC: 25f, initialTempC: 200f);

            for (int i = 0; i < 2000; i++)
            {
                m.Step(0.005f, inContact: false, ironTempC: 0f);
            }

            Assert.That(m.TempC, Is.EqualTo(25f).Within(0.1f));
        }
    }
}
