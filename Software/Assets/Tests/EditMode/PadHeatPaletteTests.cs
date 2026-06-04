using NUnit.Framework;
using UnityEngine;
using VirtualFlux.Sim;

namespace VirtualFlux.Tests
{
    public sealed class PadHeatPaletteTests
    {
        [Test]
        public void CoolTemperatureIsCoolColor()
        {
            var c = PadHeatPalette.HeatColor(25f, SolderPhase.Solid, 25f, 350f);
            AssertColorNear(PadHeatPalette.Cool, c);
        }

        [Test]
        public void HotTemperatureIsHotColor()
        {
            var c = PadHeatPalette.HeatColor(350f, SolderPhase.Liquid, 25f, 350f);
            AssertColorNear(PadHeatPalette.Hot, c);
        }

        [Test]
        public void MidTemperatureIsBetweenCoolAndHot()
        {
            var c = PadHeatPalette.HeatColor(187.5f, SolderPhase.Liquid, 25f, 350f);
            AssertColorNear(PadHeatPalette.Warm, c); // midpoint of the ramp
        }

        [Test]
        public void BurntOverridesTemperature()
        {
            var c = PadHeatPalette.HeatColor(25f, SolderPhase.Burnt, 25f, 350f);
            AssertColorNear(PadHeatPalette.Burnt, c);
        }

        [Test]
        public void LiftedOverridesTemperature()
        {
            var c = PadHeatPalette.HeatColor(350f, SolderPhase.Lifted, 25f, 350f);
            AssertColorNear(PadHeatPalette.Lifted, c);
        }

        [Test]
        public void BlobFillClampsToUnitRange()
        {
            Assert.AreEqual(0f, PadHeatPalette.BlobFill01(0f, 1.5f), 1e-5f);
            Assert.AreEqual(1f, PadHeatPalette.BlobFill01(1.5f, 1.5f), 1e-5f);
            Assert.AreEqual(1f, PadHeatPalette.BlobFill01(5f, 1.5f), 1e-5f);
            Assert.AreEqual(0f, PadHeatPalette.BlobFill01(1f, 0f), 1e-5f);
        }

        private static void AssertColorNear(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f, "r");
            Assert.AreEqual(expected.g, actual.g, 1e-3f, "g");
            Assert.AreEqual(expected.b, actual.b, 1e-3f, "b");
        }
    }
}
