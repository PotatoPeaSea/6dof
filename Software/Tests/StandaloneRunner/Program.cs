using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using VirtualFlux.Eval;
using VirtualFlux.Gerber;
using VirtualFlux.Sim;

namespace VirtualFlux.StandaloneRunner
{
    internal static class Program
    {
        private static int _pass;
        private static int _fail;

        private static int Main(string[] args)
        {
            Console.WriteLine("VirtualFlux standalone tests");
            Console.WriteLine("============================");

            Run("Parser: aperture defs (C and R)", ParserApertures);
            Run("Parser: flash coords scaled by FS LAX46", ParserFlashCoords);
            Run("Parser: bounding box from flashes", ParserBoundingBox);
            Run("Parser: G36/G37 region accumulation", ParserRegion);
            Run("Parser: obround aperture (O)", ParserObround);
            Run("Parser: imperial units (MOIN)", ParserImperial);
            Run("Parser: trailing M02 doesn't crash", ParserTrailingM02);
            Run("Loader: reads bare .gbr file", LoaderBareFile);
            Run("Loader: prefers F_Cu inside zip", LoaderZipPrefersFCu);
            Run("Loader: falls back to B_Cu if no F_Cu", LoaderZipFallbackBCu);
            Run("Loader: falls back to any .gbr if no copper-named layer", LoaderZipFallbackAny);
            Run("Loader: returns null for nonexistent path", LoaderMissing);
            Run("Loader: returns null for zip with no .gbr entries", LoaderZipNoGbr);
            Run("End-to-end: parse F_Cu pulled from a synthetic zip", EndToEndZip);

            Run("ThermalModel: heating approaches iron temp",      ThermalHeating);
            Run("ThermalModel: cooling approaches ambient",        ThermalCooling);
            Run("FluxModel: cold→active→burnout from budget",      FluxBudgetBurnout);
            Run("FluxModel: cold→active above 320°C burns out",    FluxOverTempBurnout);
            Run("SolderFlow: molten solder spreads into wetted",   SolderSpreadsToWetted);
            Run("SolderFlow: cold cells block flow",               SolderColdBoundary);

            Run("Evaluator: ideal run passes all rules",           EvalIdealRun);
            Run("Evaluator: flux after heat fails flux rule",      EvalFluxAfterHeat);
            Run("Evaluator: bad angle fails angle rule",           EvalBadAngle);
            Run("Evaluator: no solder fed fails solder rule",      EvalNoSolder);
            Run("Evaluator: solder volume below window fails",     EvalVolumeBelow);
            Run("Evaluator: solder volume above window fails",     EvalVolumeAbove);
            Run("Evaluator: Reset() clears observations",          EvalReset);

            if (args.Length > 0 && args[0] == "--real")
            {
                Run("Repo: parse Hardware/Gerbers/MCUGerbers.zip if present", RepoMcuZip);
            }

            Console.WriteLine("============================");
            Console.WriteLine($"{_pass} passed, {_fail} failed");
            return _fail == 0 ? 0 : 1;
        }

        private static void Run(string name, Action body)
        {
            try
            {
                body();
                _pass++;
                Console.WriteLine($"  OK   {name}");
            }
            catch (Exception e)
            {
                _fail++;
                Console.WriteLine($"  FAIL {name}: {e.Message}");
            }
        }

        // ----- Parser tests -----

        private const string SimpleGbr =
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

        private static void ParserApertures()
        {
            var f = GerberParser.Parse(SimpleGbr);
            AssertEq(2, f.Apertures.Count, "aperture count");
            AssertEq(ApertureShape.Circle, f.Apertures[10].Shape, "D10 shape");
            AssertNear(1.5f, f.Apertures[10].SizeX, 1e-4f, "D10 size");
            AssertEq(ApertureShape.Rectangle, f.Apertures[11].Shape, "D11 shape");
            AssertNear(2.0f, f.Apertures[11].SizeX, 1e-4f, "D11 sizeX");
            AssertNear(1.0f, f.Apertures[11].SizeY, 1e-4f, "D11 sizeY");
        }

