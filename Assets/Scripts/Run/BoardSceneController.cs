using ProjectZ.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Run
{
    public class BoardSceneController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnBoardScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.Board)
            {
                return;
            }

            var existing = FindFirstObjectByType<BoardSceneController>();
            if (existing != null)
            {
                return;
            }

            var go = new GameObject("BoardSceneController");
            go.AddComponent<BoardSceneController>();
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
                var panelWidth = 288f;
                var panelX = safeArea.x + safeArea.width - panelWidth - 12f;
                var panelY = safeArea.y + 12f;

                GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, 230f), GUI.skin.box);
                GUILayout.Label("Board Controls");
                GUILayout.Label("Current Node: " + manager.CurrentRun.boardNodeIndex);
                GUILayout.Label("Run Team Size: " + manager.CurrentRun.selectedChampionIds.Count);

                if (GUILayout.Button("Start Encounter"))
                {
                    manager.StartFight();
                }

                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }
    }
}
