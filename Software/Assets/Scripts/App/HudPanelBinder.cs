using UnityEngine;
using UnityEngine.UIElements;

namespace VirtualFlux.App
{
    /// <summary>
    /// Runtime safety net that assigns a PanelSettings (loaded from a Resources/ folder) to any
    /// UIDocument that has none. Editor-time scene serialization drops the panel reference set by
    /// the builder, so the HUDs would otherwise render blank. Runs before the HUDs (negative
    /// execution order) so their UI builds with a panel already bound.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class HudPanelBinder : MonoBehaviour
    {
        [SerializeField] private string panelResourceName = "WorkbenchPanelSettings";

        private void Awake()
        {
            var panel = Resources.Load<PanelSettings>(panelResourceName);
            if (panel == null)
            {
                Debug.LogWarning($"HudPanelBinder: no PanelSettings named '{panelResourceName}' found under a Resources/ folder.");
                return;
            }
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (doc.panelSettings == null) doc.panelSettings = panel;
            }
        }
    }
}
