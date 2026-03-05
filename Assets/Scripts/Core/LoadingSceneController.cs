using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Core
{
    public static class DebugGuiScale
    {
        public static float GetScale()
        {
            var safeHeight = Screen.safeArea.height;

            if (safeHeight >= 2200f)
            {
                return 1.8f;
            }

            if (safeHeight >= 1800f)
            {
                return 1.6f;
            }

            if (safeHeight >= 1400f)
            {
                return 1.3f;
            }

            return 1f;
        }

        public static Rect GetSafeArea(float scale)
        {
            var safe = Screen.safeArea;
            return new Rect(
                safe.x / scale,
                safe.y / scale,
                safe.width / scale,
                safe.height / scale);
        }
    }

    public class LoadingSceneController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnLoadingScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.Loading)
            {
                return;
            }

            var existing = FindFirstObjectByType<LoadingSceneController>();
            if (existing != null)
            {
                return;
            }

            var go = new GameObject("LoadingSceneController");
            go.AddComponent<LoadingSceneController>();
        }

        private void OnGUI()
        {
            var manager = GameFlowManager.Instance;
            var progress = manager != null ? manager.LoadingProgress : 0f;
            var previousMatrix = GUI.matrix;
            var scale = DebugGuiScale.GetScale();
            var safeArea = DebugGuiScale.GetSafeArea(scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var w = 420f;
            var h = 140f;
            var x = safeArea.x + (safeArea.width - w) * 0.5f;
            var y = safeArea.y + (safeArea.height - h) * 0.5f;

            try
            {
                GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
                GUILayout.Label("Loading next scene...");
                GUILayout.Label("Progress: " + Mathf.RoundToInt(progress * 100f) + "%");
                GUILayout.HorizontalSlider(progress, 0f, 1f);
                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }
    }
}
