using UnityEngine;
using UnityEngine.UIElements;

namespace VirtualFlux.App
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class LatencyHUD : MonoBehaviour
    {
        [SerializeField] private float updateIntervalSec = 0.25f;

        private UIDocument _doc;
        private Label _label;
        private float _accumDt;
        private int _frames;
        private float _timer;

        private void InitializeUI()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) return;
            var root = _doc.rootVisualElement;
            if (root == null) return;

            root.Clear();
            _label = new Label("…");
            _label.style.position = Position.Absolute;
            _label.style.top = 8;
            _label.style.right = 12;
            _label.style.color = new StyleColor(Color.white);
            root.Add(_label);
        }

        private void Update()
        {
            if (_label == null)
            {
                InitializeUI();
            }

            if (_label == null) return;

            _accumDt += Time.unscaledDeltaTime;
            _frames++;
            _timer += Time.unscaledDeltaTime;
            if (_timer < updateIntervalSec) return;

            float avgMs = (_accumDt / _frames) * 1000f;
            _label.text = $"frame {avgMs:0.0} ms  |  fixed {Time.fixedDeltaTime * 1000f:0.0} ms";
            _accumDt = 0f;
            _frames = 0;
            _timer = 0f;
        }
    }
}