        private static void ParserFlashCoords()
        {
            var f = GerberParser.Parse(SimpleGbr);
            AssertEq(3, f.Primitives.Count, "primitive count");
            AssertNear(5.0f, f.Primitives[1].Points[0].x, 1e-3f, "second flash x");
            AssertNear(5.0f, f.Primitives[2].Points[0].y, 1e-3f, "third flash y");
        }

        private static void ParserBoundingBox()
        {
            var f = GerberParser.Parse(SimpleGbr);
            AssertNear(0f, f.Min.x, 1e-3f, "min x");
            AssertNear(5.0f, f.Max.x, 1e-3f, "max x");
            AssertNear(5.0f, f.Max.y, 1e-3f, "max y");
        }

        private static void ParserRegion()
        {
            const string src =
                "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,0.1*%\nD10*\n" +
                "G36*\n" +
                "X0Y0D02*\n" +
                "X1000000Y0D01*\n" +
                "X1000000Y1000000D01*\n" +
                "X0Y1000000D01*\n" +
                "X0Y0D01*\n" +
                "G37*\n" +
                "M02*\n";
            var f = GerberParser.Parse(src);
            int regions = 0;
            foreach (var p in f.Primitives) if (p.Kind == PrimitiveKind.Region) regions++;
            AssertEq(1, regions, "region count");
        }

        private static void ParserObround()
        {
            const string src = "%FSLAX46Y46*%\n%MOMM*%\n%ADD12O,2.0X1.0*%\nD12*\nX0Y0D03*\nM02*\n";
            var f = GerberParser.Parse(src);
            AssertEq(ApertureShape.Obround, f.Apertures[12].Shape, "obround shape");
            AssertNear(2.0f, f.Apertures[12].SizeX, 1e-4f, "obround sizeX");
            AssertNear(1.0f, f.Apertures[12].SizeY, 1e-4f, "obround sizeY");
        }

        private static void ParserImperial()
        {
            // 1 inch flash → 25.4 mm.
            const string src = "%FSLAX46Y46*%\n%MOIN*%\n%ADD10C,0.04*%\nD10*\nX1000000Y0D03*\nM02*\n";
            var f = GerberParser.Parse(src);
            AssertNear(25.4f, f.Primitives[0].Points[0].x, 1e-2f, "imperial x scaled");
        }

        private static void ParserTrailingM02()
        {
            // Should not throw, should produce the one flash we asked for.
            var f = GerberParser.Parse(SimpleGbr);
            if (f.Primitives.Count < 1) throw new Exception("no primitives");
        }

        // ----- Loader tests -----

        private const string MiniCu =
            "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,1.5*%\nD10*\nX0Y0D03*\nM02*\n";

        private static string MakeTempFile(string ext, string body)
        {
            var p = Path.Combine(Path.GetTempPath(), $"vf_{Guid.NewGuid():N}{ext}");
            File.WriteAllText(p, body);
            return p;
        }

        private static string MakeZip(params (string name, string body)[] entries)
        {
            var p = Path.Combine(Path.GetTempPath(), $"vf_{Guid.NewGuid():N}.zip");
            using var fs = File.Create(p);
            using var z = new ZipArchive(fs, ZipArchiveMode.Create);
            foreach (var (name, body) in entries)
            {
                var e = z.CreateEntry(name);
                using var w = new StreamWriter(e.Open(), Encoding.ASCII);
                w.Write(body);
            }
            return p;
        }

        private static void LoaderBareFile()
        {
            var p = MakeTempFile(".gbr", MiniCu);
            try
            {
                var t = GerberZipReader.ReadCopperLayer(p);
                if (t == null) throw new Exception("returned null");
                if (!t.Contains("ADD10C")) throw new Exception("wrong content");
            }
            finally { File.Delete(p); }
        }

        private static void LoaderZipPrefersFCu()
        {
            var p = MakeZip(
                ("Board-Edge_Cuts.gbr", "EDGE"),
                ("Board-B_Cu.gbr",      "BACK"),
                ("Board-F_Cu.gbr",      MiniCu),
                ("Board-F_Mask.gbr",    "MASK"));
            try
            {
                var t = GerberZipReader.ReadCopperLayer(p);
                if (t == null) throw new Exception("returned null");
                if (!t.Contains("ADD10C")) throw new Exception("got wrong entry: " + Truncate(t));
            }
            finally { File.Delete(p); }
        }

