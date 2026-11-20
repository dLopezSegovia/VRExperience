using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    public enum ObjectiveState { Pending, Done, Left }

    /// <summary>One objective, evaluated against the real state of the scene every frame.</summary>
    public class Objective
    {
        public string Id;
        public string Title;
        public Func<bool> Check;
        /// <summary>Where the spatial marker should point (null = no marker).</summary>
        public Func<Vector3?> Target;
        /// <summary>Optional progress text, e.g. "3/8".</summary>
        public Func<string> Progress;
        /// <summary>Short contextual explanation shown the first time the player gets close to the target.</summary>
        public string Tip;
        public float TipRadius = 3.2f;
        /// <summary>Sticky objectives stay done (journey steps); non-sticky ones mirror the world (flashlight on/off).</summary>
        public bool Sticky;
        /// <summary>Optional: no marker/arrow (e.g. the hatchling has to find the sea by itself).</summary>
        public bool HideMarker;

        public ObjectiveState State { get; internal set; }
        public float StateChangedAt { get; internal set; }
        internal bool TipShown;
    }

    /// <summary>
    /// Tracks the current list of objectives (human: "YOUR OBJECTIVES", hatchling: "YOUR JOURNEY"),
    /// drives the world-space HUD and the spatial marker, and raises events for the narrative.
    /// Objectives are presented in order (the first unfinished one is the current one) but the
    /// player can complete them in any order or leave them unfinished.
    /// </summary>
    public class ObjectiveTracker : MonoBehaviour
    {
        public static ObjectiveTracker Instance { get; private set; }

        public string Title { get; private set; }
        public IReadOnlyList<Objective> Objectives => _list;
        public Objective Current { get; private set; }
        public bool Running { get; private set; }

        public event Action<Objective> Completed;
        public event Action<Objective> Reopened;
        public event Action<Objective, string> TipRequested;

        readonly List<Objective> _list = new List<Objective>();
        ObjectiveHUD _hud;
        ObjectiveMarker _marker;

        void Awake()
        {
            Instance = this;
            _hud = new GameObject("ObjectivesHUD").AddComponent<ObjectiveHUD>();
            _marker = new GameObject("ObjectiveMarker").AddComponent<ObjectiveMarker>();
        }

        public ObjectiveHUD HUD => _hud;

        public void Begin(string title, IEnumerable<Objective> objectives)
        {
            Title = title;
            _list.Clear();
            _list.AddRange(objectives);
            foreach (var o in _list) { o.State = ObjectiveState.Pending; o.StateChangedAt = Time.time; o.TipShown = false; }
            Running = true;
            _hud.Build(title, _list);
            _hud.Visible = true;
        }

        public void Stop(bool hide = true)
        {
            Running = false;
            Current = null;
            _marker.SetTarget(null);
            if (hide) _hud.Visible = false;
        }

        public Objective Get(string id) => _list.Find(o => o.Id == id);
        public bool IsDone(string id) => Get(id)?.State == ObjectiveState.Done;

        /// <summary>The player moves on: everything still pending is marked as left behind.</summary>
        public void MarkPendingAsLeft()
        {
            bool any = false;
            foreach (var o in _list)
            {
                if (o.State != ObjectiveState.Pending) continue;
                Set(o, ObjectiveState.Left);
                any = true;
            }
            if (any) AudioOneShot.Play2D(SameShoreAssets.I.objectiveLeft, 0.5f);
        }

        /// <summary>Completes an objective that has no world check (e.g. "Return to the nest").</summary>
        public void ForceComplete(string id)
        {
            var o = Get(id);
            if (o != null && o.State != ObjectiveState.Done) Complete(o);
        }

        void Update()
        {
            if (!Running) return;
            foreach (var o in _list)
            {
                if (o.Check == null || o.State == ObjectiveState.Left) continue;
                bool done = SafeCheck(o);
                if (done && o.State == ObjectiveState.Pending) Complete(o);
                else if (!done && o.State == ObjectiveState.Done && !o.Sticky)
                {
                    Set(o, ObjectiveState.Pending);
                    Reopened?.Invoke(o);
                }
            }
            Current = _list.Find(o => o.State == ObjectiveState.Pending);
            Vector3? target = Current != null && !Current.HideMarker && Current.Target != null ? Current.Target() : null;
            _marker.SetTarget(target);
            _hud.SetCurrent(Current, target);
            _hud.Refresh(_list);

            if (Current != null && !Current.TipShown && !string.IsNullOrEmpty(Current.Tip) && target.HasValue && PlayerRig.Head != null)
            {
                Vector3 d = target.Value - PlayerRig.Head.position;
                d.y = 0;
                if (d.magnitude < Current.TipRadius * PlayerRig.Scale)
                {
                    Current.TipShown = true;
                    TipRequested?.Invoke(Current, Current.Tip);
                }
            }
        }

        static bool SafeCheck(Objective o)
        {
            try { return o.Check(); }
            catch (Exception e) { Debug.LogWarning($"[SameShore] objective '{o.Id}' check failed: {e.Message}"); return false; }
        }

        void Complete(Objective o)
        {
            Set(o, ObjectiveState.Done);
            AudioOneShot.Play2D(SameShoreAssets.I.objectiveDone, 0.55f);
            XRInput.HapticBoth(0.25f, 0.08f);
            Completed?.Invoke(o);
        }

        void Set(Objective o, ObjectiveState s)
        {
            o.State = s;
            o.StateChangedAt = Time.time;
            _hud.Animate(o);
        }
    }
}
