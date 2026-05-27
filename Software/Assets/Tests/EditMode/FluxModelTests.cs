using NUnit.Framework;
using VirtualFlux.Sim;

namespace VirtualFlux.Tests
{
    public sealed class FluxModelTests
    {
        [Test]
        public void ApplyingFluxOnColdCellEntersColdState()
        {
            var f = new FluxModel();
            f.Apply(0.5f);
            Assert.AreEqual(FluxState.Cold, f.State);
            Assert.That(f.Amount, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void EnteringActivationBandActivatesFlux()
        {
            var f = new FluxModel(tActivateLowC: 150f, tBurnC: 320f);
            f.Apply(1f);
            f.Step(0.1f, tempC: 200f);
            Assert.AreEqual(FluxState.Active, f.State);
        }

        [Test]
        public void ExceedingBurnTempBurnsOutFlux()
        {
            var f = new FluxModel(tActivateLowC: 150f, tBurnC: 320f);
            f.Apply(1f);
            f.Step(0.05f, tempC: 350f);
            Assert.AreEqual(FluxState.Burnt, f.State);
            Assert.That(f.Amount, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ProlongedActivationConsumesFlux()
        {
            var f = new FluxModel(tActivateLowC: 150f, tBurnC: 320f, tActiveSecondsBudget: 1f);
            f.Apply(1f);
            for (int i = 0; i < 250; i++) f.Step(0.005f, tempC: 250f);
            Assert.AreEqual(FluxState.Burnt, f.State);
        }

        [Test]
        public void WettingWeightActiveDominatesNone()
        {
            var none = new FluxModel();
            var active = new FluxModel();
            active.Apply(1f);
            active.Step(0.1f, tempC: 200f);

            Assert.Greater(active.WettingWeight(), none.WettingWeight());
        }
    }
}