        private static void LoaderZipFallbackBCu()
        {
            var p = MakeZip(
                ("Board-Edge_Cuts.gbr", "EDGE"),
                ("Board-B_Cu.gbr",      MiniCu));
            try
            {
                var t = GerberZipReader.ReadCopperLayer(p);
                if (t == null) throw new Exception("returned null");
                if (!t.Contains("ADD10C")) throw new Exception("did not pick B_Cu");
            }
            finally { File.Delete(p); }
        }

        private static void LoaderZipFallbackAny()
        {
            var p = MakeZip(
                ("Mystery.gbr", MiniCu));
            try
            {
                var t = GerberZipReader.ReadCopperLayer(p);
                if (t == null) throw new Exception("returned null");
                if (!t.Contains("ADD10C")) throw new Exception("did not pick fallback");
            }
            finally { File.Delete(p); }
        }

        private static void LoaderMissing()
        {
            var t = GerberZipReader.ReadCopperLayer("/no/such/file.gbr");
            if (t != null) throw new Exception("expected null for missing path, got " + Truncate(t));
        }

        private static void LoaderZipNoGbr()
        {
            var p = MakeZip(("readme.txt", "hi"));
            try
            {
                var t = GerberZipReader.ReadCopperLayer(p);
                if (t != null) throw new Exception("expected null when no .gbr in zip");
            }
            finally { File.Delete(p); }
        }

        private static void EndToEndZip()
        {
            var p = MakeZip(("Sample-F_Cu.gbr", MiniCu));
            try
            {
                var text = GerberZipReader.ReadCopperLayer(p);
                var f = GerberParser.Parse(text);
                AssertEq(1, f.Apertures.Count, "apertures");
                AssertEq(1, f.Primitives.Count, "primitives");
                AssertEq(ApertureShape.Circle, f.Apertures[10].Shape, "shape");
            }
            finally { File.Delete(p); }
        }

        private static void RepoMcuZip()
        {
            var p = LocateRepoFile("Hardware/Gerbers/MCUGerbers.zip");
            if (p == null || !File.Exists(p))
            {
                Console.WriteLine("  (skip, MCUGerbers.zip not found in repo)");
                return;
            }
            var text = GerberZipReader.ReadCopperLayer(p);
            if (text == null) throw new Exception("no copper layer extracted from real zip");
            var f = GerberParser.Parse(text);
            if (f.Apertures.Count == 0) throw new Exception("real zip parsed to zero apertures");
            if (f.Primitives.Count == 0) throw new Exception("real zip parsed to zero primitives");
            Console.WriteLine($"       real zip: {f.Apertures.Count} apertures, {f.Primitives.Count} primitives, {f.SizeMm.x:0.0}x{f.SizeMm.y:0.0} mm");
        }

        // ----- sim tests -----

        private static void ThermalHeating()
        {
            var t = new ThermalModel(tauHeatSec: 0.5f, tauCoolSec: 1.0f, ambientC: 25f, initialTempC: 25f);
            for (int i = 0; i < 1000; i++) t.Step(0.005f, inContact: true, ironTempC: 350f);
            AssertNear(350f, t.TempC, 1f, "asymptote");
        }

        private static void ThermalCooling()
        {
            var t = new ThermalModel(0.5f, 1.0f, ambientC: 25f, initialTempC: 350f);
            for (int i = 0; i < 2000; i++) t.Step(0.005f, inContact: false, ironTempC: 0f);
            AssertNear(25f, t.TempC, 1f, "cooled to ambient");
        }

        private static void FluxBudgetBurnout()
        {
            var f = new FluxModel(tActivateLowC: 150f, tBurnC: 320f, tActiveSecondsBudget: 1f);
            f.Apply(0.8f);
            if (f.State != FluxState.Cold) throw new Exception($"after Apply expected Cold, got {f.State}");
            // Hold at 250 °C for 2 s — exceeds 1 s budget.
            for (int i = 0; i < 400; i++) f.Step(0.005f, 250f);
            if (f.State != FluxState.Burnt) throw new Exception($"expected Burnt, got {f.State}");
        }

