using UnityEngine;

namespace VirtualFlux.App
{
    public sealed class BootstrapScene : MonoBehaviour
    {
        [SerializeField] private float fixedTimestepSec = 0.005f;

        private void Awake()
        {
            Time.fixedDeltaTime = fixedTimestepSec;
        }
    }
}
