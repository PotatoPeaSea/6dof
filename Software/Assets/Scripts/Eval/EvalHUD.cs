using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace VirtualFlux.Eval
{
    /// <summary>
    /// Minimal V1 HUD that renders the latest <see cref="EvalResult"/> into a UI Toolkit
    /// document. The controlling MonoBehaviour subscribes to <see cref="ScoreRequested"/>
    /// and <see cref="ResetRequested"/> (R and T by default) and calls <see cref="Render"/>
    /// after each scoring pass.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EvalHUD : MonoBehaviour
    {
        public event Action ScoreRequested;
        public event Action ResetRequested;

        private UIDocument _doc;

        private void Awake() => _doc = GetComponent<UIDocument>();

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame) ScoreRequested?.Invoke();
            if (kb.tKey.wasPressedThisFrame) ResetRequested?.Invoke();
        }

        public void Render(EvalResult result)
        {
            if (_doc == null || result == null) return;
            var root = _doc.rootVisualElement;
            root.Clear();

            // Own panel, positioned clear of the StatusHUD (top-left) so the result is visible.
            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.top = 120;
            panel.style.left = 12;
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.paddingTop = 6;
            panel.style.paddingBottom = 6;
            panel.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));
            root.Add(panel);

            var summary = new Label(result.Passed ? "PASS" : "FAIL");
            summary.style.color = result.Passed ? new StyleColor(Color.green) : new StyleColor(Color.red);
            summary.style.fontSize = 24;
            panel.Add(summary);

            foreach (var rule in result.Rules)
            {
                var row = new Label((rule.Passed ? "[OK]  " : "[X]   ") + rule.Name +
                                    (string.IsNullOrEmpty(rule.Detail) ? "" : "  —  " + rule.Detail));
                row.style.color = rule.Passed ? new StyleColor(new Color(0.8f, 0.9f, 0.8f)) : new StyleColor(new Color(1f, 0.6f, 0.6f));
                panel.Add(row);
            }
        }

        public void Clear()
        {
            if (_doc == null) return;
            _doc.rootVisualElement.Clear();
        }
    }
}
