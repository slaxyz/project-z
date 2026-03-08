using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Core
{
    public class BossCelebrationSceneController : MonoBehaviour
    {
        [SerializeField] private float minDuration = 0.8f;
        private float _elapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnBossCelebrationScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.BossCelebration)
            {
                return;
            }

            if (FindFirstObjectByType<BossCelebrationSceneController>() != null)
            {
                return;
            }

            var go = new GameObject("BossCelebrationSceneController");
            go.AddComponent<BossCelebrationSceneController>();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
        }

        private void OnGUI()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var scale = DebugGuiScale.GetScale();
            var safeArea = DebugGuiScale.GetSafeArea(scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            try
            {
                var w = 700f;
                var h = 260f;
                var x = safeArea.x + (safeArea.width - w) * 0.5f;
                var y = safeArea.y + (safeArea.height - h) * 0.5f;

                GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
                GUILayout.Label("Boss Defeated!");
                GUILayout.Space(8f);
                GUILayout.Label("Zone " + manager.GetCurrentZoneNumber() + " complete.");
                GUILayout.Space(20f);

                GUI.enabled = _elapsed >= minDuration;
                if (GUILayout.Button("Continue", GUILayout.Height(52f)))
                {
                    manager.ContinueAfterBossCelebration();
                }
                GUI.enabled = true;

                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }
    }
}
