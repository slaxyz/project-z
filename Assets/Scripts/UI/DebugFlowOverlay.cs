using ProjectZ.Core;
using ProjectZ.Run;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectZ.UI
{
    public class DebugFlowOverlay : MonoBehaviour
    {
        private bool _isVisible = true;
        private readonly Rect _panelRect = new Rect(12f, 12f, 380f, 640f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            var existing = FindFirstObjectByType<DebugFlowOverlay>();
            if (existing != null)
            {
                return;
            }

            var go = new GameObject("DebugFlowOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugFlowOverlay>();
        }

        private void Update()
        {
            if (IsTogglePressed())
            {
                _isVisible = !_isVisible;
            }
        }

        private static bool IsTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F1);
#endif
        }

        private void OnGUI()
        {
            if (!_isVisible)
            {
                return;
            }

            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            GUILayout.BeginArea(_panelRect, GUI.skin.box);
            GUILayout.Label("Project Z - Debug Flow");
            GUILayout.Space(8f);

            GUILayout.Label("State: " + manager.CurrentState);
            GUILayout.Label("Run Active: " + manager.CurrentRun.isActive);
            GUILayout.Label("Board Node: " + manager.CurrentRun.boardNodeIndex);
            GUILayout.Label("Wins/Losses: " + manager.CurrentRun.wins + " / " + manager.CurrentRun.losses);
            GUILayout.Label("Meta Points: " + manager.MetaProgression.progressionPoints);

            GUILayout.Space(10f);
            GUILayout.Label("Main Navigation");
            if (GUILayout.Button("Home")) manager.GoToHome();
            if (GUILayout.Button("Collection")) manager.GoToCollection();
            if (GUILayout.Button("Team Select")) manager.GoToTeamSelect();
            if (GUILayout.Button("Open Board")) manager.OpenBoard();
            if (GUILayout.Button("Start Fight")) manager.StartFight();

            GUILayout.Space(10f);
            if (manager.CurrentState == GameFlowState.TeamSelect)
            {
                DrawTeamSelect(manager);
                GUILayout.Space(10f);
            }

            if (GUILayout.Button("Start Run (requires 3 selected)")) manager.StartRun();

            GUILayout.Space(10f);
            GUILayout.Label("Run Results");
            if (manager.CurrentState == GameFlowState.Fight)
            {
                GUILayout.Label("Fight in progress: use Fight UI actions.");
            }
            else if (manager.CurrentState == GameFlowState.Result)
            {
                GUILayout.Label("Result actions");
                if (manager.CanGoToNextBoardNode())
                {
                    if (GUILayout.Button("Next Node")) manager.NextBoardNode();
                }
                if (manager.CanEndRun())
                {
                    var reward = manager.LastFightWasVictory ? 15 : 5;
                    if (GUILayout.Button("End Run (+" + reward + ")")) manager.EndRun(reward);
                }
            }
            else
            {
                GUILayout.Label("Result actions available only in Fight.");
            }

            GUILayout.Space(10f);
            GUILayout.Label("F1: hide/show this debug panel");
            GUILayout.EndArea();
        }

        private static void DrawTeamSelect(GameFlowManager manager)
        {
            GUILayout.Label("Team Selection (pick 3 champions)");
            GUILayout.Label("Selected: " + manager.SelectedChampionCount() + "/3");

            foreach (var champion in ChampionCatalog.All)
            {
                var selected = manager.IsChampionSelected(champion.Id);
                var label = (selected ? "[x] " : "[ ] ") + champion.DisplayName + " - " + champion.Role;
                if (GUILayout.Button(label))
                {
                    manager.ToggleChampionSelection(champion.Id);
                }
            }
        }
    }
}