        private static void FluxOverTempBurnout()
        {
            var f = new FluxModel(tBurnC: 320f);
            f.Apply(1f);
            f.Step(0.01f, tempC: 400f);
            if (f.State != FluxState.Burnt) throw new Exception($"expected Burnt at 400°C, got {f.State}");
        }

        private static void SolderSpreadsToWetted()
        {
            var grid = new SolderFlow(width: 5, height: 1, meltingPointC: 183f);
            // Heat the whole row above melting, apply flux to neighbors, drop solder in middle.
            for (int x = 0; x < 5; x++) { grid.SetTemp(x, 0, 250f); grid.ApplyFlux(x, 0, 1f); }
            // Let flux activate.
            for (int i = 0; i < 5; i++) grid.Step(0.05f);
            grid.AddSolder(2, 0, 1.0f);
            for (int i = 0; i < 60; i++) grid.Step(0.05f);
            float total = 0f;
            for (int x = 0; x < 5; x++) total += grid.GetSolder(x, 0);
            AssertNear(1.0f, total, 0.01f, "mass conservation");
            // Adjacent cells should have gained some solder.
            if (grid.GetSolder(1, 0) <= 0f || grid.GetSolder(3, 0) <= 0f)
                throw new Exception("solder did not spread to wetted neighbors");
        }

        private static void SolderColdBoundary()
        {
            var grid = new SolderFlow(width: 3, height: 1, meltingPointC: 183f);
            grid.SetTemp(0, 0, 250f); grid.ApplyFlux(0, 0, 1f);
            grid.SetTemp(1, 0, 250f); grid.ApplyFlux(1, 0, 1f);
            grid.SetTemp(2, 0,  25f); // cold boundary
            for (int i = 0; i < 5; i++) grid.Step(0.05f);
            grid.AddSolder(1, 0, 1.0f);
            for (int i = 0; i < 60; i++) grid.Step(0.05f);
            if (grid.GetSolder(2, 0) != 0f)
                throw new Exception($"cold cell received solder: {grid.GetSolder(2, 0)}");
        }

        // ----- evaluator tests -----

        private static (TestCase tc, Pad pad) MakeCase(float minVol = 0.2f, float maxVol = 1.5f)
        {
            var pad = new Pad();
            ((MonoBehaviour)pad).name = "Pad";
            pad.InvokeAwake();
            var tc = ScriptableObject.CreateInstance<TestCase>();
            tc.TargetPads.Add(pad);
            tc.MinDwellSec = 1f; tc.MaxDwellSec = 3f;
            tc.MinPeakC = 250f; tc.MaxPeakC = 360f;
            tc.MinAngleDeg = 40f; tc.MaxAngleDeg = 55f;
            tc.MinSolderVolume = minVol; tc.MaxSolderVolume = maxVol;
            return (tc, pad);
        }

        private static void DriveGoodProcess(Evaluator ev, Pad pad)
        {
            ev.RecordFluxApplied(pad, padWasHotAlready: false);
            for (int i = 0; i < 400; i++) ev.RecordContact(pad, 0.005f, 45f, 320f);
            ev.RecordSolderFed(pad, ironTipPresentOnSameCell: true);
        }

        private static void EvalIdealRun()
        {
            var (tc, pad) = MakeCase();
            var ev = new Evaluator(tc);
            DriveGoodProcess(ev, pad);
            ev.RecordSolderVolume(pad, 0.8f);
            var r = ev.Score();
            if (!r.Passed) throw new Exception("ideal run failed: " + FormatRules(r));
        }

        private static void EvalFluxAfterHeat()
        {
            var (tc, pad) = MakeCase();
            var ev = new Evaluator(tc);
            ev.RecordFluxApplied(pad, padWasHotAlready: true);
            for (int i = 0; i < 400; i++) ev.RecordContact(pad, 0.005f, 45f, 320f);
            ev.RecordSolderFed(pad, true);
            ev.RecordSolderVolume(pad, 0.8f);
            var r = ev.Score();
            if (r.Passed) throw new Exception("expected fail");
            if (!r.Rules.Exists(x => !x.Passed && x.Name.Contains("flux applied before heat")))
                throw new Exception("expected flux-before-heat rule to fail; got: " + FormatRules(r));
        }

