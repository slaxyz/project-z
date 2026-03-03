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

            GUILayout.BeginArea(new Rect(Screen.width - 300f, 12f, 288f, 230f), GUI.skin.box);
            GUILayout.Label("Board Controls");
            GUILayout.Label("Current Node: " + manager.CurrentRun.boardNodeIndex);
            GUILayout.Label("Run Team Size: " + manager.CurrentRun.selectedChampionIds.Count);

            if (GUILayout.Button("Start Encounter"))
            {
                manager.StartFight();
            }

            if (GUILayout.Button("Skip To Next Node"))
            {
                manager.NextBoardNode();
            }

            if (GUILayout.Button("End Run (+10)"))
            {
                manager.EndRun(10);
            }

            GUILayout.EndArea();
        }
    }
}
