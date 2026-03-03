using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Core
{
    public static class BootLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RedirectFromBootToHome()
        {
            if (SceneManager.GetActiveScene().name != "Boot")
            {
                return;
            }

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.GoToHome();
            }
            else
            {
                SceneManager.LoadScene(GameScenes.Home);
            }
        }
    }
}
