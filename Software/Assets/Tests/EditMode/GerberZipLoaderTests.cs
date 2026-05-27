using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using VirtualFlux.Gerber;

namespace VirtualFlux.Tests
{
    public sealed class GerberZipLoaderTests
    {
        private const string SampleGbr =
            "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,1.5*%\nD10*\nX0Y0D03*\nM02*\n";

        private static string MakeZip(params (string name, string body)[] entries)
        {
            var path = Path.Combine(Path.GetTempPath(), $"vf_test_{System.Guid.NewGuid():N}.zip");
            using (var fs = File.Create(path))
            using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var (name, body) in entries)
                {
                    var entry = z.CreateEntry(name);
                    using var w = new StreamWriter(entry.Open(), Encoding.ASCII);
                    w.Write(body);
                }
            }
            return path;
        }

        [Test]
        public void ReadsBareGbrFile()
        {
            var path = Path.Combine(Path.GetTempPath(), $"vf_test_{System.Guid.NewGuid():N}.gbr");
            File.WriteAllText(path, SampleGbr);
            try
            {
                var text = GerberLoadUI.ReadCopperLayer(path);
                Assert.IsNotNull(text);
                Assert.That(text, Does.Contain("ADD10C"));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void PrefersFCuLayerInZip()
        {
            var path = MakeZip(
                ("Board-Edge_Cuts.gbr", "EDGE"),
                ("Board-B_Cu.gbr",      "BACK"),
                ("Board-F_Cu.gbr",      SampleGbr),
                ("Board-F_Mask.gbr",    "MASK"));
            try
            {
                var text = GerberLoadUI.ReadCopperLayer(path);
                Assert.IsNotNull(text);
                Assert.That(text, Does.Contain("ADD10C"));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void FallsBackToBCuWhenNoFCu()
        {
            var path = MakeZip(
                ("Board-Edge_Cuts.gbr", "EDGE"),
                ("Board-B_Cu.gbr",      SampleGbr));
            try
            {
                var text = GerberLoadUI.ReadCopperLayer(path);
                Assert.IsNotNull(text);
                Assert.That(text, Does.Contain("ADD10C"));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void ReturnsNullForMissingPath()
        {
            var text = GerberLoadUI.ReadCopperLayer("/no/such/file.gbr");
            Assert.IsNull(text);
        }
    }
}
