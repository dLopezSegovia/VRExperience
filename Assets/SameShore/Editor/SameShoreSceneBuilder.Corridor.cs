using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static SameShore.EditorTools.BuilderUtil;

namespace SameShore.EditorTools
{
    /// <summary>
    /// The hatchling's way from the nest to the sea, framed by the beach itself: low dune berms sculpted into a copy
    /// of the terrain (the team's terrain asset is never modified), lined with dune grass, rocks and driftwood.
    /// Safety colliders hide inside the crests and are solid only in the hatchling's act (TurtleBounds).
    /// </summary>
    public static partial class SameShoreSceneBuilder
    {
        const float BermHalfWidth = 0.85f;

        static float BermHeight(float d, int edge, float along)
        {
            // taller up the beach, flattening out on the wet sand near the water
            float h = 0.32f + 0.08f * Mathf.Sin(along * 1.7f + edge * 2.1f) + 0.05f * Mathf.Sin(along * 4.3f + edge);
            return h * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 2.4f, d));
        }

        static List<Vector2[]> CorridorEdges() => new List<Vector2[]> { LeftEdgeXY, RightEdgeXY, BackEdgeXY };

        /// <summary>World xz of a (dx, d) layout point (no ground lookup: used before/while sculpting).</summary>
        static Vector2 WXZ(Vector2 xy) => new Vector2(cx + xy.x, zs - xy.y);

        /// <summary>Raises the berms in a generated copy of each terrain the corridor touches.</summary>
        static void SculptCorridor()
        {
            var edges = CorridorEdges();
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var e in edges)
                foreach (var xy in e)
                {
                    Vector2 w = WXZ(xy);
                    min = Vector2.Min(min, w);
                    max = Vector2.Max(max, w);
                }
            min -= Vector2.one * (BermHalfWidth + 0.5f);
            max += Vector2.one * (BermHalfWidth + 0.5f);
            EnsureFolder(Gen + "/Terrain");
            int ti = 0;
            foreach (Terrain t in _terrains)
            {
                Vector3 tp = t.transform.position, size = t.terrainData.size;
                if (max.x < tp.x || min.x > tp.x + size.x || max.y < tp.z || min.y > tp.z + size.z) continue;
                // copy the data, then sculpt the copy
                string path = $"{Gen}/Terrain/SameShoreTerrain_{ti++}.asset";
                AssetDatabase.DeleteAsset(path);
                var data = Object.Instantiate(t.terrainData);
                AssetDatabase.CreateAsset(data, path);
                t.terrainData = data;
                var tc = t.GetComponent<TerrainCollider>();
                if (tc != null) tc.terrainData = data;

                int res = data.heightmapResolution;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - tp.x) / size.x * (res - 1)), 0, res - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((max.x - tp.x) / size.x * (res - 1)), 0, res - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt((min.y - tp.z) / size.z * (res - 1)), 0, res - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt((max.y - tp.z) / size.z * (res - 1)), 0, res - 1);
                int w = x1 - x0 + 1, h = y1 - y0 + 1;
                if (w <= 1 || h <= 1) continue;
                float[,] hs = data.GetHeights(x0, y0, w, h);
                int raised = 0;
                for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float wx = tp.x + (x0 + i) / (float)(res - 1) * size.x;
                    float wz = tp.z + (y0 + j) / (float)(res - 1) * size.z;
                    float add = 0f;
                    for (int e = 0; e < edges.Count; e++)
                    {
                        float dist = DistToPolyline(new Vector2(wx, wz), edges[e], out float along, out float dHere);
                        if (dist >= BermHalfWidth) continue;
                        float k = 0.5f + 0.5f * Mathf.Cos(dist / BermHalfWidth * Mathf.PI);   // smooth cosine profile
                        add = Mathf.Max(add, BermHeight(dHere, e, along) * k);
                    }
                    if (add <= 0f) continue;
                    hs[j, i] = Mathf.Clamp01(hs[j, i] + add / size.y);
                    raised++;
                }
                data.SetHeights(x0, y0, hs);
                EditorUtility.SetDirty(data);
                Debug.Log($"[SameShore] corridor berms sculpted into {path}: {raised} samples, spacing {size.x / (res - 1):F3} m");
            }
            AssetDatabase.SaveAssets();
        }

        static float DistToPolyline(Vector2 p, Vector2[] layout, out float along, out float dHere)
        {
            float best = float.MaxValue;
            along = 0f;
            dHere = 0f;
            float acc = 0f;
            for (int i = 0; i < layout.Length - 1; i++)
            {
                Vector2 a = WXZ(layout[i]), b = WXZ(layout[i + 1]);
                Vector2 ab = b - a;
                float len = ab.magnitude;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, len * len));
                float dd = (p - (a + ab * t)).magnitude;
                if (dd < best)
                {
                    best = dd;
                    along = acc + t * len;
                    dHere = Mathf.Lerp(layout[i].y, layout[i + 1].y, t);
                }
                acc += len;
            }
            return best;
        }

        static float EdgeAt(Vector2[] edge, float d)
        {
            for (int i = 0; i < edge.Length - 1; i++)
            {
                float a = edge[i].y, b = edge[i + 1].y;
                if ((d - a) * (d - b) <= 0f) return Mathf.Lerp(edge[i].x, edge[i + 1].x, Mathf.InverseLerp(a, b, d));
            }
            return d > edge[0].y ? edge[0].x : edge[edge.Length - 1].x;
        }

        /// <summary>A trigger box spanning the corridor (crest to crest) at 'd' metres from the shore.</summary>
        static StoryTrigger CorridorTrigger(string id, float d, float depth)
        {
            float l = EdgeAt(LeftEdgeXY, d), r = EdgeAt(RightEdgeXY, d);
            Transform parent = _env.Find("TurtleCorridor") ?? Child(_env, "TurtleCorridor").transform;
            var go = Child(parent, "Trigger_" + id);
            go.transform.position = L((l + r) * 0.5f, d);
            var t = go.AddComponent<StoryTrigger>();
            t.Id = id;
            t.size = new Vector3(r - l + 0.6f, 2.5f, depth);
            t.center = new Vector3(0, 0.6f, 0);
            return t;
        }

        /// <summary>Grass, rocks and driftwood along the crests, and the hidden safety colliders inside them.</summary>
        static void BuildCorridor()
        {
            var root = Child(_env, "TurtleCorridor").transform;
            var bounds = Child(root, "TurtleBounds").transform;
            var edges = CorridorEdges();
            int rockIndex = 30;
            for (int e = 0; e < edges.Count; e++)
            {
                var layout = edges[e];
                float acc = 0f;
                for (int i = 0; i < layout.Length - 1; i++)
                {
                    Vector3 a = LX(layout[i]), b = LX(layout[i + 1]);
                    Vector3 ab = b - a;
                    float len = new Vector2(ab.x, ab.z).magnitude;
                    Vector3 dir = new Vector3(ab.x, 0, ab.z).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, dir);

                    // hidden barrier: a thin wall along the crest, 0.45 m above the crest
                    var wall = new GameObject("Limit").AddComponent<BoxCollider>();
                    wall.transform.SetParent(bounds, false);
                    Vector3 mid = (a + b) * 0.5f;
                    wall.transform.SetPositionAndRotation(mid + Vector3.up * 0.1f, Quaternion.LookRotation(dir, Vector3.up));
                    wall.size = new Vector3(0.14f, 0.7f, len + 0.3f);

                    // dressing every ~0.9 m: grass clumps, a rock now and then, driftwood lying along the crest
                    for (float s = 0.2f; s < len; s += Rand(0.7f, 1.1f))
                    {
                        float at = acc + s;
                        Vector3 p = a + dir * s + side * Rand(-0.35f, 0.35f);
                        p = G(p.x, p.z);
                        int pick = Mathf.FloorToInt(Mathf.Repeat(at * 1.37f + e * 0.5f, 7f));
                        if (pick == 0)
                            Rock(root, $"{PH}/sand_rocks_small_01/sand_rocks_small_01.fbx", rockIndex++, p, Rand(0.4f, 0.65f), M["sand_rocks_small_01"]);
                        else if (pick == 3 && e < 2)
                        {
                            var wd = Model($"{PH}/dead_quiver_branch_01/dead_quiver_branch_01.fbx", root, "Driftwood");
                            if (wd != null)
                            {
                                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + Rand(-20, 20);
                                wd.transform.rotation = OnGround(p, yaw) * wd.transform.rotation;
                                PlaceOn(wd, p, 0.03f);
                                Remap(wd, new Dictionary<string, Material>(), M["dead_quiver_branch_01"]);
                                AddMeshColliders(wd, true);
                            }
                        }
                        for (int g = 0; g < 2; g++)
                        {
                            Vector3 gp = G(p.x + Rand(-0.3f, 0.3f), p.z + Rand(-0.3f, 0.3f));
                            var grass = ModelPart($"{PH}/grass_medium_01/grass_medium_01.fbx", Rng.Next(100), root, "DuneGrass");
                            if (grass == null) break;
                            grass.transform.rotation = Quaternion.Euler(0, Rand(0, 360), 0) * grass.transform.rotation;
                            grass.transform.localScale *= Rand(0.7f, 1.2f);
                            PlaceOn(grass, gp, 0.03f);
                            foreach (var mr in grass.GetComponentsInChildren<MeshRenderer>())
                            {
                                mr.sharedMaterial = M["grass_medium_01"];
                                mr.shadowCastingMode = ShadowCastingMode.Off;
                            }
                        }
                    }
                    acc += len;
                }
            }
            bounds.gameObject.AddComponent<TurtleBounds>();
        }
    }
}
