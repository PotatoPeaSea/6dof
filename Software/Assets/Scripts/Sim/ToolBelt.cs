using System;
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
        Tweezers,
    }

    public readonly struct ToolDepositEvent
    {
        public readonly Pad Pad;
        public readonly int CellX;
        public readonly int CellY;
        public readonly bool IronTipOnSameCell;

        public ToolDepositEvent(Pad pad, int cellX, int cellY, bool ironTipOnSameCell)
        {
            Pad = pad;
            CellX = cellX;
            CellY = cellY;
            IronTipOnSameCell = ironTipOnSameCell;
        }
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
        [SerializeField] private float solderFeedRatePerSec = 2.5f;
        [SerializeField] private float fluxPenAmountPerSec = 6f;
        [SerializeField] private float fluxPasteAmountPerSec = 12f;
        [SerializeField] private float ironSnapRadiusMeters = 0.003f;

        public ToolMode Mode { get; private set; } = ToolMode.Iron;

        public event Action<ToolDepositEvent> FluxApplied;
        public event Action<ToolDepositEvent> SolderFed;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Mode = ToolMode.Iron;
                if (kb.digit2Key.wasPressedThisFrame) Mode = ToolMode.SolderWire;
                if (kb.digit3Key.wasPressedThisFrame) Mode = ToolMode.FluxPen;
                if (kb.digit4Key.wasPressedThisFrame) Mode = ToolMode.FluxPaste;
                if (kb.digit5Key.wasPressedThisFrame) Mode = ToolMode.Tweezers;
            }

            if (Mode == ToolMode.Iron) return;

            var mouse = Mouse.current;
            if (mouse == null || worldCamera == null) return;

            if (Mode == ToolMode.Tweezers)
            {
                UpdateTweezers(mouse);
                return;
            }

            if (!mouse.leftButton.isPressed) return;

            var ray = worldCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 5f)) return;
            var pad = hit.collider.GetComponentInParent<Pad>();
            if (pad == null) return;

            var localHit = pad.transform.InverseTransformPoint(hit.point);
            var (cx, cy) = WorldToCell(pad, localHit);

            // Snap to iron tip if it is over the same pad and within snap radius.
            bool ironTipOnSameCell = false;
            if (iron != null && iron.Tip != null)
            {
                var tipLocal = pad.transform.InverseTransformPoint(iron.Tip.position);
                if (Vector2.Distance(new Vector2(tipLocal.x, tipLocal.z), new Vector2(localHit.x, localHit.z)) <= ironSnapRadiusMeters)
                {
                    (cx, cy) = WorldToCell(pad, tipLocal);
                    ironTipOnSameCell = true;
                }
            }

            switch (Mode)
            {
                case ToolMode.SolderWire:
                    // Apply solder in a 3x3 grid centered at the hit cell, adding to any cells at/above melting point
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = cx + dx;
                            int ny = cy + dy;
                            if (nx < 0 || nx >= pad.Flow.Width || ny < 0 || ny >= pad.Flow.Height) continue;
                            if (pad.Flow.GetTemp(nx, ny) >= pad.Flow.MeltingPointC)
                            {
                                float factor = (dx == 0 && dy == 0) ? 1.0f : 0.6f;
                                pad.Flow.AddSolder(nx, ny, solderFeedRatePerSec * factor * Time.deltaTime);
                            }
                        }
                    }
                    SolderFed?.Invoke(new ToolDepositEvent(pad, cx, cy, ironTipOnSameCell));
                    break;
                case ToolMode.FluxPen:
                    // 3x3 cross pattern (up, down, left, right, center)
                    pad.Flow.ApplyFlux(cx, cy, fluxPenAmountPerSec * Time.deltaTime);
                    if (cx + 1 < pad.Flow.Width) pad.Flow.ApplyFlux(cx + 1, cy, fluxPenAmountPerSec * 0.5f * Time.deltaTime);
                    if (cx - 1 >= 0) pad.Flow.ApplyFlux(cx - 1, cy, fluxPenAmountPerSec * 0.5f * Time.deltaTime);
                    if (cy + 1 < pad.Flow.Height) pad.Flow.ApplyFlux(cx, cy + 1, fluxPenAmountPerSec * 0.5f * Time.deltaTime);
                    if (cy - 1 >= 0) pad.Flow.ApplyFlux(cx, cy - 1, fluxPenAmountPerSec * 0.5f * Time.deltaTime);
                    FluxApplied?.Invoke(new ToolDepositEvent(pad, cx, cy, ironTipOnSameCell));
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
                    FluxApplied?.Invoke(new ToolDepositEvent(pad, cx, cy, ironTipOnSameCell));
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

        private Transform _draggedComponent;
        private float _dragPlaneY;
        private Vector3 _dragOffset;

        private void UpdateTweezers(Mouse mouse)
        {
            var ray = worldCamera.ScreenPointToRay(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (Physics.Raycast(ray, out var hit, 5f))
                {
                    var component = hit.collider.GetComponentInParent<App.SolderableComponent>();
                    if (component != null && !component.IsCurrentlyBonded)
                    {
                        _draggedComponent = component.transform;
                        // Lift Y slightly by 2 mm (0.002) to prevent it from scraping/penetrating the board while dragging
                        _dragPlaneY = _draggedComponent.position.y + 0.002f;
                        _dragOffset = _draggedComponent.position - hit.point;
                        _dragOffset.y = 0f; // Lock Y offset calculation to deterministic dragPlaneY height

                        // Set Rigidbody to kinematic during drag to prevent violent collision reactions
                        if (_draggedComponent.TryGetComponent<Rigidbody>(out var rb))
                        {
                            rb.isKinematic = true;
                        }
                    }
                }
            }

            if (mouse.leftButton.isPressed && _draggedComponent != null)
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, _dragPlaneY, 0f));
                if (plane.Raycast(ray, out float enter))
                {
                    var targetPos = ray.GetPoint(enter) + _dragOffset;
                    if (_draggedComponent.TryGetComponent<Rigidbody>(out var rb))
                    {
                        rb.position = targetPos;
                    }
                    else
                    {
                        _draggedComponent.position = targetPos;
                    }
                }
            }

            if (!mouse.leftButton.isPressed && _draggedComponent != null)
            {
                // Disable all colliders on the component so the down-raycast hits the board/pad, not itself
                var colliders = _draggedComponent.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) c.enabled = false;

                // Snap to board/pad surface on release to avoid floating
                var startPos = _draggedComponent.position;
                var downRay = new Ray(startPos + Vector3.up * 0.05f, Vector3.down);
                if (Physics.Raycast(downRay, out var hit, 5f))
                {
                    // Resistor pins extend 0.004m down from the root center, so root Y is hit.point.y + 0.004f
                    var targetPos = startPos;
                    targetPos.y = hit.point.y + 0.004f;
                    
                    if (_draggedComponent.TryGetComponent<Rigidbody>(out var rb))
                    {
                        rb.position = targetPos;
                    }
                    else
                    {
                        _draggedComponent.position = targetPos;
                    }
                }

                // Re-enable colliders
                foreach (var c in colliders) c.enabled = true;

                // Keep it kinematic to prevent sub-centimeter PhysX collision launch instability
                if (_draggedComponent.TryGetComponent<Rigidbody>(out var rb2))
                {
                    rb2.isKinematic = true;
                }
                _draggedComponent = null;
            }
        }
    }
}
