namespace VirtualFlux.Sim
{
    /// <summary>
    /// Pure heuristic for whether molten solder bridges the gap between two adjacent pads.
    /// Holds no Unity state, so it is deterministic and unit-testable. Adjacency and edge-solder
    /// sampling are handled by <see cref="App.BridgeMonitor"/>; this only decides the outcome.
    /// </summary>
    public static class BridgeRules
    {
        /// <summary>Combined facing-edge solder volume at/above which a bridge forms.</summary>
        public const float DefaultFormVolume = 0.15f;

        /// <summary>
        /// A bridge forms only when both facing edges are molten (the gap is hot enough for
        /// solder to flow) and their combined edge solder reaches the threshold.
        /// </summary>
        public static bool Bridged(float edgeSolderA, float edgeSolderB, bool moltenA, bool moltenB, float formVolume)
        {
            if (!moltenA || !moltenB) return false;
            return edgeSolderA + edgeSolderB >= formVolume;
        }
    }
}
