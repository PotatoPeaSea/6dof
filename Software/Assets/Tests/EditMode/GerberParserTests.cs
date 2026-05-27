using NUnit.Framework;
using VirtualFlux.Gerber;

namespace VirtualFlux.Tests
{
    public sealed class GerberParserTests
    {
        private const string Sample =
            "%FSLAX46Y46*%\n" +
            "%MOMM*%\n" +
            "%ADD10C,1.5*%\n" +
            "%ADD11R,2.0X1.0*%\n" +
            "D10*\n" +
            "X0Y0D03*\n" +
            "X5000000Y0D03*\n" +
            "D11*\n" +
            "X0Y5000000D03*\n" +
            "M02*\n";

        [Test]
        public void ParsesApertureDefinitions()
        {
            var file = GerberParser.Parse(Sample);
            Assert.AreEqual(2, file.Apertures.Count);
            Assert.AreEqual(ApertureShape.Circle, file.Apertures[10].Shape);
            Assert.That(file.Apertures[10].SizeX, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.AreEqual(ApertureShape.Rectangle, file.Apertures[11].Shape);
            Assert.That(file.Apertures[11].SizeX, Is.EqualTo(2.0f).Within(1e-4f));
            Assert.That(file.Apertures[11].SizeY, Is.EqualTo(1.0f).Within(1e-4f));
        }

        [Test]
        public void ParsesFlashPrimitivesAtScaledCoords()
        {
            var file = GerberParser.Parse(Sample);
            Assert.AreEqual(3, file.Primitives.Count);
            // X5000000 with LAX46 → 5.0 mm.
            Assert.That(file.Primitives[1].Points[0].x, Is.EqualTo(5.0f).Within(1e-3f));
            Assert.That(file.Primitives[2].Points[0].y, Is.EqualTo(5.0f).Within(1e-3f));
        }

        [Test]
        public void TracksBoundingBox()
        {
            var file = GerberParser.Parse(Sample);
            Assert.That(file.Min.x, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(file.Max.x, Is.EqualTo(5.0f).Within(1e-3f));
            Assert.That(file.Max.y, Is.EqualTo(5.0f).Within(1e-3f));
        }
    }
}
