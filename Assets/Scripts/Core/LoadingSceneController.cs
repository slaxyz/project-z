using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Core
{
    public class LoadingSceneController : MonoBehaviour
    {
        [SerializeField] private float minDisplayDuration = 0.7f;

        private float _displayedProgress;

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

        private void Start()
        {
            StartCoroutine(LoadNextSceneAsync());
        }

        private IEnumerator LoadNextSceneAsync()
        {
            var manager = GameFlowManager.Instance;
            var targetScene = manager != null ? manager.NextSceneAfterLoading : GameScenes.Board;

            var op = SceneManager.LoadSceneAsync(targetScene);
            if (op == null)
            {
                yield break;
            }

            op.allowSceneActivation = false;
            var elapsed = 0f;

            while (true)
            {
                elapsed += Time.deltaTime;
                _displayedProgress = Mathf.Clamp01(op.progress / 0.9f);

                var doneLoading = op.progress >= 0.9f;
                var minimumTimeReached = elapsed >= minDisplayDuration;

                if (doneLoading && minimumTimeReached)
                {
                    break;
                }

                yield return null;
            }

            op.allowSceneActivation = true;
        }

        private void OnGUI()
        {
            var w = 420f;
            var h = 140f;
            var x = (Screen.width - w) * 0.5f;
            var y = (Screen.height - h) * 0.5f;
            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
            GUILayout.Label("Loading next scene...");
            GUILayout.Label("Progress: " + Mathf.RoundToInt(_displayedProgress * 100f) + "%");
            GUILayout.HorizontalSlider(_displayedProgress, 0f, 1f);
            GUILayout.EndArea();
        }
    }
}
