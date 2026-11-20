using SameShore.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SameShore.EditorTools
{
    /// <summary>
    /// Plays the scene and runs SmokeTestRunner (full walkthrough). Report: Logs/SameShoreSmokeTest.txt.
    /// Batch (without -quit): -executeMethod SameShore.EditorTools.SameShoreSmokeTest.Run
    /// Menu: Same Shore / Run Smoke Test (Play Mode).
    /// </summary>
    public static class SameShoreSmokeTest
    {
        const string Key = "SameShoreSmokeTestPending";

        [MenuItem("Same Shore/Run Smoke Test (Play Mode)")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/SameShore/Scenes/SameShore.unity");
            var go = new GameObject("~SmokeTest");
            go.AddComponent<SmokeTestRunner>();
            EditorSceneManager.MarkSceneDirty(go.scene);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void CleanupAfterPlay()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
                SessionState.SetBool(Key, false);
                // do not leave the test object in the saved scene
                EditorSceneManager.OpenScene("Assets/SameShore/Scenes/SameShore.unity");
            };
        }
    }
}
