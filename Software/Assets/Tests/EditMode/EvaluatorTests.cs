using NUnit.Framework;
using UnityEngine;
using VirtualFlux.Eval;
using VirtualFlux.Sim;

namespace VirtualFlux.Tests
{
    public sealed class EvaluatorTests
    {
        private static (TestCase tc, Pad pad, GameObject host) MakeCase()
        {
            var host = new GameObject("Pad");
            var pad = host.AddComponent<Pad>();
            // Pad.Awake() ran already.
            var tc = ScriptableObject.CreateInstance<TestCase>();
            tc.TargetPads.Add(pad);
            tc.MinDwellSec = 1f; tc.MaxDwellSec = 3f;
            tc.MinPeakC = 250f; tc.MaxPeakC = 360f;
            tc.MinAngleDeg = 40f; tc.MaxAngleDeg = 55f;
            return (tc, pad, host);
        }

        [Test]
        public void IdealRunPassesAllRules()
        {
            var (tc, pad, host) = MakeCase();
            try
            {
                var ev = new Evaluator(tc);
                ev.RecordFluxApplied(pad, padWasHotAlready: false);
                for (int i = 0; i < 400; i++) ev.RecordContact(pad, 0.005f, 45f, 320f);
                ev.RecordSolderFed(pad, ironTipPresentOnSameCell: true);

                var r = ev.Score();
                Assert.IsTrue(r.Passed, "ideal run should pass; got: " +
                    string.Join("\n", r.Rules.ConvertAll(x => (x.Passed ? "OK " : "X  ") + x.Name + " " + x.Detail)));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(tc); }
        }

        [Test]
        public void FluxAfterHeatFailsFluxRule()
        {
            var (tc, pad, host) = MakeCase();
            try
            {
                var ev = new Evaluator(tc);
                ev.RecordFluxApplied(pad, padWasHotAlready: true);  // wrong order
                for (int i = 0; i < 400; i++) ev.RecordContact(pad, 0.005f, 45f, 320f);
                ev.RecordSolderFed(pad, ironTipPresentOnSameCell: true);

                var r = ev.Score();
                Assert.IsFalse(r.Passed);
                Assert.That(r.Rules.Exists(x => !x.Passed && x.Name.Contains("flux applied before heat")));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(tc); }
        }

        [Test]
        public void BadAngleFailsAngleRule()
        {
            var (tc, pad, host) = MakeCase();
            try
            {
                var ev = new Evaluator(tc);
                ev.RecordFluxApplied(pad, padWasHotAlready: false);
                for (int i = 0; i < 400; i++) ev.RecordContact(pad, 0.005f, 85f, 320f);
                ev.RecordSolderFed(pad, ironTipPresentOnSameCell: true);

                var r = ev.Score();
                Assert.IsFalse(r.Passed);
                Assert.That(r.Rules.Exists(x => !x.Passed && x.Name.Contains("angle")));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(tc); }
        }

        [Test]
        public void DryJointFailsSolderRule()
        {
            var (tc, pad, host) = MakeCase();
            try
            {
                var ev = new Evaluator(tc);
                ev.RecordFluxApplied(pad, padWasHotAlready: false);
                for (int i = 0; i < 400; i++) ev.RecordContact(pad, 0.005f, 45f, 320f);
                // never feed solder

                var r = ev.Score();
                Assert.IsFalse(r.Passed);
                Assert.That(r.Rules.Exists(x => !x.Passed && x.Name.Contains("solder fed at iron tip")));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(tc); }
        }
    }
}
