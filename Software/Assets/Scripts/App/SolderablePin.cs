using UnityEngine;
using VirtualFlux.Sim;

namespace VirtualFlux.App
{
    /// <summary>
    /// Attached to a pin tip on a component. Raycasts downwards to detect contact with
    /// a Pad, and check if the pad's solder at the contact cell is liquid or solid.
    /// </summary>
    public sealed class SolderablePin : MonoBehaviour
    {
        [SerializeField] private float contactOffset = 0.003f; // Y offset to start raycast above pin
        [SerializeField] private float contactDistance = 0.005f; // Ray length

        public bool IsBonded { get; private set; }
        public Pad BondedPad { get; private set; }
        public int BondedCellX { get; private set; }
        public int BondedCellY { get; private set; }

        public Pad CurrentOverlappingPad { get; private set; }

        private SolderableComponent _myComponent;
        private bool _isWetted;

        private void Start()
        {
            _myComponent = GetComponentInParent<SolderableComponent>();
        }

        private void Update()
        {
            CurrentOverlappingPad = null;
            var myPos = transform.position;
            // Raycast straight down in world coordinates to find pads on the board
            var ray = new Ray(myPos + Vector3.up * contactOffset, Vector3.down);
            var hits = Physics.RaycastAll(ray, contactDistance);
            
            // Sort by distance to find the closest valid collider
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // Skip if the hit collider belongs to our own SolderableComponent
                var otherComp = hit.collider.GetComponentInParent<SolderableComponent>();
                if (otherComp != null && otherComp == _myComponent)
                {
                    continue;
                }

                var pad = hit.collider.GetComponentInParent<Pad>();
                if (pad != null)
                {
                    var localHit = pad.transform.InverseTransformPoint(hit.point);
                    // Check if local coordinates fall within the pad quad bounds (-0.5 to 0.5 in XZ)
                    if (Mathf.Abs(localHit.x) <= 0.5f && Mathf.Abs(localHit.z) <= 0.5f && Mathf.Abs(localHit.y) <= 0.1f)
                    {
                        CurrentOverlappingPad = pad;
                        var (cx, cy) = WorldToCell(pad, localHit);
                        var flow = pad.Flow;
                        if (flow != null)
                        {
                            // Sum solder in a 5x5 neighborhood around the pin's contact cell
                            float solder = 0f;
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                for (int dy = -2; dy <= 2; dy++)
                                {
                                    int nx = cx + dx;
                                    int ny = cy + dy;
                                    if (nx >= 0 && nx < flow.Width && ny >= 0 && ny < flow.Height)
                                    {
                                        solder += flow.GetSolder(nx, ny);
                                    }
                                }
                            }

                            float temp = flow.GetTemp(cx, cy);

                            if (solder > 0.05f)
                            {
                                if (temp >= flow.MeltingPointC)
                                {
                                    // Solder is molten: pin is free to move/be pulled out, and is wetted!
                                    IsBonded = false;
                                    BondedPad = null;
                                    _isWetted = true;
                                }
                                else if (_isWetted && !IsBonded)
                                {
                                    // Solder is solid, and we were previously wetted by molten solder: we become bonded (frozen in place)
                                    IsBonded = true;
                                    BondedPad = pad;
                                    BondedCellX = cx;
                                    BondedCellY = cy;
                                }
                            }
                            else
                            {
                                // No solder at the cell neighborhood: cannot be bonded, and not wetted
                                IsBonded = false;
                                BondedPad = null;
                                _isWetted = false;
                            }
                            return;
                        }
                    }
                }
            }

            // Not touching a pad with solder
            IsBonded = false;
            BondedPad = null;
            _isWetted = false;
        }

        private static (int x, int y) WorldToCell(Pad pad, Vector3 padLocal)
        {
            float u = Mathf.Clamp01(padLocal.x + 0.5f);
            float v = Mathf.Clamp01(padLocal.z + 0.5f);
            int x = Mathf.Clamp(Mathf.FloorToInt(u * pad.Flow.Width), 0, pad.Flow.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * pad.Flow.Height), 0, pad.Flow.Height - 1);
            return (x, y);
        }
    }
}
