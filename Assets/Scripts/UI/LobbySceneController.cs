using ProjectZ.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.UI
{
    public class LobbySceneController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnHome()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.Home)
            {
                return;
            }

            if (FindFirstObjectByType<LobbySceneController>() != null)
            {
                return;
            }

            var go = new GameObject("LobbySceneController");
            go.AddComponent<LobbySceneController>();
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
                const float panelWidth = 520f;
                const float panelHeight = 300f;
                var panelX = safeArea.x + (safeArea.width - panelWidth) * 0.5f;
                var panelY = safeArea.y + (safeArea.height - panelHeight) * 0.5f;

                GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), GUI.skin.box);
                GUILayout.Label("Project Z - Lobby");
                GUILayout.Space(8f);
                GUILayout.Label("Coins: " + manager.GetPlayerCoins());
                GUILayout.Space(16f);

                if (GUILayout.Button("Collection", GUILayout.Height(64f)))
                {
                    manager.GoToCollection();
                }

                GUILayout.Space(8f);

                if (GUILayout.Button("Play", GUILayout.Height(64f)))
                {
                    manager.GoToTeamSelect();
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
