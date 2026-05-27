using NUnit.Framework;
using UnityEngine;
using VirtualFlux.Eval;
using VirtualFlux.Sim;

namespace VirtualFlux.Tests
{
    public sealed class SolderVolumeRuleTests
    {
        private static (TestCase tc, Pad pad, GameObject host) MakeCase(float min, float max)
        {
            var host = new GameObject("Pad");
            var pad = host.AddComponent<Pad>();
            var tc = ScriptableObject.CreateInstance<TestCase>();
            tc.TargetPads.Add(pad);
            tc.MinDwellSec = 1f; tc.MaxDwellSec = 3f;
            tc.MinPeakC = 250f; tc.MaxPeakC = 360f;
            tc.MinAngleDeg = 40f; tc.MaxAngleDeg = 55f;
            tc.MinSolderVolume = min;
            tc.MaxSolderVolume = max;
            return (tc, pad, host);
        }

        private static void DriveValidProcess(Evaluator ev, Pad pad)
        {
            ev.RecordFluxApplied(pad, padWasHotAlready: false);
            for (int i = 0; i < 400; i++) ev.RecordContact(pad, 0.005f, 45f, 320f);
            ev.RecordSolderFed(pad, ironTipPresentOnSameCell: true);
        }

        [Test]
        public void VolumeInsideWindowPasses()
        {
            var (tc, pad, host) = MakeCase(0.2f, 1.5f);
            try
            {
                var ev = new Evaluator(tc);
                DriveValidProcess(ev, pad);
                ev.RecordSolderVolume(pad, 0.8f);
                var r = ev.Score();
                Assert.IsTrue(r.Passed, FormatRules(r));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(tc); }
        }

        [Test]
        public void TooLittleSolderFails()
        {
            var (tc, pad, host) = MakeCase(0.2f, 1.5f);
            try
            {
                var ev = new Evaluator(tc);
                DriveValidProcess(ev, pad);
                ev.RecordSolderVolume(pad, 0.05f);
                var r = ev.Score();
                Assert.IsFalse(r.Passed);
                Assert.That(r.Rules.Exists(x => !x.Passed && x.Name.Contains("solder volume")));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(tc); }
        }

        [Test]
        public void TooMuchSolderFails()
        {
            var (tc, pad, host) = MakeCase(0.2f, 1.5f);
            try
            {
                var ev = new Evaluator(tc);
                DriveValidProcess(ev, pad);
                ev.RecordSolderVolume(pad, 3.0f);
                var r = ev.Score();
                Assert.IsFalse(r.Passed);
                Assert.That(r.Rules.Exists(x => !x.Passed && x.Name.Contains("solder volume")));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(tc); }
        }

        [Test]
        public void ResetClearsObservations()
        {
            var (tc, pad, host) = MakeCase(0.2f, 1.5f);
            try
            {
                var ev = new Evaluator(tc);
                DriveValidProcess(ev, pad);
                ev.RecordSolderVolume(pad, 0.8f);
                Assert.IsTrue(ev.Score().Passed);

                ev.Reset();
                // After reset, no dwell, no flux, no solder fed → should fail multiple rules.
                var r = ev.Score();
                Assert.IsFalse(r.Passed);
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(tc); }
        }

        private static string FormatRules(EvalResult r)
            => string.Join("\n", r.Rules.ConvertAll(x => (x.Passed ? "OK " : "X  ") + x.Name + " " + x.Detail));
    }
}
