using ProjectZ.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Run
{
    public class ResultSceneController : MonoBehaviour
    {
        [SerializeField] private int defeatEndRunReward = 5;
        [SerializeField] private int victoryEndRunReward = 15;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnResultScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.Result)
            {
                return;
            }

            var existing = FindFirstObjectByType<ResultSceneController>();
            if (existing != null)
            {
                return;
            }

            var go = new GameObject("ResultSceneController");
            go.AddComponent<ResultSceneController>();
        }

        private void OnGUI()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (!manager.HasLastFightResult)
            {
                GUILayout.BeginArea(new Rect((Screen.width - 460f) * 0.5f, 40f, 460f, 220f), GUI.skin.box);
                GUILayout.Label("Run Result");
                GUILayout.Space(6f);
                GUILayout.Label("No fight result available yet.");
                GUILayout.Label("Finish a fight first before using Result actions.");
                GUILayout.Space(12f);
                if (GUILayout.Button("Back Home"))
                {
                    manager.GoToHome();
                }

                GUILayout.EndArea();
                return;
            }

            var wasVictory = manager.HasLastFightResult && manager.LastFightWasVictory;
            var title = wasVictory ? "Victory" : "Defeat";
            var color = wasVictory ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.95f, 0.35f, 0.35f);

            var originalColor = GUI.color;
            GUI.color = color;
            GUILayout.BeginArea(new Rect((Screen.width - 460f) * 0.5f, 40f, 460f, 330f), GUI.skin.box);
            GUI.color = originalColor;

            GUILayout.Label("Run Result");
            GUILayout.Space(4f);
            GUILayout.Label("Outcome: " + title);
            GUILayout.Label("Node Reached: " + manager.CurrentRun.boardNodeIndex);
            GUILayout.Label("Wins / Losses: " + manager.CurrentRun.wins + " / " + manager.CurrentRun.losses);
            GUILayout.Label("Team Size: " + manager.CurrentRun.selectedChampionIds.Count + " / 3");

            GUILayout.Space(14f);
            GUILayout.Label("Actions");

            if (wasVictory)
            {
                if (GUILayout.Button("Next Node (continue run)"))
                {
                    manager.NextBoardNode();
                }

                if (GUILayout.Button("End Run (+" + victoryEndRunReward + " points)"))
                {
                    manager.EndRun(victoryEndRunReward);
                }
            }
            else
            {
                if (GUILayout.Button("End Run (+" + defeatEndRunReward + " points)"))
                {
                    manager.EndRun(defeatEndRunReward);
                }
            }

            if (GUILayout.Button("Back Home (no reward)"))
            {
                manager.GoToHome();
            }

            GUILayout.EndArea();
        }
    }
}
