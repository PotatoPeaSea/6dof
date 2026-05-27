using UnityEngine;
using UnityEngine.InputSystem;

namespace VirtualFlux.Sim
{
    public enum ToolMode
    {
        Iron,
        SolderWire,
        FluxPen,
        FluxPaste,
    }

    /// <summary>
    /// Dispatches the player's active deposition tool. Iron is hands-off (the iron transform
    /// is driven by <see cref="Hardware.IronController"/>); the other tools deposit material
    /// into the cell currently under the cursor, with the "feed at the iron tip" snap rule.
    /// </summary>
    public sealed class ToolBelt : MonoBehaviour
    {
        [SerializeField] private Hardware.IronController iron;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float solderFeedRatePerSec = 0.4f;
        [SerializeField] private float fluxPenAmountPerSec = 1.5f;
        [SerializeField] private float fluxPasteAmountPerSec = 4f;
        [SerializeField] private float ironSnapRadiusMeters = 0.003f;

        public ToolMode Mode { get; private set; } = ToolMode.Iron;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Mode = ToolMode.Iron;
                if (kb.digit2Key.wasPressedThisFrame) Mode = ToolMode.SolderWire;
                if (kb.digit3Key.wasPressedThisFrame) Mode = ToolMode.FluxPen;
                if (kb.digit4Key.wasPressedThisFrame) Mode = ToolMode.FluxPaste;
            }

            if (Mode == ToolMode.Iron) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return;
            if (worldCamera == null) return;

            var ray = worldCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 5f)) return;
            var pad = hit.collider.GetComponentInParent<Pad>();
            if (pad == null) return;

            var localHit = pad.transform.InverseTransformPoint(hit.point);
            var (cx, cy) = WorldToCell(pad, localHit);

            // Snap to iron tip if it is over the same pad and within snap radius.
            if (iron != null && iron.Tip != null)
            {
                var tipLocal = pad.transform.InverseTransformPoint(iron.Tip.position);
                if (Vector2.Distance(new Vector2(tipLocal.x, tipLocal.z), new Vector2(localHit.x, localHit.z)) <= ironSnapRadiusMeters)
                {
                    (cx, cy) = WorldToCell(pad, tipLocal);
                }
            }

            switch (Mode)
            {
                case ToolMode.SolderWire:
                    if (pad.Flow.GetTemp(cx, cy) >= pad.Flow.MeltingPointC)
                    {
                        pad.Flow.AddSolder(cx, cy, solderFeedRatePerSec * Time.deltaTime);
                    }
                    break;
                case ToolMode.FluxPen:
                    pad.Flow.ApplyFlux(cx, cy, fluxPenAmountPerSec * Time.deltaTime);
                    break;
                case ToolMode.FluxPaste:
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = cx + dx;
                            int ny = cy + dy;
                            if (nx < 0 || nx >= pad.Flow.Width || ny < 0 || ny >= pad.Flow.Height) continue;
                            pad.Flow.ApplyFlux(nx, ny, fluxPasteAmountPerSec * Time.deltaTime);
                        }
                    }
                    break;
            }
        }

        private static (int x, int y) WorldToCell(Pad pad, Vector3 padLocal)
        {
            // Pad mesh assumed unit-square in local XZ from (-0.5, 0, -0.5) to (0.5, 0, 0.5).
            float u = Mathf.Clamp01(padLocal.x + 0.5f);
            float v = Mathf.Clamp01(padLocal.z + 0.5f);
            int x = Mathf.Clamp(Mathf.FloorToInt(u * pad.Flow.Width), 0, pad.Flow.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * pad.Flow.Height), 0, pad.Flow.Height - 1);
            return (x, y);
        }
    }
}
