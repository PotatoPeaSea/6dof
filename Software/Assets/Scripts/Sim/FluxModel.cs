using UnityEngine;

namespace VirtualFlux.Sim
{
    public enum FluxState
    {
        None,
        Cold,
        Active,
        Burnt,
    }

    /// <summary>
    /// Per-cell flux state machine. Pure logic; one instance lives in each grid cell.
    ///
    /// Activation band: tempC in [TActivateLowC, TBurnC).
    /// Beyond TBurnC or after TActiveSecondsBudget seconds active, flux burns out.
    /// </summary>
    public sealed class FluxModel
    {
        public float TActivateLowC { get; }
        public float TBurnC { get; }
        public float TActiveSecondsBudget { get; }
        public float OxidationGrowthPerSecAt250C { get; }

        public FluxState State { get; private set; }
        public float Amount { get; private set; }
        public float ActiveSeconds { get; private set; }
        public float Oxidation { get; private set; }

        public FluxModel(
            float tActivateLowC = 150f,
            float tBurnC = 320f,
            float tActiveSecondsBudget = 6f,
            float oxidationGrowthPerSecAt250C = 0.05f)
        {
            TActivateLowC = tActivateLowC;
            TBurnC = tBurnC;
            TActiveSecondsBudget = tActiveSecondsBudget;
            OxidationGrowthPerSecAt250C = oxidationGrowthPerSecAt250C;
            State = FluxState.None;
        }

        public void Apply(float amount01)
        {
            if (State == FluxState.Burnt) return;
            Amount = Mathf.Clamp01(Amount + amount01);
            if (Amount > 0f && State == FluxState.None)
            {
                State = FluxState.Cold;
            }
        }

        public void Step(float dtSec, float tempC)
        {
            if (dtSec <= 0f) return;

            if (State == FluxState.Burnt || Amount <= 0f)
            {
                GrowOxidation(dtSec, tempC);
                if (Amount <= 0f && State != FluxState.Burnt) State = FluxState.None;
                return;
            }

            if (tempC >= TBurnC)
            {
                BurnOut();
                return;
            }

            if (tempC >= TActivateLowC)
            {
                State = FluxState.Active;
                ActiveSeconds += dtSec;
                Oxidation = Mathf.Max(0f, Oxidation - dtSec * 0.5f);
                if (ActiveSeconds >= TActiveSecondsBudget)
                {
                    BurnOut();
                }
            }
            else
            {
                State = FluxState.Cold;
                // Cold flux still inhibits oxidation slightly.
                Oxidation = Mathf.Max(0f, Oxidation - dtSec * 0.05f);
            }
        }

        /// <summary>
        /// Wetting weight in [0, 1] used by SolderFlow as the cell-affinity multiplier.
        /// Active flux gives full wetting; oxidation cuts it; absent/burnt flux falls back
        /// to bare-copper wetting (poor).
        /// </summary>
        public float WettingWeight()
        {
            float baseW = State switch
            {
                FluxState.Active => 1.0f,
                FluxState.Cold => 0.25f,
                FluxState.None => 0.10f,
                FluxState.Burnt => 0.05f,
                _ => 0.10f,
            };
            return Mathf.Clamp01(baseW * (1f - Oxidation));
        }

        private void BurnOut()
        {
            State = FluxState.Burnt;
            Amount = 0f;
        }

        private void GrowOxidation(float dtSec, float tempC)
        {
            if (tempC <= 60f) return;
            float scale = Mathf.Max(0f, (tempC - 60f) / 190f);
            Oxidation = Mathf.Clamp01(Oxidation + OxidationGrowthPerSecAt250C * scale * dtSec);
        }
    }
}
