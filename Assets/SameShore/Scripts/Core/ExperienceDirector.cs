using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SameShore
{
    public enum ExperienceMode { Full, HumanOnly, TurtleOnly }

    /// <summary>
    /// Runs the experience: start menu -> HUMAN -> transition -> HATCHLING -> sea.
    /// Both bodies live in the same scene, so whatever the human changed on the beach is exactly
    /// what the hatchling finds. All relocations happen under a full fade.
    /// Uses the project's GameManager states (Intro / Human / Transition / Hatchling / End).
    /// </summary>
    public class ExperienceDirector : MonoBehaviour
    {
        /// <summary>Set before reloading the scene to skip the menu.</summary>
        public static ExperienceMode? PendingMode;

        [Header("Systems")]
        [SerializeField] private PlayerPhaseSwitcher phaseSwitcher;
        [SerializeField] private TurtleController turtle;
        [SerializeField] private HumanController human;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private PerceptionController perception;
        [SerializeField] private AmbientAudio ambient;
        [SerializeField] private BeachStateTracker beach;
        [SerializeField] private SeagullFlock gulls;

        [Header("Places")]
        [SerializeField] private Transform humanStart;
        [SerializeField] private Transform menuSpot;

        [Header("Timing")]
        [SerializeField] private float fadeSeconds = 1.2f;
        [SerializeField] private float lineSeconds = 3.2f;

        public ExperienceMode Mode { get; private set; }

        UIPanel _menu, _story, _end, _hint;
        TextMeshPro _storyText, _endTitle, _endBody, _hintText;
        bool _busy;
        float _hintUntil;

        void Start()
        {
            BuildUI();
            if (turtle != null)
            {
                turtle.ReachedGoal += () => StartCoroutine(TurtleSucceeded());
                turtle.Message += ShowHint;
            }
            if (gulls != null)
            {
                gulls.Caught += () => StartCoroutine(TurtleCaught());
                gulls.Message += ShowHint;
            }
            if (human != null) human.FinishRequested += () => StartCoroutine(HumanFinished());

            fader.SetImmediate(1f);
            if (PendingMode.HasValue)
            {
                var m = PendingMode.Value;
                PendingMode = null;
                StartCoroutine(Begin(m));
            }
            else StartCoroutine(ShowMenu());
        }

        void Update()
        {
            if (_hint != null && _hint.gameObject.activeSelf && Time.time > _hintUntil) _hint.Hide();
        }

        // ------------------------------------------------------------------ menu
        IEnumerator ShowMenu()
        {
            GameManager.Instance.SetState(GameState.Intro);
            human.SetActive(false);
            yield return null; // let the rig initialise its tracking pose
            human.PlaceAt(menuSpot != null ? menuSpot : humanStart);
            _menu.Show();
            _menu.Follow.Snap();
            yield return fader.FadeTo(0f, fadeSeconds * 1.5f);
        }

        void Choose(ExperienceMode mode)
        {
            if (_busy) return;
            StartCoroutine(Begin(mode));
        }

        IEnumerator Begin(ExperienceMode mode)
        {
            _busy = true;
            Mode = mode;
            _menu.Hide();
            if (fader.Alpha < 0.99f) yield return fader.FadeTo(1f, fadeSeconds);

            if (mode == ExperienceMode.TurtleOnly)
            {
                yield return Story("The same beach, a few minutes later.",
                                   "Tonight, the nest hatches.",
                                   "Now you are one of them.\n<size=70%>8 cm above the sand. Everything is twenty times bigger.</size>");
                yield return EnterTurtle();
            }
            else
            {
                human.PlaceAt(humanStart);
                GameManager.Instance.SetState(GameState.Human);
                perception.Apply(Perception.Human, true);
                ambient.Apply(Perception.Human, 0.5f);
                human.FinishLabel = mode == ExperienceMode.HumanOnly ? "End the night" : "End the night and become a hatchling";
                yield return Story("Tonight, the nest hatches.");
                yield return fader.FadeTo(0f, fadeSeconds * 1.5f);
                human.SetActive(true);
                ShowHint(DesktopTestingMode.Active
                    ? "<b>Walk</b> WASD  ·  <b>Look</b> mouse\n<b>Grab / drop</b> left click or E  ·  <b>Use</b> right click or F\n" +
                      "Tip a bucket of sand over the hole to fill it\n<b>Hold Enter</b> when you are done"
                    : "<b>Walk</b> left stick  ·  <b>Turn</b> right stick\n<b>Grab</b> grip  ·  <b>Flashlight</b> trigger while holding it\n" +
                      "Tip a bucket of sand over the hole to fill it\n<b>Hold B / Y</b> when you are done", 14f);
            }
            _busy = false;
        }

        // ------------------------------------------------------------------ human -> hatchling
        IEnumerator HumanFinished()
        {
            if (_busy) yield break;
            _busy = true;
            human.SetActive(false);
            yield return fader.FadeTo(1f, fadeSeconds);
            if (Mode == ExperienceMode.HumanOnly)
            {
                ShowEnd("The night goes on", beach.Summary(), false,
                        ("Continue as a hatchling", () => StartCoroutine(ContinueAsTurtle())),
                        ("Main menu", BackToMenu));
                _busy = false;
                yield break;
            }
            GameManager.Instance.SetState(GameState.Transition);
            yield return Story(beach.Summary(),
                               "A little later, under the same moon...",
                               "Tonight, the nest hatches.",
                               "Now you are one of them.\n<size=70%>8 cm above the sand. Everything is twenty times bigger.</size>");
            yield return EnterTurtle();
            _busy = false;
        }

        IEnumerator ContinueAsTurtle()
        {
            _end.Hide();
            Mode = ExperienceMode.Full;
            yield return Story("Tonight, the nest hatches.", "Now you are one of them.");
            yield return EnterTurtle();
        }

        IEnumerator EnterTurtle()
        {
            GameManager.Instance.SetState(GameState.Hatchling); // PlayerPhaseSwitcher shrinks the rig into the nest
            perception.Body = turtle.transform;
            perception.Apply(Perception.Hatchling, false);
            ambient.Apply(Perception.Hatchling, 2f);
            yield return new WaitForSeconds(0.3f);
            yield return fader.FadeTo(0f, fadeSeconds * 1.6f);
            ShowHint(DesktopTestingMode.Active
                ? "<b>Reach the sea.</b>\nW: crawl  ·  A / D: turn slowly  ·  S: crawl backwards\n<b>Space</b>: sudden burst  ·  mouse: look around"
                : "<b>Reach the sea.</b>\nLeft stick forward: crawl  ·  left / right: turn slowly\n<b>A / X</b>: sudden burst  ·  back: crawl backwards", 12f);
        }

        // ------------------------------------------------------------------ endings
        IEnumerator TurtleSucceeded()
        {
            AudioOneShot.Play2D(SameShoreAssets.I.success, 0.7f);
            yield return new WaitForSeconds(1.2f);
            if (gulls != null) gulls.Stop();
            string body = $"You reached the sea in {Format(turtle.ElapsedTime)}.\n" +
                          $"<size=80%>{turtle.Strokes} flipper strokes  ·  stuck {turtle.TimesStuck} times  ·  " +
                          $"pulled by artificial light for {Mathf.RoundToInt(turtle.SecondsPulledByArtificialLight)} s</size>\n\n" +
                          "<size=80%>Only about 1 in 1,000 hatchlings survives to adulthood.\nWhat we leave on the beach decides the odds.</size>";
            ShowEnd("Great Job", body, true,
                    ("Crawl again", () => StartCoroutine(RetryTurtle())),
                    ("Main menu", BackToMenu));
        }

        IEnumerator TurtleCaught()
        {
            turtle.enabled = false;
            yield return new WaitForSeconds(0.6f);
            ShowEnd("A seagull caught you",
                    "<size=80%>On open sand, with no cover and lights pulling them off course,\nmost hatchlings never reach the water.</size>",
                    true, ("Try again", () => StartCoroutine(RetryTurtle())), ("Main menu", BackToMenu));
        }

        IEnumerator RetryTurtle()
        {
            _end.Hide();
            yield return fader.FadeTo(1f, fadeSeconds);
            GameManager.Instance.SetState(GameState.Hatchling);
            phaseSwitcher.ResetTurtle();
            if (gulls != null) gulls.Begin();
            yield return fader.FadeTo(0f, fadeSeconds);
        }

        void BackToMenu()
        {
            StartCoroutine(Reload());
        }

        IEnumerator Reload()
        {
            _end.Hide();
            yield return fader.FadeTo(1f, fadeSeconds);
            PendingMode = null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0 ? SceneManager.GetActiveScene().buildIndex : 0);
        }

        // ------------------------------------------------------------------ UI
        IEnumerator Story(params string[] lines)
        {
            ambient.Duck(0.55f, 0.6f);
            AudioOneShot.Play2D(SameShoreAssets.I.transition, 0.5f);
            foreach (string line in lines)
            {
                _storyText.text = line;
                _storyText.alpha = 0;
                _story.Show();
                _story.Follow.Snap();
                for (float t = 0; t < 0.6f; t += Time.deltaTime) { _storyText.alpha = t / 0.6f; yield return null; }
                _storyText.alpha = 1;
                float hold = lineSeconds + line.Length * 0.025f;
                for (float t = 0; t < hold; t += Time.deltaTime)
                {
                    if (XRInput.AnyTriggerDown || XRInput.KeyDown(XRInput.KeyId.Space)) break; // skip
                    yield return null;
                }
                for (float t = 0; t < 0.5f; t += Time.deltaTime) { _storyText.alpha = 1 - t / 0.5f; yield return null; }
            }
            _story.Hide();
            ambient.Duck(1f, 1.2f);
        }

        void ShowEnd(string title, string body, bool fadeIn, params (string label, System.Action action)[] buttons)
        {
            _endTitle.text = title;
            _endBody.text = body;
            _end.ClearButtons();
            float w = 0.52f, gap = 0.06f;
            float x0 = -(buttons.Length - 1) * (w + gap) * 0.5f;
            XRInput.KeyId[] keys = { XRInput.KeyId.R, XRInput.KeyId.M };
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                _end.AddButton(b.label, null, new Vector2(x0 + i * (w + gap), -0.26f), new Vector2(w, 0.13f),
                               () => b.action(), i < keys.Length ? keys[i] : (XRInput.KeyId?)null);
            }
            _end.Show();
            _end.Follow.Snap();
            if (_hint != null) _hint.Hide();
        }

        public void ShowHint(string text) => ShowHint(text, 6f);

        public void ShowHint(string text, float seconds)
        {
            _hintText.text = text;
            _hint.Show();
            _hint.Follow.Snap();
            _hintUntil = Time.time + seconds;
        }

        void BuildUI()
        {
            Color title = new Color(0.86f, 0.93f, 1f);
            Color accent = new Color(0.45f, 0.78f, 0.82f);

            _menu = UIPanel.Create("StartMenu", new Vector2(1.5f, 1.02f), 1.7f);
            _menu.Follow.follow = false;
            _menu.AddText("O N E   B E A C H ,   T W O   B O D I E S", 0.32f, new Vector2(0, 0.42f), new Vector2(1.4f, 0.08f), accent);
            _menu.AddText("SAME SHORE", 1.45f, new Vector2(0, 0.3f), new Vector2(1.4f, 0.2f), title, FontStyles.Bold);
            _menu.AddText("Experience the same beach first as a person, then as a newborn sea turtle.\nWhat you change as a human is what the hatchling will face.",
                          0.34f, new Vector2(0, 0.15f), new Vector2(1.35f, 0.12f), new Color(0.8f, 0.85f, 0.9f));
            _menu.AddButton("Full Experience", "Human, then hatchling", new Vector2(0, -0.03f), new Vector2(0.9f, 0.15f),
                            () => Choose(ExperienceMode.Full), XRInput.KeyId.Digit1);
            _menu.AddButton("Human Experience", "Explore the beach at night", new Vector2(-0.235f, -0.22f), new Vector2(0.44f, 0.15f),
                            () => Choose(ExperienceMode.HumanOnly), XRInput.KeyId.Digit2);
            _menu.AddButton("Turtle Experience", "Start in the nest", new Vector2(0.235f, -0.22f), new Vector2(0.44f, 0.15f),
                            () => Choose(ExperienceMode.TurtleOnly), XRInput.KeyId.Digit3);
            _menu.AddText(DesktopTestingMode.Active ? "Desktop testing: click a button with the mouse (or press 1 / 2 / 3)"
                                                    : "Point with a controller and pull the trigger", 0.26f, new Vector2(0, -0.42f), new Vector2(1.3f, 0.06f), new Color(0.6f, 0.68f, 0.75f));

            _story = UIPanel.Create("Story", new Vector2(1.3f, 0.4f), 1.6f, new Color(0, 0, 0, 0));
            _story.Follow.follow = false;
            _storyText = _story.AddText("", 0.62f, Vector2.zero, new Vector2(1.25f, 0.38f), title);

            _end = UIPanel.Create("EndScreen", new Vector2(1.3f, 0.78f), 1.5f);
            _end.Follow.follow = false;
            _endTitle = _end.AddText("", 1.1f, new Vector2(0, 0.25f), new Vector2(1.2f, 0.18f), title, FontStyles.Bold);
            _endBody = _end.AddText("", 0.38f, new Vector2(0, 0.0f), new Vector2(1.2f, 0.32f), new Color(0.82f, 0.88f, 0.93f));

            _hint = UIPanel.Create("Hint", new Vector2(1.05f, 0.27f), 1.25f);
            _hint.Follow.heightOffset = -0.3f;
            _hintText = _hint.AddText("", 0.36f, Vector2.zero, new Vector2(1.0f, 0.25f), new Color(0.9f, 0.95f, 1f));
        }

        static string Format(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";
    }
}
