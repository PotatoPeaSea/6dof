using System.IO;
using UnityEngine;

namespace VirtualFlux.Gerber
{
    /// <summary>
    /// V1 loader: imports a single .gbr file or a zipped Gerber bundle. Path comes from
    /// an Inspector field, the VIRTUAL_FLUX_GERBER env var, or a default repo-relative
    /// fallback. A native file picker can replace this layer without changing the parser.
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

            var text = GerberZipReader.ReadCopperLayer(path);
            if (text == null) return;
            var file = GerberParser.Parse(text);
            GerberMeshBuilder.Build(file, transform, copperMaterial);
        }

        /// <summary>
        /// Convenience pass-through retained for back-compat with existing tests.
        /// New code should call <see cref="GerberZipReader.ReadCopperLayer"/>.
        /// </summary>
        public static string ReadCopperLayer(string path) => GerberZipReader.ReadCopperLayer(path);
    }
}
