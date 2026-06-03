using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace VirtualFlux.Input
{
    public sealed class KeyboardIronInput : MonoBehaviour, IIronInputSource
    {
        [SerializeField] private float translateSpeed = 0.05f;
        [SerializeField] private float rotateSpeedDeg = 90f;
        [SerializeField] private Vector3 initialPosition = new Vector3(0f, 0.05f, 0f);
        [SerializeField] private Vector3 initialEulerDeg = new Vector3(45f, 0f, 0f);
        [SerializeField] private float[] tempPresetsC = { 25f, 280f, 330f, 360f, 400f };

        private Vector3 _position;
        private Quaternion _rotation;
        private float _setpointC;
        private bool _energized;
        private uint _seq;

        public bool IsConnected => true;
        public IronSample Latest { get; private set; }

        private void Awake()
        {
            ResetPose();
        }

        public void Tick(float deltaTime)
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            var translate = new Vector3(
                Axis(kb.dKey, kb.aKey),
                Axis(kb.eKey, kb.qKey),
                Axis(kb.wKey, kb.sKey));
            _position += translate * (translateSpeed * deltaTime);

            var euler = new Vector3(
                Axis(kb.iKey, kb.kKey),
                Axis(kb.lKey, kb.jKey),
                Axis(kb.oKey, kb.uKey)) * (rotateSpeedDeg * deltaTime);
            _rotation = Quaternion.Euler(euler) * _rotation;

            for (int i = 0; i < tempPresetsC.Length && i < 5; i++)
            {
                if (DigitJustPressed(kb, i + 1))
                {
                    _setpointC = tempPresetsC[i];
                }
            }

            _energized = kb.spaceKey.isPressed;

            if (kb.rKey.wasPressedThisFrame)
            {
                ResetPose();
            }

            unchecked { _seq++; }
            Latest = new IronSample(_position, _rotation, _setpointC, _energized, _seq);
        }

        private void ResetPose()
        {
            _position = initialPosition;
            _rotation = Quaternion.Euler(initialEulerDeg);
            _setpointC = tempPresetsC.Length > 0 ? tempPresetsC[0] : 25f;
            _energized = false;
        }

        private static float Axis(ButtonControl positive, ButtonControl negative)
        {
            float v = 0f;
            if (positive != null && positive.isPressed) v += 1f;
            if (negative != null && negative.isPressed) v -= 1f;
            return v;
        }

        private static bool DigitJustPressed(Keyboard kb, int digit)
        {
            return digit switch
            {
                1 => kb.digit1Key.wasPressedThisFrame,
                2 => kb.digit2Key.wasPressedThisFrame,
                3 => kb.digit3Key.wasPressedThisFrame,
                4 => kb.digit4Key.wasPressedThisFrame,
                5 => kb.digit5Key.wasPressedThisFrame,
                _ => false,
            };
        }
    }
}
