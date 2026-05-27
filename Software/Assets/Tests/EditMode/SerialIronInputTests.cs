using NUnit.Framework;
using VirtualFlux.Input;

namespace VirtualFlux.Tests
{
    public sealed class SerialIronInputTests
    {
        [Test]
        public void ParsesValidPoseLine()
        {
            const string line = "P,10.0,20.0,5.5,30.0,0.0,-45.0,320.0,42";
            bool ok = SerialIronInput.TryParsePoseLine(line, out var sample);

            Assert.IsTrue(ok);
            Assert.AreEqual(42u, sample.Sequence);
            Assert.That(sample.TipTempSetpointC, Is.EqualTo(320f).Within(1e-4f));
            // mm → meters
            Assert.That(sample.Position.x, Is.EqualTo(0.010f).Within(1e-5f));
            Assert.That(sample.Position.y, Is.EqualTo(0.020f).Within(1e-5f));
            Assert.That(sample.Position.z, Is.EqualTo(0.0055f).Within(1e-5f));
        }

        [Test]
        public void RejectsWrongFieldCount()
        {
            Assert.IsFalse(SerialIronInput.TryParsePoseLine("P,1,2,3", out _));
            Assert.IsFalse(SerialIronInput.TryParsePoseLine("", out _));
            Assert.IsFalse(SerialIronInput.TryParsePoseLine("X,0,0,0,0,0,0,0,0", out _));
        }
    }
}
