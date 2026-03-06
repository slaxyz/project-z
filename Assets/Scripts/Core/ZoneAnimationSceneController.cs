using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Core
{
    public class ZoneAnimationSceneController : MonoBehaviour
    {
        [SerializeField] private float minDuration = 0.8f;
        private float _elapsed;
        private bool _triggered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnZoneAnimationScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.ZoneAnimation)
            {
                return;
            }

            if (FindFirstObjectByType<ZoneAnimationSceneController>() != null)
            {
                return;
            }

            var go = new GameObject("ZoneAnimationSceneController");
            go.AddComponent<ZoneAnimationSceneController>();
        }

        private void Update()
        {
            if (_triggered)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed < minDuration)
            {
                return;
            }

            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            _triggered = true;
            manager.OpenBoard();
        }

        private void OnGUI()
        {
            var manager = GameFlowManager.Instance;
            var zone = manager != null ? manager.GetCurrentZoneNumber() : 1;
            var tile = manager != null ? manager.GetActiveTileIndex() + 1 : 1;

            var previousMatrix = GUI.matrix;
            var scale = DebugGuiScale.GetScale();
            var safeArea = DebugGuiScale.GetSafeArea(scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            try
            {
                var w = 620f;
                var h = 220f;
                var x = safeArea.x + (safeArea.width - w) * 0.5f;
                var y = safeArea.y + (safeArea.height - h) * 0.5f;

                GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
                GUILayout.Label("Entering Zone " + zone);
                GUILayout.Space(8f);
                GUILayout.Label("Preparing board tile " + tile + "...");
                GUILayout.Space(12f);
                var progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.1f, minDuration));
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
