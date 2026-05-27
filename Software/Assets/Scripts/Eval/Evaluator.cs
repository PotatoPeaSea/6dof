using System.Collections.Generic;
using UnityEngine;
using VirtualFlux.Sim;

namespace VirtualFlux.Eval
{
    public sealed class RuleResult
    {
        public string Name;
        public bool Passed;
        public string Detail;
    }

    public sealed class EvalResult
    {
        public bool Passed;
        public List<RuleResult> Rules = new List<RuleResult>();
    }

    public sealed class PadObservation
    {
        public Pad Pad;
        public float DwellSec;
        public float PeakC;
        public float AngleDegSampleSum;
        public int AngleSampleCount;
        public bool FluxAppliedBeforeHeat;
        public bool SolderFedAtIronTip;
        public bool SawBurnt;
        public bool SawLifted;

        public float AvgAngleDeg => AngleSampleCount > 0 ? AngleDegSampleSum / AngleSampleCount : 0f;
    }

    public sealed class Evaluator
    {
        private readonly TestCase _case;
        private readonly Dictionary<Pad, PadObservation> _byPad = new Dictionary<Pad, PadObservation>();

        public Evaluator(TestCase testCase)
        {
            _case = testCase;
            foreach (var p in _case.TargetPads)
            {
                if (p != null) _byPad[p] = new PadObservation { Pad = p };
            }
        }

        public IReadOnlyDictionary<Pad, PadObservation> Observations => _byPad;

        public void RecordContact(Pad pad, float dtSec, float angleFromNormalDeg, float padTempC)
        {
            if (!_byPad.TryGetValue(pad, out var obs)) return;
            obs.DwellSec += dtSec;
            if (padTempC > obs.PeakC) obs.PeakC = padTempC;
            obs.AngleDegSampleSum += angleFromNormalDeg;
            obs.AngleSampleCount++;
            if (pad.Phase == SolderPhase.Burnt) obs.SawBurnt = true;
            if (pad.Phase == SolderPhase.Lifted) obs.SawLifted = true;
        }

        public void RecordFluxApplied(Pad pad, bool padWasHotAlready)
        {
            if (!_byPad.TryGetValue(pad, out var obs)) return;
            if (!padWasHotAlready) obs.FluxAppliedBeforeHeat = true;
        }

        public void RecordSolderFed(Pad pad, bool ironTipPresentOnSameCell)
        {
            if (!_byPad.TryGetValue(pad, out var obs)) return;
            if (ironTipPresentOnSameCell) obs.SolderFedAtIronTip = true;
        }

        public EvalResult Score()
        {
            var result = new EvalResult { Passed = true };
            foreach (var kv in _byPad)
            {
                var p = kv.Key;
                var o = kv.Value;
                AddRule(result, $"{p.name}: dwell in [{_case.MinDwellSec:0.0}, {_case.MaxDwellSec:0.0}]s",
                    o.DwellSec >= _case.MinDwellSec && o.DwellSec <= _case.MaxDwellSec,
                    $"actual {o.DwellSec:0.00}s");
                AddRule(result, $"{p.name}: peak in [{_case.MinPeakC:0}, {_case.MaxPeakC:0}]°C",
                    o.PeakC >= _case.MinPeakC && o.PeakC <= _case.MaxPeakC,
                    $"actual {o.PeakC:0.0}°C");
                AddRule(result, $"{p.name}: angle in [{_case.MinAngleDeg:0}, {_case.MaxAngleDeg:0}]°",
                    o.AvgAngleDeg >= _case.MinAngleDeg && o.AvgAngleDeg <= _case.MaxAngleDeg,
                    $"avg {o.AvgAngleDeg:0.0}°");
                if (_case.RequireFluxBeforeHeat)
                    AddRule(result, $"{p.name}: flux applied before heat", o.FluxAppliedBeforeHeat, "");
                if (_case.RequireSolderFedAtIronTip)
                    AddRule(result, $"{p.name}: solder fed at iron tip", o.SolderFedAtIronTip, "");
                if (_case.ForbidBurnt)
                    AddRule(result, $"{p.name}: not burnt", !o.SawBurnt, "");
                if (_case.ForbidLifted)
                    AddRule(result, $"{p.name}: not lifted", !o.SawLifted, "");
            }
            return result;
        }

        private static void AddRule(EvalResult result, string name, bool passed, string detail)
        {
            result.Rules.Add(new RuleResult { Name = name, Passed = passed, Detail = detail });
            if (!passed) result.Passed = false;
        }
    }
}
