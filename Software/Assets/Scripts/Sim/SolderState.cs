namespace VirtualFlux.Sim
{
    public enum SolderPhase
    {
        Solid,
        Liquid,
        Burnt,
        Lifted,
    }

    public static class SolderStateRules
    {
        public const float DefaultMeltingPointC = 183f;   // SnPb eutectic; SAC305 ≈ 217 °C
        public const float DefaultBurnSecondsAtMaxC = 3f;
        public const float DefaultLiftSecondsBeyondBurn = 5f;

        public static SolderPhase Transition(
            SolderPhase current,
            float tempC,
            float meltingPointC,
            float maxToleranceC,
            float secondsAboveMaxTolerance,
            float burnSeconds,
            float liftSeconds)
        {
            if (current == SolderPhase.Lifted) return SolderPhase.Lifted;

            if (secondsAboveMaxTolerance >= burnSeconds + liftSeconds)
            {
                return SolderPhase.Lifted;
            }

            if (tempC >= maxToleranceC && secondsAboveMaxTolerance >= burnSeconds)
            {
                return SolderPhase.Burnt;
            }

            if (current == SolderPhase.Burnt)
            {
                return SolderPhase.Burnt;
            }

            return tempC >= meltingPointC ? SolderPhase.Liquid : SolderPhase.Solid;
        }
    }
}
