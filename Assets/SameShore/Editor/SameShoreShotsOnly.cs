using UnityEditor.SceneManagement;

namespace SameShore.EditorTools
{
    /// <summary>Batch helper: open the built scene and render preview shots without rebuilding.</summary>
    public static class SameShoreShotsOnly
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/SameShore/Scenes/SameShore.unity");
            SameShorePreview.Shots(SameShoreInspector.Arg("-shots", ""));
        }
    }
}
