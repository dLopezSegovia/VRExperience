using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Small procedural mesh toolkit shared by the scene builder (props) and runtime systems
    /// (light-string cables, castle towers, fireworks debris). Smooth normals, sensible UVs.
    /// </summary>
    public static class MeshKit
    {
        /// <summary>Surface of revolution around +Y. profile = (radius, height) from bottom to top.</summary>
        public static Mesh Lathe(string name, IList<Vector2> profile, int segments = 24, bool capBottom = true, bool capTop = true,
                                 System.Func<int, float, float> radiusScale = null)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            int rows = profile.Count;
            float len = 0f;
            var acc = new float[rows];
            for (int i = 1; i < rows; i++) { len += (profile[i] - profile[i - 1]).magnitude; acc[i] = len; }
            for (int r = 0; r < rows; r++)
            {
                // profile tangent -> outward normal in the (r, y) plane
                Vector2 a = profile[Mathf.Max(0, r - 1)], b = profile[Mathf.Min(rows - 1, r + 1)];
                Vector2 tng = (b - a).normalized;
                Vector2 pn = new Vector2(tng.y, -tng.x);
                for (int s = 0; s <= segments; s++)
                {
                    float ang = s / (float)segments * Mathf.PI * 2f;
                    float rs = radiusScale != null ? radiusScale(s, ang) : 1f;
                    float c = Mathf.Cos(ang), si = Mathf.Sin(ang);
                    v.Add(new Vector3(c * profile[r].x * rs, profile[r].y, si * profile[r].x * rs));
                    n.Add(new Vector3(c * pn.x, pn.y, si * pn.x).normalized);
                    uv.Add(new Vector2(s / (float)segments, len > 0 ? acc[r] / len : 0));
                }
            }
            int w = segments + 1;
            for (int r = 0; r < rows - 1; r++)
            for (int s = 0; s < segments; s++)
            {
                int i = r * w + s;
                tri.AddRange(new[] { i, i + w, i + 1, i + 1, i + w, i + w + 1 });
            }
            if (capBottom && profile[0].x > 1e-4f) Cap(v, n, uv, tri, profile[0], segments, false);
            if (capTop && profile[rows - 1].x > 1e-4f) Cap(v, n, uv, tri, profile[rows - 1], segments, true);
            return Build(name, v, n, uv, tri);
        }

        static void Cap(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri, Vector2 p, int seg, bool top)
        {
            int c = v.Count;
            Vector3 nn = top ? Vector3.up : Vector3.down;
            v.Add(new Vector3(0, p.y, 0)); n.Add(nn); uv.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s <= seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x));
                n.Add(nn);
                uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int s = 0; s < seg; s++)
            {
                if (top) tri.AddRange(new[] { c, c + 2 + s, c + 1 + s });
                else tri.AddRange(new[] { c, c + 1 + s, c + 2 + s });
            }
        }

        /// <summary>Box with rounded edges (radius r), subdivided so normals are smooth.</summary>
        public static Mesh RoundedBox(string name, Vector3 size, float r, int div = 6)
        {
            Vector3 half = size * 0.5f;
            r = Mathf.Min(r, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.99f);
            Vector3 inner = half - Vector3.one * r;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            Vector3[] faceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 f in faceN)
            {
                Vector3 u = Mathf.Abs(f.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(f, u);
                u = Vector3.Cross(w, f);
                int start = v.Count;
                for (int y = 0; y <= div; y++)
                for (int x = 0; x <= div; x++)
                {
                    Vector3 cube = f + u * (x / (float)div * 2 - 1) + w * (y / (float)div * 2 - 1);
                    Vector3 p = Vector3.Scale(cube, half);
                    Vector3 ip = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 dir = p - ip;
                    Vector3 nn = dir.sqrMagnitude > 1e-10f ? dir.normalized : f;
                    v.Add(ip + nn * r);
                    n.Add(nn);
                    uv.Add(new Vector2(x / (float)div, y / (float)div));
                }
                for (int y = 0; y < div; y++)
                for (int x = 0; x < div; x++)
                {
                    int i = start + y * (div + 1) + x;
                    tri.AddRange(new[] { i, i + div + 1, i + 1, i + 1, i + div + 1, i + div + 2 });
                }
            }
            return Build(name, v, n, uv, tri);
        }

        /// <summary>Tube along a polyline (e.g. a chair frame, a cable).</summary>
        public static Mesh Tube(string name, IList<Vector3> path, float radius, int sides = 8, bool caps = true)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            AppendTube(v, n, uv, tri, path, radius, sides, caps);
            return Build(name, v, n, uv, tri);
        }

        public static void AppendTube(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri, IList<Vector3> path, float radius, int sides, bool caps)
        {
            int rows = path.Count;
            if (rows < 2) return;
            Vector3 prevSide = Vector3.zero;
            int start = v.Count;
            float len = 0f;
            for (int r = 0; r < rows; r++)
            {
                Vector3 t = (path[Mathf.Min(rows - 1, r + 1)] - path[Mathf.Max(0, r - 1)]).normalized;
                Vector3 side = r == 0 ? Vector3.Cross(t, Mathf.Abs(t.y) < 0.95f ? Vector3.up : Vector3.right).normalized
                                      : Vector3.Cross(Vector3.Cross(t, prevSide), t).normalized;
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(t, Vector3.right).normalized;
                prevSide = side;
                Vector3 up = Vector3.Cross(side, t);
                if (r > 0) len += (path[r] - path[r - 1]).magnitude;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 d = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    v.Add(path[r] + d * radius);
                    n.Add(d);
                    uv.Add(new Vector2(s / (float)sides, len));
                }
            }
            int w = sides + 1;
            for (int r = 0; r < rows - 1; r++)
            for (int s = 0; s < sides; s++)
            {
                int i = start + r * w + s;
                tri.AddRange(new[] { i, i + 1, i + w, i + 1, i + w + 1, i + w });
            }
            if (!caps) return;
            for (int e = 0; e < 2; e++)
            {
                int r = e == 0 ? 0 : rows - 1;
                Vector3 t = (path[e == 0 ? 1 : rows - 1] - path[e == 0 ? 0 : rows - 2]).normalized * (e == 0 ? -1 : 1);
                int c = v.Count;
                v.Add(path[r]); n.Add(t); uv.Add(Vector2.zero);
                for (int s = 0; s < sides; s++)
                {
                    int i = start + r * w + s;
                    if (e == 0) tri.AddRange(new[] { c, i + 1, i });
                    else tri.AddRange(new[] { c, i, i + 1 });
                }
            }
        }

        /// <summary>Sagging cable between two points (parabolic approximation of a catenary).</summary>
        public static Vector3[] Sag(Vector3 a, Vector3 b, float sag, int points)
        {
            var p = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                p[i] = Vector3.Lerp(a, b, t) + Vector3.down * (4f * sag * t * (1f - t));
            }
            return p;
        }

        public static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            FixWinding(m);
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Flip triangles whose winding disagrees with the stored normals (keeps generators simple).</summary>
        public static void FixWinding(Mesh m)
        {
            var v = m.vertices;
            var n = m.normals;
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(fn, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            m.triangles = t;
            m.RecalculateBounds();
        }

        /// <summary>Merge several meshes (each with its own transform) into one mesh with one submesh per material index.</summary>
        public static Mesh Combine(string name, IList<(Mesh mesh, Matrix4x4 m, int sub)> parts, int subCount)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>[subCount];
            for (int i = 0; i < subCount; i++) tris[i] = new List<int>();
            foreach (var p in parts)
            {
                int o = v.Count;
                var pv = p.mesh.vertices;
                var pn = p.mesh.normals;
                var pu = p.mesh.uv;
                for (int i = 0; i < pv.Length; i++)
                {
                    v.Add(p.m.MultiplyPoint3x4(pv[i]));
                    n.Add(p.m.MultiplyVector(pn.Length > i ? pn[i] : Vector3.up).normalized);
                    uv.Add(pu.Length > i ? pu[i] : Vector2.zero);
                }
                foreach (int t in p.mesh.triangles) tris[p.sub].Add(t + o);
            }
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.subMeshCount = subCount;
            for (int i = 0; i < subCount; i++) m.SetTriangles(tris[i], i);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>World ground height (terrains only).</summary>
        public static float GroundY(Vector3 p)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 o = t.transform.position, s = t.terrainData.size;
                if (p.x >= o.x && p.z >= o.z && p.x <= o.x + s.x && p.z <= o.z + s.z)
                    return t.SampleHeight(p) + o.y;
            }
            return p.y;
        }

        public static Vector3 OnGround(Vector3 p, float lift = 0f) => new Vector3(p.x, GroundY(p) + lift, p.z);
    }
}
