using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameShore.EditorTools
{
    /// <summary>
    /// Renders reference screenshots of the built scene (batch mode, no headset).
    /// Spec: "name|x,y,z|pitch,yaw|fov|h or t;..." where t = hatchling grading + clip planes.
    /// Positions are relative to the shoreline frame: x = metres from the corridor centre,
    /// y = metres above the sand, z = metres up the beach from the shore.
    /// </summary>
    public static class SameShorePreview
    {
        public static void Shots(string spec)
        {
            string outDir = SameShoreInspector.Arg("-out", Path.GetFullPath("Logs/SameShoreShots"));
            Directory.CreateDirectory(outDir);
            var human = GameObject.Find("Volume_HumanView")?.GetComponent<Volume>();
            var hatch = GameObject.Find("Volume_HatchlingView")?.GetComponent<Volume>();
            foreach (string hide in SameShoreInspector.Arg("-hide", "").Split(','))
            {
                var h = string.IsNullOrEmpty(hide) ? null : GameObject.Find(hide);
                if (h != null) h.SetActive(false);
            }
            var shoreline = GameObject.Find("SeaPoint");
            Vector3 origin = shoreline != null ? shoreline.transform.position : Vector3.zero;

            foreach (string s in spec.Split(';'))
            {
                string[] p = s.Split('|');
                if (p.Length < 3) continue;
                string[] v = p[1].Split(',');
                float x = F(v[0]), up = F(v[1]), d = F(v[2]);
                float gx = origin.x + x, gz = origin.z - d;
                float ground = SameShoreSceneBuilderGround(gx, gz);
                Vector3 pos = new Vector3(gx, ground + up, gz);
                string[] r = p[2].Split(',');
                bool turtle = p.Length > 4 && p[4] == "t";
                var dayCycle = Object.FindFirstObjectByType<DayCycle>();
                if (dayCycle != null) dayCycle.Set(p.Length > 5 ? F(p[5]) : (turtle ? 2f : 0f));
                if (human) human.weight = turtle ? 0 : 1;
                if (hatch) hatch.weight = turtle ? 1 : 0;
                if (turtle) RenderSettings.fogDensity = 0.03f;
                SameShoreInspector.Shot(Path.Combine(outDir, p[0] + ".png"), pos, Quaternion.Euler(F(r[0]), F(r[1]), 0),
                                        p.Length > 3 ? F(p[3]) : 90f, turtle ? 0.003f : 0.05f);
            }
            if (human) human.weight = 1;
            if (hatch) hatch.weight = 0;
            Debug.Log("[SameShore] preview shots -> " + outDir);
        }

        static float SameShoreSceneBuilderGround(float x, float z)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 o = t.transform.position, s = t.terrainData.size;
                if (x >= o.x && z >= o.z && x <= o.x + s.x && z <= o.z + s.z) return t.SampleHeight(new Vector3(x, 0, z)) + o.y;
            }
            return 0;
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    }
}
