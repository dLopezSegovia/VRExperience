using System.Collections;
using System.Text;
using UnityEngine;

namespace SameShore.Tests
{
    /// <summary>
    /// Play-mode test runner (added by SameShoreSmokeTest in batch mode, never saved in the scene).
    /// Scenario from Logs/SameShoreSmokeMode.txt: "full" (default), "turtle", "human" or "xr". Writes
    /// Logs/SameShoreSmokeTest.txt and exits the Editor with 0 (all passed) or 2.
    /// </summary>
    public class SmokeTestRunner : MonoBehaviour
    {
        static string ReportPath => System.IO.Path.Combine(Application.dataPath, "../Logs/SameShoreSmokeTest.txt");
        static SmokeTestRunner _running;
        readonly StringBuilder _log = new StringBuilder();
        int _fail, _pass;

        IEnumerator Start()
        {
            // a scene reload in the Editor can bring a second copy of the test object: keep only the first
            if (_running != null && _running != this) { Destroy(gameObject); yield break; }
            _running = this;
            yield return new WaitForSeconds(1.5f);
            string modeFile = System.IO.Path.Combine(Application.dataPath, "../Logs/SameShoreSmokeMode.txt");
            string mode = System.IO.File.Exists(modeFile) ? System.IO.File.ReadAllText(modeFile).Trim() : "full";
            Check("GameManager starts in Intro (menu)", GameManager.Instance != null && GameManager.Instance.Current == GameState.Intro);
            Check("Start menu is visible", GameObject.Find("StartMenu") != null);
            Debug.Log("[SameShoreTest] scenario: " + mode);
            StartCoroutine(Watchdog(mode == "full" ? 1500f : 600f));
            switch (mode)
            {
                case "turtle": yield return StoryTests.TurtleOnly(this); break;
                case "human": yield return StoryTests.HumanOnly(this); break;
                case "xr": yield return StoryTests.Xr(this); break;
                default: yield return StoryTests.Full(this); break;
            }
        }

        IEnumerator Watchdog(float realSeconds)
        {
            yield return new WaitForSecondsRealtime(realSeconds);
            Check("Scenario finished before the watchdog timeout", false);
            Finish();
        }

        public void Check(string what, bool ok)
        {
            if (ok) _pass++; else _fail++;
            string line = (ok ? "PASS  " : "FAIL  ") + what;
            _log.AppendLine(line);
            Debug.Log("[SameShoreTest] " + line);
        }

        bool _finished;

        public void Finish()
        {
            if (_finished) return;
            _finished = true;
            Time.timeScale = 1f;
            string summary = _fail == 0 ? $"ALL CHECKS PASSED ({_pass})" : $"{_fail} CHECK(S) FAILED ({_pass} passed)";
            _log.AppendLine(summary);
            Debug.Log("[SameShoreTest] " + summary);
            System.IO.File.WriteAllText(ReportPath, _log.ToString());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(_fail == 0 ? 0 : 2);
#endif
        }
    }
}
