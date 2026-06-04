using System;
using System.Collections.Generic;
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

            // Collapse to one line per pad: the rule list is grouped, then each pad shows PASS or
            // FAIL with the short names of its failing rules — so the whole board fits on screen.
            var order = new List<string>();
            var byPad = new Dictionary<string, List<RuleResult>>();
            foreach (var rule in result.Rules)
            {
                var key = string.IsNullOrEmpty(rule.PadName) ? "—" : rule.PadName;
                if (!byPad.TryGetValue(key, out var list))
                {
                    list = new List<RuleResult>();
                    byPad[key] = list;
                    order.Add(key);
                }
                list.Add(rule);
            }

            int passedPads = 0;
            foreach (var key in order)
            {
                if (byPad[key].TrueForAll(r => r.Passed)) passedPads++;
            }

            var summary = new Label((result.Passed ? "PASS" : "FAIL") + $"   {passedPads}/{order.Count} pads");
            summary.style.color = result.Passed ? new StyleColor(Color.green) : new StyleColor(Color.red);
            summary.style.fontSize = 24;
            panel.Add(summary);

            foreach (var key in order)
            {
                var rules = byPad[key];
                var failed = new List<string>();
                foreach (var r in rules)
                {
                    if (!r.Passed) failed.Add(r.ShortName);
                }
                bool padPassed = failed.Count == 0;

                var row = new Label(padPassed ? $"[OK]  {key}" : $"[X]   {key}  —  {string.Join(", ", failed)}");
                row.style.color = padPassed ? new StyleColor(new Color(0.8f, 0.9f, 0.8f)) : new StyleColor(new Color(1f, 0.6f, 0.6f));
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
