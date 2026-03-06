using ProjectZ.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Run
{
    public class BoardSceneController : MonoBehaviour
    {
        private bool _showConfirmPopup;

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
            var width = 760f;
            var height = 180f;
            var x = safeArea.x + (safeArea.width - width) * 0.5f;
            var y = safeArea.y + safeArea.height * 0.45f;
            var tileCount = manager.GetTilesForCurrentZone();
            var activeTile = manager.GetActiveTileIndex();

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
            GUILayout.Label("Zone " + manager.GetCurrentZoneNumber() + " Board");
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();

            for (var i = 0; i < tileCount; i++)
            {
                var isPast = i < activeTile;
                var isActive = i == activeTile;
                var isFuture = i > activeTile;
                var previousState = GUI.enabled;
                GUI.enabled = isActive;

                var label = isPast ? "Done" : (isActive ? "Active" : "Locked");
                if (GUILayout.Button("Tile " + (i + 1) + "\n" + label, GUILayout.Width(120f), GUILayout.Height(90f)))
                {
                    _showConfirmPopup = true;
                }

                GUI.enabled = previousState;

                if (isFuture)
                {
                    continue;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawConfirmPopup(GameFlowManager manager, Rect safeArea)
        {
            if (!_showConfirmPopup)
            {
                return;
            }

            var width = 420f;
            var height = 220f;
            var x = safeArea.x + (safeArea.width - width) * 0.5f;
            var y = safeArea.y + (safeArea.height - height) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.window);
            GUILayout.Label("Confirm Encounter");
            GUILayout.Space(8f);
            GUILayout.Label("Start fight on active tile " + (manager.GetActiveTileIndex() + 1) + "?");
            GUILayout.Space(16f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(36f)))
            {
                _showConfirmPopup = false;
            }

            if (GUILayout.Button("Validate", GUILayout.Height(36f)))
            {
                if (manager.TryValidateCurrentBoardTile())
                {
                    _showConfirmPopup = false;
                    manager.StartFight();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
