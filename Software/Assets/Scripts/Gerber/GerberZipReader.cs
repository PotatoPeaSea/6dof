using System.IO;
using System.IO.Compression;

namespace VirtualFlux.Gerber
{
    /// <summary>
    /// Pure-IO helper that reads a copper-layer Gerber from either a bare .gbr file
    /// or a zipped bundle. Kept separate from <see cref="GerberLoadUI"/> so it can
    /// be exercised outside the Unity runtime.
    /// </summary>
    public static class GerberZipReader
    {
        public static string ReadCopperLayer(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            if (path.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(path);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var entry = SelectCopperEntry(archive);
                if (entry == null) return null;
                using var reader = new StreamReader(entry.Open());
                return reader.ReadToEnd();
            }

            return File.ReadAllText(path);
        }

        public static ZipArchiveEntry SelectCopperEntry(ZipArchive archive)
        {
            ZipArchiveEntry fCu = null, bCu = null, anyGbr = null;
            foreach (var e in archive.Entries)
            {
                if (!e.Name.EndsWith(".gbr", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (e.Name.IndexOf("F_Cu", System.StringComparison.OrdinalIgnoreCase) >= 0) { fCu = e; break; }
                if (e.Name.IndexOf("B_Cu", System.StringComparison.OrdinalIgnoreCase) >= 0) bCu ??= e;
                anyGbr ??= e;
            }
            return fCu ?? bCu ?? anyGbr;
        }
    }
}
