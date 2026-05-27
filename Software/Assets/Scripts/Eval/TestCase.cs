using System.Collections.Generic;
using UnityEngine;
using VirtualFlux.Sim;

namespace VirtualFlux.Eval
{
    [CreateAssetMenu(fileName = "TestCase", menuName = "VirtualFlux/Test Case")]
    public sealed class TestCase : ScriptableObject
    {
        public string DisplayName = "Basic SMD Joint";

        [Header("Targets")]
        public List<Pad> TargetPads = new List<Pad>();

        [Header("Angle window (degrees from board normal)")]
        public float MinAngleDeg = 40f;
        public float MaxAngleDeg = 55f;

        [Header("Dwell window (seconds per pad)")]
        public float MinDwellSec = 1.5f;
        public float MaxDwellSec = 3.0f;

        [Header("Peak temperature window (°C)")]
        public float MinPeakC = 250f;
        public float MaxPeakC = 360f;

        [Header("Solder volume window (relative units per pad)")]
        public float MinSolderVolume = 0.2f;
        public float MaxSolderVolume = 1.5f;

        [Header("Process rules")]
        public bool RequireFluxBeforeHeat = true;
        public bool RequireSolderFedAtIronTip = true;
        public bool ForbidBurnt = true;
        public bool ForbidLifted = true;
    }
}
