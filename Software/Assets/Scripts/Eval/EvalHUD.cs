using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VirtualFlux.Sim;
using VirtualFlux.App;

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
            if (kb.tKey.wasPressedThisFrame) ResetRequested?.Invoke();
        }

        public void RenderGuidance(
            Pad activePad,
            SolderablePin[] allPins,
            SolderableComponent[] allComponents)
        {
            if (_doc == null) return;
            var root = _doc.rootVisualElement;
            root.Clear();

            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.top = 90;
            panel.style.left = 12;
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;
            panel.style.minWidth = 230;
            panel.style.backgroundColor = new StyleColor(new Color(0.08f, 0.09f, 0.12f, 0.85f));
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            root.Add(panel);

            // --- Header ---
            var title = new Label("SOLDERING PROCESS");
            title.style.color = new StyleColor(new Color(0.2f, 0.8f, 1f)); // cyan
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            panel.Add(title);

            // --- Board Progress ---
            var progressHeader = new Label("Board Progress:");
            progressHeader.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            progressHeader.style.fontSize = 10;
            progressHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            progressHeader.style.marginBottom = 2;
            panel.Add(progressHeader);

            foreach (var comp in allComponents)
            {
                if (comp == null) continue;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginBottom = 2;

                var nameLbl = new Label(comp.name);
                nameLbl.style.color = new StyleColor(Color.white);
                nameLbl.style.fontSize = 10;

                var statusLbl = new Label(comp.IsCurrentlyBonded ? "SECURED" : "UNSECURED");
                statusLbl.style.color = comp.IsCurrentlyBonded 
                    ? new StyleColor(new Color(0.2f, 0.9f, 0.2f)) // green
                    : new StyleColor(new Color(1f, 0.6f, 0.2f)); // orange
                statusLbl.style.fontSize = 10;
                statusLbl.style.unityFontStyleAndWeight = FontStyle.Bold;

                row.Add(nameLbl);
                row.Add(statusLbl);
                panel.Add(row);
            }

            // Divider
            var div = new VisualElement();
            div.style.height = 1;
            div.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.4f));
            div.style.marginTop = 4;
            div.style.marginBottom = 4;
            panel.Add(div);

            if (activePad == null)
            {
                var noPadLbl = new Label("Hover iron or cursor over pad to guide.");
                noPadLbl.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                noPadLbl.style.fontSize = 10;
                noPadLbl.style.whiteSpace = WhiteSpace.Normal;
                panel.Add(noPadLbl);
                return;
            }

            // --- Active Pad Header ---
            var activePadLbl = new Label($"Active: {activePad.name} ({activePad.TempC:0}°C)");
            activePadLbl.style.color = new StyleColor(Color.white);
            activePadLbl.style.fontSize = 11;
            activePadLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            activePadLbl.style.marginBottom = 4;
            panel.Add(activePadLbl);

            // Calculate active pad states
            bool isJointBonded = false;
            foreach (var pin in allPins)
            {
                if (pin != null && pin.CurrentOverlappingPad == activePad && pin.IsBonded)
                {
                    isJointBonded = true;
                    break;
                }
            }

            bool step1 = false;
            bool step2 = false;
            bool step3 = false;
            bool step4 = false;
            bool step5 = false;

            if (isJointBonded)
            {
                step1 = step2 = step3 = step4 = step5 = true;
            }
            else
            {
                // Step 1: Place Pin
                SolderablePin overlappingPin = null;
                foreach (var pin in allPins)
                {
                    if (pin != null && pin.CurrentOverlappingPad == activePad)
                    {
                        overlappingPin = pin;
                        break;
                    }
                }
                step1 = overlappingPin != null;

                // Step 2: Flux
                float totalFlux = 0f;
                if (activePad.Flow != null)
                {
                    for (int x = 0; x < activePad.Flow.Width; x++)
                    {
                        for (int y = 0; y < activePad.Flow.Height; y++)
                        {
                            var f = activePad.Flow.GetFlux(x, y);
                            if (f != null) totalFlux += f.Amount;
                        }
                    }
                }
                step2 = totalFlux > 0.5f;

                // Step 3: Heat Joint
                step3 = activePad.TempC >= activePad.Flow.MeltingPointC;

                // Step 4: Solder volume
                float totalSolder = 0f;
                if (activePad.Flow != null)
                {
                    for (int x = 0; x < activePad.Flow.Width; x++)
                    {
                        for (int y = 0; y < activePad.Flow.Height; y++)
                        {
                            totalSolder += activePad.Flow.GetSolder(x, y);
                        }
                    }
                }
                step4 = totalSolder > 0.05f;

                // Step 5: Cool & Solidify
                step5 = step4 && (activePad.TempC < activePad.Flow.MeltingPointC);
            }

            // Find first incomplete step
            int activeStep = 6;
            if (!step1) activeStep = 1;
            else if (!step2) activeStep = 2;
            else if (!step3) activeStep = 3;
            else if (!step4) activeStep = 4;
            else if (!step5) activeStep = 5;

            if (isJointBonded)
            {
                var successLbl = new Label("✓ Joint Successfully Bonded!");
                successLbl.style.color = new StyleColor(new Color(0.2f, 0.9f, 0.2f)); // green
                successLbl.style.fontSize = 11;
                successLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                successLbl.style.marginTop = 4;
                panel.Add(successLbl);
            }
            else
            {
                // Draw only incomplete steps so they "scroll up" the panel as you complete them
                if (!step1) AddStepRow(panel, "1. Place Resistor Pin", "Use Tweezers (5) to align pin on pad.", step1, activeStep == 1);
                if (!step2) AddStepRow(panel, "2. Apply Flux", "Use Flux Pen/Paste (3/4) on pad.", step2, activeStep == 2);
                if (!step3) AddStepRow(panel, $"3. Heat Joint ({activePad.TempC:0}°C / 183°C)", "Hold [Space] with Iron (1) on pad.", step3, activeStep == 3);
                if (!step4) AddStepRow(panel, "4. Feed Solder", "Click Solder Wire (2) on heated pad.", step4, activeStep == 4);
                if (!step5) AddStepRow(panel, "5. Cool & Solidify", "Let solder cool below 183°C.", step5, activeStep == 5);
            }
        }

        private static void AddStepRow(VisualElement parent, string title, string description, bool passed, bool active)
        {
            var row = new VisualElement();
            row.style.marginBottom = 4;
            parent.Add(row);

            string prefix = passed ? "✓ " : (active ? "▶ " : "• ");
            var titleLbl = new Label(prefix + title);
            titleLbl.style.fontSize = 11;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;

            if (passed)
            {
                titleLbl.style.color = new StyleColor(new Color(0.2f, 0.9f, 0.2f)); // green
            }
            else if (active)
            {
                titleLbl.style.color = new StyleColor(new Color(1f, 0.8f, 0.2f)); // gold/active
            }
            else
            {
                titleLbl.style.color = new StyleColor(new Color(0.45f, 0.45f, 0.45f)); // dark grey
            }
            row.Add(titleLbl);

            var descLbl = new Label("    " + description);
            descLbl.style.fontSize = 9;
            descLbl.style.color = passed 
                ? new StyleColor(new Color(0.4f, 0.6f, 0.4f)) 
                : (active ? new StyleColor(new Color(0.85f, 0.85f, 0.85f)) : new StyleColor(new Color(0.35f, 0.35f, 0.35f)));
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            row.Add(descLbl);
        }

        public void Clear()
        {
            if (_doc == null) return;
            _doc.rootVisualElement.Clear();
        }
    }
}
