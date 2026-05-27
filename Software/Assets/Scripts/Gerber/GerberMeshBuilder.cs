using System.Collections.Generic;
using UnityEngine;
using VirtualFlux.Sim;

namespace VirtualFlux.Gerber
{
    /// <summary>
    /// Turns parsed Gerber primitives into a copper mesh + spawns Pad GameObjects for
    /// each flashed aperture so the thermal model has targets. V1 quality bar: it
    /// renders and pads register tip contact. Polish (silkscreen, mask, paste) deferred.
    /// </summary>
    public static class GerberMeshBuilder
    {
        public static GameObject Build(GerberFile file, Transform parent, Material copperMaterial = null)
        {
            var root = new GameObject("GerberCopper");
            root.transform.SetParent(parent, worldPositionStays: false);

            // Copper mesh: simple triangulation of flashed circles + regions as quads/fans.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            foreach (var prim in file.Primitives)
            {
                if (prim.Kind == PrimitiveKind.Flash && file.Apertures.TryGetValue(prim.ApertureDCode, out var ap))
                {
                    AppendFlash(prim.Points[0], ap, vertices, triangles);
                    SpawnPad(prim.Points[0], ap, root.transform);
                }
                else if (prim.Kind == PrimitiveKind.Region)
                {
                    AppendFan(prim.Points, vertices, triangles);
                }
                else if (prim.Kind == PrimitiveKind.Draw && file.Apertures.TryGetValue(prim.ApertureDCode, out var apD))
                {
                    AppendTrace(prim.Points[0], prim.Points[1], apD.SizeX, vertices, triangles);
                }
            }

            var mesh = new Mesh { name = "GerberCopper" };
            if (vertices.Count >= 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = root.AddComponent<MeshRenderer>();
            if (copperMaterial != null) mr.sharedMaterial = copperMaterial;

            return root;
        }

        private static void AppendFlash(Vector2 center, Aperture ap, List<Vector3> v, List<int> t)
        {
            if (ap.Shape == ApertureShape.Circle)
            {
                AppendDisc(center, ap.SizeX * 0.5f, 16, v, t);
            }
            else
            {
                AppendRect(center, ap.SizeX, ap.SizeY, v, t);
            }
        }

        private static void AppendDisc(Vector2 c, float radius, int segments, List<Vector3> v, List<int> t)
        {
            int center = v.Count;
            v.Add(new Vector3(c.x, 0f, c.y));
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                v.Add(new Vector3(c.x + Mathf.Cos(a) * radius, 0f, c.y + Mathf.Sin(a) * radius));
            }
            for (int i = 0; i < segments; i++)
            {
                t.Add(center);
                t.Add(center + 1 + i);
                t.Add(center + 1 + ((i + 1) % segments));
            }
        }

        private static void AppendRect(Vector2 c, float w, float h, List<Vector3> v, List<int> t)
        {
            float hw = w * 0.5f, hh = h * 0.5f;
            int b = v.Count;
            v.Add(new Vector3(c.x - hw, 0f, c.y - hh));
            v.Add(new Vector3(c.x + hw, 0f, c.y - hh));
            v.Add(new Vector3(c.x + hw, 0f, c.y + hh));
            v.Add(new Vector3(c.x - hw, 0f, c.y + hh));
            t.Add(b); t.Add(b + 2); t.Add(b + 1);
            t.Add(b); t.Add(b + 3); t.Add(b + 2);
        }

        private static void AppendFan(List<Vector2> pts, List<Vector3> v, List<int> t)
        {
            if (pts.Count < 3) return;
            int b = v.Count;
            for (int i = 0; i < pts.Count; i++) v.Add(new Vector3(pts[i].x, 0f, pts[i].y));
            for (int i = 1; i < pts.Count - 1; i++)
            {
                t.Add(b);
                t.Add(b + i + 1);
                t.Add(b + i);
            }
        }

        private static void AppendTrace(Vector2 a, Vector2 b, float width, List<Vector3> v, List<int> t)
        {
            var dir = (b - a).normalized;
            var perp = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            int idx = v.Count;
            v.Add(new Vector3(a.x - perp.x, 0f, a.y - perp.y));
            v.Add(new Vector3(a.x + perp.x, 0f, a.y + perp.y));
            v.Add(new Vector3(b.x + perp.x, 0f, b.y + perp.y));
            v.Add(new Vector3(b.x - perp.x, 0f, b.y - perp.y));
            t.Add(idx); t.Add(idx + 2); t.Add(idx + 1);
            t.Add(idx); t.Add(idx + 3); t.Add(idx + 2);
        }

        private static void SpawnPad(Vector2 center, Aperture ap, Transform parent)
        {
            var go = new GameObject($"Pad_D{ap.DCode}");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = new Vector3(center.x, 0f, center.y);
            float side = Mathf.Max(ap.SizeX, ap.SizeY);
            go.transform.localScale = new Vector3(side, 1f, side);
            go.AddComponent<Pad>();
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 0.001f, 1f);
            col.isTrigger = false;
        }
    }
}
