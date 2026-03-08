using ProjectZ.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Run
{
    public class ResultSceneController : MonoBehaviour
    {
        private Vector2 _scroll;

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
                if (wasVictory && manager.LastFightWasBoss)
                {
                    title = "Boss Victory";
                }
                var panelX = safeArea.x + (safeArea.width - 560f) * 0.5f;
                var panelY = safeArea.y + 40f;
                GUILayout.BeginArea(new Rect(panelX, panelY, 560f, 620f), GUI.skin.box);
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(540f), GUILayout.Height(590f));

                GUILayout.Label("Run Result");
                GUILayout.Space(4f);
                GUILayout.Label("Outcome: " + title);
                GUILayout.Label("Node Reached: " + manager.CurrentRun.boardNodeIndex);
                GUILayout.Label("Zone / Tile: " + manager.GetCurrentZoneNumber() + " / " + (manager.GetActiveTileIndex() + 1));
                GUILayout.Label("Wins / Losses: " + manager.CurrentRun.wins + " / " + manager.CurrentRun.losses);
                GUILayout.Label("Team Size: " + manager.CurrentRun.selectedChampionIds.Count + " / 3");
                GUILayout.Label("Coins won this fight: +" + manager.LastFightCoinsReward);
                GUILayout.Label("Coins won this run: " + manager.CurrentRun.coinsGained);
                GUILayout.Label("Run Spells: " + manager.CurrentRun.deckCardIds.Count);

                GUILayout.Space(14f);
                GUILayout.Label("Actions");

                if (wasVictory && manager.HasPendingSpellRewardChoice())
                {
                    GUILayout.Label("Choose 1 spell reward");
                    foreach (var spellId in manager.GetPendingSpellRewardOffers())
                    {
                        var price = manager.GetSpellPrice(spellId);
                        if (GUILayout.Button("Take: " + spellId + " (value " + price + ")"))
                        {
                            manager.TrySelectSpellReward(spellId, out var _);
                        }
                    }

                    if (GUILayout.Button("Skip Reward"))
                    {
                        manager.SkipSpellReward();
                    }

                    GUILayout.Space(8f);
                }

                if (manager.IsWaitingSpellReplacementChoice() && !manager.IsPendingReplacementFromShop())
                {
                    if (string.IsNullOrWhiteSpace(manager.GetPendingReplacementChampionId()))
                    {
                        GUILayout.Label("Choose champion for: " + manager.GetPendingIncomingSpellId());
                        foreach (var championId in manager.GetSelectedChampionIdsForRun())
                        {
                            var championName = manager.GetChampionDisplayName(championId);
                            if (GUILayout.Button("Champion: " + championName))
                            {
                                manager.TrySelectReplacementChampion(championId, out var _);
                            }
                        }
                    }
                    else
                    {
                        var selectedChampion = manager.GetPendingReplacementChampionId();
                        GUILayout.Label("Replace on " + manager.GetChampionDisplayName(selectedChampion) + " with: " + manager.GetPendingIncomingSpellId());
                        foreach (var existing in manager.GetChampionSpellLoadout(selectedChampion))
                        {
                            if (GUILayout.Button("Replace: " + existing))
                            {
                                manager.TryReplaceRunSpell(existing, out var _);
                            }
                        }

                        if (GUILayout.Button("Choose Another Champion"))
                        {
                            manager.CancelPendingReplacementChampion();
                        }
                    }

                    GUILayout.Space(8f);
                }

                if (manager.CanGoToNextBoardNode() && !manager.HasPendingSpellRewardChoice() && !manager.IsWaitingSpellReplacementChoice())
                {
                    if (GUILayout.Button("Next Node (continue run)"))
                    {
                        manager.NextBoardNode();
                    }
                }

                if (manager.CanEndRun())
                {
                    if (GUILayout.Button("End Run (keep earned coins)"))
                    {
                        manager.EndRun(0);
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

                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }
    }
}
