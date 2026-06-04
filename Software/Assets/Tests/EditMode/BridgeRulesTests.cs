using NUnit.Framework;
using VirtualFlux.Sim;

namespace VirtualFlux.Tests
{
    public sealed class BridgeRulesTests
    {
        private const float Form = BridgeRules.DefaultFormVolume;

        [Test]
        public void BridgesWhenBothEdgesMoltenAndOverThreshold()
        {
            Assert.IsTrue(BridgeRules.Bridged(0.1f, 0.1f, true, true, Form));
        }

        [Test]
        public void NoBridgeWhenAnEdgeIsCold()
        {
            Assert.IsFalse(BridgeRules.Bridged(0.2f, 0.2f, true, false, Form));
            Assert.IsFalse(BridgeRules.Bridged(0.2f, 0.2f, false, true, Form));
        }

        [Test]
        public void NoBridgeWhenSolderBelowThreshold()
        {
            Assert.IsFalse(BridgeRules.Bridged(0.02f, 0.02f, true, true, Form));
        }

        [Test]
        public void ThresholdIsCombinedAcrossBothEdges()
        {
            // Each edge alone is below 0.15, but together (0.2) they bridge.
            Assert.IsTrue(BridgeRules.Bridged(0.1f, 0.1f, true, true, 0.15f));
        }
    }
}
