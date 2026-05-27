using UnityEngine;
using UnityEngine.UIElements;

namespace VirtualFlux.Eval
{
    /// <summary>
    /// Minimal V1 HUD that renders the latest <see cref="EvalResult"/> into a UI Toolkit
    /// document. Call <see cref="Render"/> from the controlling MonoBehaviour after Score().
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EvalHUD : MonoBehaviour
    {
        private UIDocument _doc;

        private void Awake() => _doc = GetComponent<UIDocument>();

        public void Render(EvalResult result)
        {
            if (_doc == null || result == null) return;
            var root = _doc.rootVisualElement;
            root.Clear();

            var summary = new Label(result.Passed ? "PASS" : "FAIL");
            summary.style.color = result.Passed ? new StyleColor(Color.green) : new StyleColor(Color.red);
            summary.style.fontSize = 24;
            root.Add(summary);

            foreach (var rule in result.Rules)
            {
                var row = new Label((rule.Passed ? "[OK]  " : "[X]   ") + rule.Name +
                                    (string.IsNullOrEmpty(rule.Detail) ? "" : "  —  " + rule.Detail));
                row.style.color = rule.Passed ? new StyleColor(new Color(0.8f, 0.9f, 0.8f)) : new StyleColor(new Color(1f, 0.6f, 0.6f));
                root.Add(row);
            }
        }
    }
}
