using UnityEngine;

namespace VirtualFlux.Sim
{
    /// <summary>
    /// Grid-based deterministic solder + flux + temperature simulation for a single pad
    /// region. Indexed [x, y] with origin at (0, 0). No randomness; tests step it by hand.
    /// </summary>
    public sealed class SolderFlow
    {
        public int Width { get; }
        public int Height { get; }
        public float MeltingPointC { get; }

        private readonly float[,] _solderVolume;
        private readonly float[,] _tempC;
        private readonly FluxModel[,] _flux;
        private readonly float[,] _scratch;

        public SolderFlow(int width, int height, float meltingPointC = SolderStateRules.DefaultMeltingPointC)
        {
            if (width <= 0 || height <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(width), "grid dims must be positive");
            }
            Width = width;
            Height = height;
            MeltingPointC = meltingPointC;
            _solderVolume = new float[width, height];
            _tempC = new float[width, height];
            _flux = new FluxModel[width, height];
            _scratch = new float[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _flux[x, y] = new FluxModel();
                }
            }
        }

        public float GetSolder(int x, int y) => _solderVolume[x, y];
        public float GetTemp(int x, int y) => _tempC[x, y];
        public FluxModel GetFlux(int x, int y) => _flux[x, y];

        public void SetTemp(int x, int y, float tempC) => _tempC[x, y] = tempC;
        public void AddSolder(int x, int y, float volume) => _solderVolume[x, y] = Mathf.Max(0f, _solderVolume[x, y] + volume);
        public void ApplyFlux(int x, int y, float amount01) => _flux[x, y].Apply(amount01);

        public void Step(float dtSec)
        {
            if (dtSec <= 0f) return;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    _flux[x, y].Step(dtSec, _tempC[x, y]);
                }
            }

            // Diffuse solder from molten cells toward higher-wettability neighbors.
            System.Array.Clear(_scratch, 0, _scratch.Length);

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    float vol = _solderVolume[x, y];
                    if (vol <= 0f) continue;
                    if (_tempC[x, y] < MeltingPointC) continue;

                    float selfWeight = _flux[x, y].WettingWeight();
                    float totalWeight = selfWeight;
                    float wN = NeighborWeight(x, y + 1);
                    float wS = NeighborWeight(x, y - 1);
                    float wE = NeighborWeight(x + 1, y);
                    float wW = NeighborWeight(x - 1, y);
                    totalWeight += wN + wS + wE + wW;
                    if (totalWeight <= 0f)
                    {
                        _scratch[x, y] += vol;
                        continue;
                    }

                    float mobile = vol * Mathf.Clamp01(dtSec * 4f);
                    float retained = vol - mobile;
                    _scratch[x, y] += retained + mobile * (selfWeight / totalWeight);
                    if (y + 1 < Height) _scratch[x, y + 1] += mobile * (wN / totalWeight);
                    if (y - 1 >= 0)     _scratch[x, y - 1] += mobile * (wS / totalWeight);
                    if (x + 1 < Width)  _scratch[x + 1, y] += mobile * (wE / totalWeight);
                    if (x - 1 >= 0)     _scratch[x - 1, y] += mobile * (wW / totalWeight);
                }
            }

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    _solderVolume[x, y] = _scratch[x, y];
                }
            }
        }

        private float NeighborWeight(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return 0f;
            // Only molten neighbors accept solder; cold neighbors freeze the flow boundary.
            if (_tempC[x, y] < MeltingPointC) return 0f;
            return _flux[x, y].WettingWeight();
        }
    }
}
