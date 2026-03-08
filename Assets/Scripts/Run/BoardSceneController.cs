using ProjectZ.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Run
{
    public class BoardSceneController : MonoBehaviour
    {
        private bool _showConfirmPopup;
        private string _shopFeedback;

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

        private void OnEnable()
        {
            _showConfirmPopup = false;
            _shopFeedback = string.Empty;
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
                var panelWidth = 360f;
                var panelX = safeArea.x + safeArea.width - panelWidth - 12f;
                var panelY = safeArea.y + 12f;

                GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, 280f), GUI.skin.box);
                GUILayout.Label("Board Controls");
                GUILayout.Label("Current Node: " + manager.CurrentRun.boardNodeIndex);
                GUILayout.Label("Zone: " + manager.GetCurrentZoneNumber());
                GUILayout.Label("Tile: " + (manager.GetActiveTileIndex() + 1) + "/" + manager.GetTilesForCurrentZone());
                GUILayout.Label("Tile Type: " + manager.GetCurrentTileType());
                GUILayout.Label("Run Team Size: " + manager.CurrentRun.selectedChampionIds.Count);
                GUILayout.Label("Coins this run: " + manager.CurrentRun.coinsGained);
                GUILayout.Space(8f);
                GUILayout.Label(manager.IsCurrentBoardTileValidated()
                    ? "Tile validated. You can start fight."
                    : "Select active tile then validate.");
                GUILayout.Space(8f);

                if (GUILayout.Button("Start Encounter (guard test)"))
                {
                    manager.StartFight();
                }

                GUILayout.EndArea();

                DrawBoardTiles(manager, safeArea);
                DrawConfirmPopup(manager, safeArea);
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private void DrawBoardTiles(GameFlowManager manager, Rect safeArea)
        {
            var padding = 24f;
            var boardX = safeArea.x + padding;
            var boardY = safeArea.y + safeArea.height * 0.35f;
            var boardWidth = Mathf.Max(300f, safeArea.width - padding * 2f);
            var boardHeight = 300f;
            var stepSpacing = 220f;
            var nodeWidth = 130f;
            var nodeHeight = 92f;
            var laneOffsetY = 66f;
            var activeIndex = manager.GetActiveTileIndex();
            var activeCenterX = activeIndex * stepSpacing;
            var desiredCenterX = boardWidth * 0.5f;
            var offsetX = desiredCenterX - activeCenterX;

            GUI.Box(new Rect(boardX, boardY, boardWidth, boardHeight), "Zone " + manager.GetCurrentZoneNumber() + " Board");
            foreach (var node in BuildNodes(manager.GetTilesForCurrentZone()))
            {
                var nodeCenterX = node.step * stepSpacing + offsetX;
                var nodeCenterY = boardHeight * 0.5f + node.lane * laneOffsetY;
                var rect = new Rect(
                    boardX + nodeCenterX - nodeWidth * 0.5f,
                    boardY + nodeCenterY - nodeHeight * 0.5f,
                    nodeWidth,
                    nodeHeight);

                var isDone = node.step < activeIndex;
                var isActive = IsNodeActive(node.step, node.lane, activeIndex, manager.CurrentRun.branchChoice);
                var canClick = isActive;
                var label = isDone ? "Done" : (isActive ? "Active" : "Locked");
                var previousEnabled = GUI.enabled;
                GUI.enabled = canClick;

                if (GUI.Button(rect, "Tile " + (node.step + 1) + "\n" + label + "\n" + node.type))
                {
                    OnNodeClicked(manager, node.step, node.lane);
                }

                GUI.enabled = previousEnabled;
            }
        }

        private void DrawConfirmPopup(GameFlowManager manager, Rect safeArea)
        {
            if (!_showConfirmPopup)
            {
                return;
            }

            var tileType = manager.GetCurrentTileType();
            var width = tileType == BoardTileType.Shop ? 620f : 420f;
            var height = tileType == BoardTileType.Shop ? 420f : 260f;
            var x = safeArea.x + (safeArea.width - width) * 0.5f;
            var y = safeArea.y + (safeArea.height - height) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.window);
            GUILayout.Label("Confirm Tile");
            GUILayout.Space(8f);
            GUILayout.Label("Tile " + (manager.GetActiveTileIndex() + 1) + " - " + tileType);
            GUILayout.Space(16f);

            if (tileType == BoardTileType.Fight || tileType == BoardTileType.Boss)
            {
                GUILayout.Label("Start fight now?");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Cancel", GUILayout.Height(36f)))
                {
                    _showConfirmPopup = false;
                }

                if (GUILayout.Button("Validate", GUILayout.Height(36f)))
                {
                    if (manager.TryValidateCurrentBoardTile(tileType))
                    {
                        _showConfirmPopup = false;
                        manager.StartFight();
                    }
                }
                GUILayout.EndHorizontal();
            }
            else if (tileType == BoardTileType.Event)
            {
                GUILayout.Label("Event reward: +12 run coins");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Cancel", GUILayout.Height(36f)))
                {
                    _showConfirmPopup = false;
                }

                if (GUILayout.Button("Resolve Event", GUILayout.Height(36f)))
                {
                    _showConfirmPopup = false;
                    manager.ResolveCurrentNonCombatTile(tileType);
                }
                GUILayout.EndHorizontal();
            }
            else if (tileType == BoardTileType.Shop)
            {
                GUILayout.Label("Shop - choose 1 spell");
                GUILayout.Label("Run coins: " + manager.CurrentRun.coinsGained);
                GUILayout.Space(6f);

                foreach (var spellId in manager.GetCurrentShopOffers())
                {
                    var price = manager.GetSpellPrice(spellId);
                    if (GUILayout.Button("Buy: " + spellId + " (" + price + ")", GUILayout.Height(34f)))
                    {
                        manager.TrySelectShopSpell(spellId, out _shopFeedback);
                    }
                }

                if (manager.IsWaitingSpellReplacementChoice() && manager.IsPendingReplacementFromShop())
                {
                    GUILayout.Space(10f);
                    if (string.IsNullOrWhiteSpace(manager.GetPendingReplacementChampionId()))
                    {
                        GUILayout.Label("Choose champion for: " + manager.GetPendingIncomingSpellId());
                        foreach (var championId in manager.GetSelectedChampionIdsForRun())
                        {
                            var championName = manager.GetChampionDisplayName(championId);
                            if (GUILayout.Button("Champion: " + championName, GUILayout.Height(32f)))
                            {
                                manager.TrySelectReplacementChampion(championId, out _shopFeedback);
                            }
                        }
                    }
                    else
                    {
                        var selectedChampion = manager.GetPendingReplacementChampionId();
                        GUILayout.Label("Replace on " + manager.GetChampionDisplayName(selectedChampion) + " with: " + manager.GetPendingIncomingSpellId());
                        foreach (var existing in manager.GetChampionSpellLoadout(selectedChampion))
                        {
                            if (GUILayout.Button("Replace " + existing, GUILayout.Height(32f)))
                            {
                                manager.TryReplaceRunSpell(existing, out _shopFeedback);
                                _showConfirmPopup = false;
                            }
                        }

                        if (GUILayout.Button("Choose Another Champion", GUILayout.Height(32f)))
                        {
                            manager.CancelPendingReplacementChampion();
                        }
                    }
                }

                GUILayout.Space(10f);
                if (!string.IsNullOrWhiteSpace(_shopFeedback))
                {
                    GUILayout.Label(_shopFeedback);
                }

                if (GUILayout.Button("Skip Shop", GUILayout.Height(34f)))
                {
                    _showConfirmPopup = false;
                    manager.SkipShop();
                }
            }
            else if (tileType == BoardTileType.BranchChoice)
            {
                GUILayout.Label("Choose your path");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Left Path", GUILayout.Height(40f)))
                {
                    manager.ChooseBranch(0);
                    _showConfirmPopup = false;
                }

                if (GUILayout.Button("Right Path", GUILayout.Height(40f)))
                {
                    manager.ChooseBranch(1);
                    _showConfirmPopup = false;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        private void OnNodeClicked(GameFlowManager manager, int step, int lane)
        {
            if (step != manager.GetActiveTileIndex())
            {
                return;
            }

            if (step == 2 && manager.CurrentRun.branchChoice < 0)
            {
                manager.ChooseBranch(lane > 0 ? 1 : 0);
                _showConfirmPopup = true;
                return;
            }

            _showConfirmPopup = true;
        }

        private static bool IsNodeActive(int step, int lane, int activeStep, int branchChoice)
        {
            if (step != activeStep)
            {
                return false;
            }

            if (step == 2 && branchChoice < 0)
            {
                return true;
            }

            if ((step == 2 || step == 3) && branchChoice >= 0)
            {
                var chosenLane = branchChoice == 0 ? -1 : 1;
                return lane == chosenLane;
            }

            return lane == 0;
        }

        private static List<(int step, int lane, BoardTileType type)> BuildNodes(int tileCount)
        {
            var nodes = new List<(int step, int lane, BoardTileType type)>();
            for (var step = 0; step < tileCount; step++)
            {
                if (step == 2)
                {
                    nodes.Add((step, -1, BoardTileType.Fight));
                    nodes.Add((step, 1, BoardTileType.Event));
                    continue;
                }

                if (step == 3)
                {
                    nodes.Add((step, -1, BoardTileType.Event));
                    nodes.Add((step, 1, BoardTileType.Fight));
                    continue;
                }

                BoardTileType type;
                if (step == 6)
                {
                    type = BoardTileType.Shop;
                }
                else if (step == tileCount - 1)
                {
                    type = BoardTileType.Boss;
                }
                else
                {
                    type = BoardTileType.Fight;
                }

                nodes.Add((step, 0, type));
            }

            return nodes;
        }
    }
}
