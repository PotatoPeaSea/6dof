using UnityEngine;

namespace VirtualFlux.Sim
{
    /// <summary>
    /// Pure mapping from pad simulation state to display color and solder-blob fill. Holds no
    /// Unity runtime state, so it is deterministic and unit-testable. <see cref="App.PadVisualizer"/>
    /// is the per-frame consumer.
    /// </summary>
    public static class PadHeatPalette
    {
        public static readonly Color Cool = new Color(0.72f, 0.45f, 0.20f);   // copper
        public static readonly Color Warm = new Color(0.95f, 0.45f, 0.10f);   // heating
        public static readonly Color Hot = new Color(1.00f, 0.25f, 0.08f);    // near peak
        public static readonly Color Burnt = new Color(0.14f, 0.11f, 0.09f);  // charred
        public static readonly Color Lifted = new Color(0.45f, 0.45f, 0.48f); // lifted pad

        /// <summary>Surface color for a pad. Burnt/Lifted phases override the temperature ramp.</summary>
        public static Color HeatColor(float tempC, SolderPhase phase, float coolTempC, float hotTempC)
        {
            if (phase == SolderPhase.Burnt) return Burnt;
            if (phase == SolderPhase.Lifted) return Lifted;

            float t = Mathf.Clamp01(Mathf.InverseLerp(coolTempC, hotTempC, tempC));
            return t < 0.5f
                ? Color.Lerp(Cool, Warm, t * 2f)
                : Color.Lerp(Warm, Hot, (t - 0.5f) * 2f);
        }

        /// <summary>Normalized 0..1 blob fill for a given solder volume.</summary>
        public static float BlobFill01(float volume, float maxVolume)
            => maxVolume <= 0f ? 0f : Mathf.Clamp01(volume / maxVolume);
    }
}
