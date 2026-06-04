using UnityEngine;

namespace VirtualFlux.App
{
    /// <summary>
    /// Attached to a physical component root containing a Rigidbody. When all child
    /// SolderablePin components are bonded, it sets the Rigidbody to kinematic to freeze it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SolderableComponent : MonoBehaviour
    {
        private SolderablePin[] _pins;
        private Rigidbody _rb;
        private bool _isCurrentlyBonded;

        public bool IsCurrentlyBonded => _isCurrentlyBonded;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _pins = GetComponentsInChildren<SolderablePin>();
        }

        private void Update()
        {
            if (_pins == null || _pins.Length == 0) return;

            // The component is bonded if ANY of its pins are bonded
            bool anyPinBonded = false;
            foreach (var pin in _pins)
            {
                if (pin.IsBonded)
                {
                    anyPinBonded = true;
                    break;
                }
            }

            _isCurrentlyBonded = anyPinBonded;
        }
    }
}
