using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Core
{
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
            var w = 420f;
            var h = 140f;
            var x = (Screen.width - w) * 0.5f;
            var y = (Screen.height - h) * 0.5f;
            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
            GUILayout.Label("Loading next scene...");
            GUILayout.Label("Progress: " + Mathf.RoundToInt(progress * 100f) + "%");
            GUILayout.HorizontalSlider(progress, 0f, 1f);
            GUILayout.EndArea();
        }
    }
}
