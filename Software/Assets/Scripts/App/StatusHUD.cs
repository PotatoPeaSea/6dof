using UnityEngine;
using UnityEngine.UIElements;
using VirtualFlux.Hardware;
using VirtualFlux.Sim;

namespace VirtualFlux.App
{
    /// <summary>
    /// Live status: active tool, iron tip °C, energized indicator, key hints.
    /// Independent of the EvalHUD so the user can read state continuously.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class StatusHUD : MonoBehaviour
    {
        [SerializeField] private ToolBelt toolBelt;
        [SerializeField] private IronController iron;

        private UIDocument _doc;
        private Label _tool;
        private Label _temp;
        private Label _energized;
        private Label _hints;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private bool _uiInitialized;

        private void InitializeUI()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) return;
            var root = _doc.rootVisualElement;
            if (root == null) return;

            root.Clear();

            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.top = 8;
            panel.style.left = 12;
            panel.style.minWidth = 220;
            root.Add(panel);

            _tool = AddRow(panel, "tool: —");
            _temp = AddRow(panel, "tip:  —");
            _energized = AddRow(panel, "iron: off");
            _hints = AddRow(panel, "[1-5] tools (5=tweezers)  scroll: temp  [Space] heat  [T] reset  [R] reset pose");
            _hints.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            _hints.style.fontSize = 11;

            _uiInitialized = true;
        }

        private static Label AddRow(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.style.color = new StyleColor(Color.white);
            l.style.fontSize = 14;
            parent.Add(l);
            return l;
        }

        private void Update()
        {
            if (!_uiInitialized || _tool == null || _temp == null || _energized == null)
            {
                InitializeUI();
            }

            if (!_uiInitialized || _tool == null || _temp == null || _energized == null) return;

            if (toolBelt != null) _tool.text = $"tool: {toolBelt.Mode}";
            if (iron != null)
            {
                _temp.text = $"tip:  {iron.TipTempC:0}°C";
                _energized.text = iron.Energized ? "iron: ON" : "iron: off";
                _energized.style.color = iron.Energized
                    ? new StyleColor(new Color(1f, 0.7f, 0.2f))
                    : new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            }
        }
    }
}
