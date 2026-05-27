using System.IO;
using UnityEngine;

namespace VirtualFlux.Gerber
{
    /// <summary>
    /// V1 loader: reads a path from a Unity Inspector field or env var and imports.
    /// A native file picker (StandaloneFileBrowser) can be wired in later without
    /// touching downstream code.
    /// </summary>
    public sealed class GerberLoadUI : MonoBehaviour
    {
        [SerializeField] private string defaultPath = "";
        [SerializeField] private Material copperMaterial;

        private void Start()
        {
            var path = defaultPath;
            if (string.IsNullOrEmpty(path))
            {
                path = System.Environment.GetEnvironmentVariable("VIRTUAL_FLUX_GERBER");
            }
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            var file = GerberParser.ParseFile(path);
            GerberMeshBuilder.Build(file, transform, copperMaterial);
        }
    }
}
