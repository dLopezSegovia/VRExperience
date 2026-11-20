using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SameShore.Tests
{
    /// <summary>
    /// Automated play-mode walkthrough (added by SameShoreSmokeTest in batch mode, never in the scene):
    /// menu -> human changes the beach -> transition -> hatchling -> sea. Writes a report and exits.
    /// </summary>
    public class SmokeTestRunner : MonoBehaviour
    {
        static string ReportPath => System.IO.Path.Combine(Application.dataPath, "../Logs/SameShoreSmokeTest.txt");
        readonly StringBuilder _log = new StringBuilder();
        int _fail;

        IEnumerator Start()
        {
            Time.timeScale = 4f;
            yield return Wait(2f);
            var director = FindFirstObjectByType<ExperienceDirector>();
            var gm = GameManager.Instance;
            Check("GameManager starts in Intro (menu)", gm != null && gm.Current == GameState.Intro);
            Check("Start menu is visible", GameObject.Find("StartMenu") != null && GameObject.Find("StartMenu").activeSelf);

            // ---- Full experience
            director.GetType().GetMethod("Choose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, new object[] { ExperienceMode.Full });
            yield return WaitFor(() => FindFirstObjectByType<SmoothLocomotion>().MovementEnabled, 40f);
            Check("Human phase active", gm.Current == GameState.Human);
            var loco = FindFirstObjectByType<SmoothLocomotion>();
            Check("Human locomotion enabled", loco != null && loco.MovementEnabled);
            Transform head = PlayerRig.Head;
            Check($"Human eye scale = 1 (got {PlayerRig.Scale:F2})", Mathf.Approximately(PlayerRig.Scale, 1f));

            // human changes the beach
            var bin = FindFirstObjectByType<TrashBin>();
            var items = new System.Collections.Generic.List<TrashItem>(TrashItem.All);
            Check($"Litter present ({items.Count} items)", items.Count >= 8);
            TrashItem bottle = items.Find(t => t.Label == "bottle");
            bottle.GetComponent<Rigidbody>().position = bin.transform.position + Vector3.up * 0.2f;
            bottle.transform.position = bin.transform.position + Vector3.up * 0.2f;
            Vector3 binnedPos = bottle.transform.position;
            var flashlight = FindFirstObjectByType<Flashlight>();
            Check("Flashlight starts ON", flashlight.IsOn);
            var hole = FindFirstObjectByType<SandHole>();
            Check("Hole starts open", !hole.IsFilled);
            float holeDepthBefore = Ground(hole.transform.position);
            float rimRef = Ground(hole.transform.position + Vector3.right * 1.5f);
            Check($"Hole is carved into the terrain ({rimRef - holeDepthBefore:F2} m deep)", rimRef - holeDepthBefore > 0.15f);
            flashlight.Toggle();
            hole.AddSand(1f);
            hole.AddSand(1f);
            yield return Wait(1.5f);
            Check("Bottle counted as removed (in bin)", bottle.InBin && bottle.Cleared);
            Check("Flashlight switched OFF by the human", !flashlight.IsOn);
            Check("Hole filled with the bucket sand", hole.IsFilled);

            // ---- end human phase -> hatchling
            var human = FindFirstObjectByType<HumanController>();
            Run(director, "HumanFinished");
            yield return WaitFor(() => gm.Current == GameState.Hatchling && ScreenFader.Instance.Alpha < 0.05f, 90f);
            Check("Hatchling phase active", gm.Current == GameState.Hatchling);
            Check($"Player scale = 0.05 (got {PlayerRig.Scale:F3})", Mathf.Abs(PlayerRig.Scale - 0.05f) < 0.001f);
            var turtle = FindFirstObjectByType<TurtleController>();
            float eye = PlayerRig.Head.position.y - turtle.transform.position.y;
            Check($"Rig is carried by TurtleBody", PlayerRig.Root != null && PlayerRig.Rig.transform.IsChildOf(turtle.transform));
            Check($"Camera near clip for 8 cm eyes ({PlayerRig.HeadCamera.nearClipPlane})", PlayerRig.HeadCamera.nearClipPlane <= 0.005f);
            Check("TurtleController running", turtle.enabled);
            Check("Human sea barrier disabled for the hatchling", GameObject.Find("HumanSeaBarrier") == null);

            // ---- persistence of the human's changes
            Check("PERSIST: bottle still in the bin", (bottle.transform.position - binnedPos).magnitude < 0.6f && bottle.InBin);
            Check("PERSIST: flashlight still OFF", !flashlight.IsOn);
            Check("PERSIST: hole still filled, trap disabled", hole.IsFilled && !hole.GetComponent<TurtleZone>().enabled);

            // ---- light attraction
            Vector3 pullOff = LightSource.GetNetPull(turtle.transform.position, out float artOff);
            Check($"Natural sea glow pulls towards the sea (z={pullOff.normalized.z:F2})", pullOff.normalized.z > 0.5f);
            flashlight.Toggle();
            Vector3 probe = flashlight.transform.position + flashlight.transform.forward * 3f;
            Vector3 pullOn = LightSource.GetNetPull(probe, out float artOn);
            Vector3 toLamp = flashlight.transform.position - probe; toLamp.y = 0;
            Check($"Lit flashlight dominates in its beam (artificial share {artOn:F2})", artOn > 0.5f && Vector3.Dot(pullOn.normalized, toLamp.normalized) > 0.5f);
            flashlight.Toggle();

            // ---- crawling: simulate strokes by teleporting along the corridor, then reach the sea
            var sea = GameObject.Find("SeaPoint").transform.position;
            turtle.TeleportTo(new Vector3(sea.x, Ground(sea - Vector3.forward * 2f) + 0.02f, sea.z - 2f), 0f);
            yield return Wait(0.5f);
            Check("Turtle not yet at goal 2 m from the sea", gm.Current == GameState.Hatchling);
            turtle.TeleportTo(new Vector3(sea.x, Ground(sea - Vector3.forward * 0.3f) + 0.02f, sea.z - 0.3f), 0f);
            yield return WaitFor(() => GameObject.Find("EndScreen") != null, 20f);
            Check("Reaching the sea ends the game (state End)", gm.Current == GameState.End);
            var end = GameObject.Find("EndScreen");
            Check("End screen visible", end != null && end.activeSelf);
            var title = end != null ? end.GetComponentInChildren<TMPro.TextMeshPro>() : null;
            Check($"End screen says 'Great Job' ({(title != null ? title.text : "-")})", title != null && title.text == "Great Job");

            // ---- retry keeps the beach
            Run(director, "RetryTurtle");
            yield return WaitFor(() => gm.Current == GameState.Hatchling && ScreenFader.Instance.Alpha < 0.05f, 20f);
            Check("Retry: hatchling back in the nest", gm.Current == GameState.Hatchling && turtle.enabled &&
                  (turtle.transform.position - GameObject.Find("TurtleSpawn").transform.position).magnitude < 0.3f);
            Check("Retry: beach still as the human left it", hole.IsFilled && bottle.InBin && !flashlight.IsOn);

            // ---- real crawling with simulated input (W = stick forward)
            Time.timeScale = 1f;
            // batch mode has no focused Game view: route device input to play mode (temporary settings instance)
            var settings = Instantiate(UnityEngine.InputSystem.InputSystem.settings);
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            UnityEngine.InputSystem.InputSystem.settings = settings;
            var kb = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            kb.MakeCurrent();
            Vector3 p0 = turtle.transform.position;
            int strokes0 = turtle.Strokes;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
            yield return new WaitForSecondsRealtime(6f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return new WaitForSecondsRealtime(0.8f);
            Vector3 moved = turtle.transform.position - p0;
            Check($"Crawl: {turtle.Strokes - strokes0} strokes in 6 s, moved {new Vector2(moved.x, moved.z).magnitude:F2} m", turtle.Strokes - strokes0 >= 5 && new Vector2(moved.x, moved.z).magnitude > 0.5f);
            Check($"Crawl: heading to the sea (dz={moved.z:F2})", moved.z > 0.4f);
            Check($"Crawl: stays on the sand (dy={moved.y:F2})", Mathf.Abs(moved.y) < 0.6f);

            float yaw0 = turtle.transform.eulerAngles.y;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.D));
            yield return new WaitForSecondsRealtime(2f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            float turned = Mathf.DeltaAngle(yaw0, turtle.transform.eulerAngles.y);
            Check($"Turn: ~25 deg/s (turned {turned:F0} deg in 2 s)", turned > 35f && turned < 60f);

            // light pull deflects strokes: with the flashlight on, crawl near its beam
            flashlight.Toggle();
            Vector3 fp = flashlight.transform.position, ff = flashlight.transform.forward; ff.y = 0; ff.Normalize();
            Vector3 start = fp + ff * 3f;
            Vector3 side = Vector3.Cross(Vector3.up, ff);
            turtle.TeleportTo(new Vector3(start.x, Ground(start) + 0.02f, start.z), Quaternion.LookRotation(side).eulerAngles.y);
            yield return new WaitForSecondsRealtime(0.3f);
            Vector3 q0 = turtle.transform.position;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
            yield return new WaitForSecondsRealtime(4f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            Vector3 dq = turtle.transform.position - q0; dq.y = 0;
            float towardLamp = Vector3.Dot(dq, -ff);
            Check($"Light pull: crawling across the beam drifts towards the flashlight ({towardLamp:F2} m)", towardLamp > 0.1f);
            Check($"Light pull: time pulled by artificial light counted ({turtle.SecondsPulledByArtificialLight:F1} s)", turtle.SecondsPulledByArtificialLight > 0.5f);

            Finish();
        }

        static float Ground(Vector3 p)
        {
            Terrain t = Terrain.activeTerrain;
            foreach (Terrain tt in Terrain.activeTerrains)
            {
                Vector3 o = tt.transform.position, s = tt.terrainData.size;
                if (p.x >= o.x && p.z >= o.z && p.x <= o.x + s.x && p.z <= o.z + s.z) t = tt;
            }
            return t.SampleHeight(p) + t.transform.position.y;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float timeoutRealSeconds)
        {
            float end = Time.realtimeSinceStartup + timeoutRealSeconds;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        static void Run(MonoBehaviour target, string coroutine)
        {
            var m = target.GetType().GetMethod(coroutine, BindingFlags.Instance | BindingFlags.NonPublic);
            target.StartCoroutine((IEnumerator)m.Invoke(target, null));
        }

        void Check(string what, bool ok)
        {
            if (!ok) _fail++;
            string line = (ok ? "PASS  " : "FAIL  ") + what;
            _log.AppendLine(line);
            Debug.Log("[SameShoreTest] " + line);
        }

        void Finish()
        {
            Time.timeScale = 1f;
            _log.AppendLine(_fail == 0 ? "ALL CHECKS PASSED" : $"{_fail} CHECK(S) FAILED");
            Debug.Log("[SameShoreTest] " + (_fail == 0 ? "ALL CHECKS PASSED" : _fail + " FAILED"));
            System.IO.File.WriteAllText(ReportPath, _log.ToString());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(_fail == 0 ? 0 : 2);
#endif
        }

        void OnApplicationQuit() { }
    }
}
