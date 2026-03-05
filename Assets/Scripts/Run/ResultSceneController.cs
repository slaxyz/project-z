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

            var previousMatrix = GUI.matrix;
            var scale = DebugGuiScale.GetScale();
            var safeArea = DebugGuiScale.GetSafeArea(scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            try
            {
                if (!manager.HasLastFightResult)
                {
                    var x = safeArea.x + (safeArea.width - 460f) * 0.5f;
                    var y = safeArea.y + 40f;
                    GUILayout.BeginArea(new Rect(x, y, 460f, 220f), GUI.skin.box);
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

                var wasVictory = manager.LastFightWasVictory;
                var title = wasVictory ? "Victory" : "Defeat";
                var panelX = safeArea.x + (safeArea.width - 500f) * 0.5f;
                var panelY = safeArea.y + 40f;
                GUILayout.BeginArea(new Rect(panelX, panelY, 500f, 420f), GUI.skin.box);

                GUILayout.Label("Run Result");
                GUILayout.Space(4f);
                GUILayout.Label("Outcome: " + title);
                GUILayout.Label("Node Reached: " + manager.CurrentRun.boardNodeIndex);
                GUILayout.Label("Wins / Losses: " + manager.CurrentRun.wins + " / " + manager.CurrentRun.losses);
                GUILayout.Label("Team Size: " + manager.CurrentRun.selectedChampionIds.Count + " / 3");

                GUILayout.Space(14f);
                GUILayout.Label("Actions");

                if (manager.CanGoToNextBoardNode())
                {
                    if (GUILayout.Button("Next Node (continue run)"))
                    {
                        manager.NextBoardNode();
                    }
                }

                if (manager.CanEndRun())
                {
                    var reward = wasVictory ? victoryEndRunReward : defeatEndRunReward;
                    if (GUILayout.Button("End Run (+" + reward + " points)"))
                    {
                        manager.EndRun(reward);
                    }
                }
                else
                {
                    GUILayout.Label("End Run unavailable: result state is not valid yet.");
                }

                if (GUILayout.Button("Back Home (no reward)"))
                {
                    manager.GoToHome();
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
