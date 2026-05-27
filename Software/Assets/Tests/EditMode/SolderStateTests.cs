using NUnit.Framework;
using VirtualFlux.Sim;

namespace VirtualFlux.Tests
{
    public sealed class SolderStateTests
    {
        [Test]
        public void BelowMeltingPointStaysSolid()
        {
            var phase = SolderStateRules.Transition(
                SolderPhase.Solid, tempC: 100f,
                meltingPointC: 183f, maxToleranceC: 380f,
                secondsAboveMaxTolerance: 0f, burnSeconds: 3f, liftSeconds: 5f);
            Assert.AreEqual(SolderPhase.Solid, phase);
        }

        [Test]
        public void AboveMeltingPointGoesLiquid()
        {
            var phase = SolderStateRules.Transition(
                SolderPhase.Solid, tempC: 250f,
                meltingPointC: 183f, maxToleranceC: 380f,
                secondsAboveMaxTolerance: 0f, burnSeconds: 3f, liftSeconds: 5f);
            Assert.AreEqual(SolderPhase.Liquid, phase);
        }

        [Test]
        public void SustainedOverheatBurns()
        {
            var phase = SolderStateRules.Transition(
                SolderPhase.Liquid, tempC: 400f,
                meltingPointC: 183f, maxToleranceC: 380f,
                secondsAboveMaxTolerance: 3.5f, burnSeconds: 3f, liftSeconds: 5f);
            Assert.AreEqual(SolderPhase.Burnt, phase);
        }

        [Test]
        public void ExtremeOverheatLiftsPad()
        {
            var phase = SolderStateRules.Transition(
                SolderPhase.Burnt, tempC: 400f,
                meltingPointC: 183f, maxToleranceC: 380f,
                secondsAboveMaxTolerance: 9f, burnSeconds: 3f, liftSeconds: 5f);
            Assert.AreEqual(SolderPhase.Lifted, phase);
        }

        [Test]
        public void LiftedIsTerminal()
        {
            var phase = SolderStateRules.Transition(
                SolderPhase.Lifted, tempC: 25f,
                meltingPointC: 183f, maxToleranceC: 380f,
                secondsAboveMaxTolerance: 0f, burnSeconds: 3f, liftSeconds: 5f);
            Assert.AreEqual(SolderPhase.Lifted, phase);
        }
    }
}