        private static void EvalBadAngle()
        {
            var (tc, pad) = MakeCase();
            var ev = new Evaluator(tc);
            ev.RecordFluxApplied(pad, padWasHotAlready: false);
            for (int i = 0; i < 400; i++) ev.RecordContact(pad, 0.005f, 85f, 320f);  // too steep
            ev.RecordSolderFed(pad, true);
            ev.RecordSolderVolume(pad, 0.8f);
            var r = ev.Score();
            if (r.Passed) throw new Exception("expected fail");
            if (!r.Rules.Exists(x => !x.Passed && x.Name.Contains("angle")))
                throw new Exception("expected angle rule fail; got: " + FormatRules(r));
        }

        private static void EvalNoSolder()
        {
            var (tc, pad) = MakeCase();
            var ev = new Evaluator(tc);
            ev.RecordFluxApplied(pad, padWasHotAlready: false);
            for (int i = 0; i < 400; i++) ev.RecordContact(pad, 0.005f, 45f, 320f);
            // skip RecordSolderFed
            ev.RecordSolderVolume(pad, 0.8f);
            var r = ev.Score();
            if (r.Passed) throw new Exception("expected fail");
            if (!r.Rules.Exists(x => !x.Passed && x.Name.Contains("solder fed at iron tip")))
                throw new Exception("expected solder-fed rule fail; got: " + FormatRules(r));
        }

        private static void EvalVolumeBelow()
        {
            var (tc, pad) = MakeCase(minVol: 0.2f, maxVol: 1.5f);
            var ev = new Evaluator(tc);
            DriveGoodProcess(ev, pad);
            ev.RecordSolderVolume(pad, 0.05f);
            var r = ev.Score();
            if (r.Passed) throw new Exception("expected fail");
            if (!r.Rules.Exists(x => !x.Passed && x.Name.Contains("solder volume")))
                throw new Exception("expected volume rule fail; got: " + FormatRules(r));
        }

        private static void EvalVolumeAbove()
        {
            var (tc, pad) = MakeCase(minVol: 0.2f, maxVol: 1.5f);
            var ev = new Evaluator(tc);
            DriveGoodProcess(ev, pad);
            ev.RecordSolderVolume(pad, 3.0f);
            var r = ev.Score();
            if (r.Passed) throw new Exception("expected fail");
            if (!r.Rules.Exists(x => !x.Passed && x.Name.Contains("solder volume")))
                throw new Exception("expected volume rule fail; got: " + FormatRules(r));
        }

        private static void EvalReset()
        {
            var (tc, pad) = MakeCase();
            var ev = new Evaluator(tc);
            DriveGoodProcess(ev, pad);
            ev.RecordSolderVolume(pad, 0.8f);
            if (!ev.Score().Passed) throw new Exception("setup did not pass");
            ev.Reset();
            var r = ev.Score();
            if (r.Passed) throw new Exception("expected reset state to fail multiple rules");
        }

        private static string FormatRules(EvalResult r)
        {
            var sb = new StringBuilder();
            foreach (var x in r.Rules) sb.AppendLine($"    {(x.Passed?"OK ":"X  ")} {x.Name} {x.Detail}");
            return sb.ToString();
        }

        // ----- helpers -----

        private static void AssertEq<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"{what}: expected {expected}, got {actual}");
        }

        private static void AssertNear(float expected, float actual, float tol, string what)
        {
            if (Math.Abs(expected - actual) > tol)
                throw new Exception($"{what}: expected {expected} ±{tol}, got {actual}");
        }

        private static string Truncate(string s) => s.Length > 80 ? s.Substring(0, 80) + "…" : s;

        private static string LocateRepoFile(string relative)
        {
            var dir = AppContext.BaseDirectory;
            for (int hop = 0; hop < 8; hop++)
            {
                var candidate = Path.Combine(dir, relative);
                if (File.Exists(candidate)) return candidate;
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return null;
        }
    }
}
