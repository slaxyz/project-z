using ProjectZ.Meta;
using ProjectZ.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Core
{
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        public RunData CurrentRun { get; private set; }
        public MetaData MetaProgression { get; private set; }
        public GameFlowState CurrentState { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject("GameFlowManager");
            Instance = go.AddComponent<GameFlowManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            MetaProgression = MetaSaveService.Load();
            CurrentRun = new RunData();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CurrentState = StateFromScene(scene.name);
        }

        public void GoToHome()
        {
            SaveMeta();
            LoadScene(GameScenes.Home);
        }

        public void GoToCollection()
        {
            LoadScene(GameScenes.Collection);
        }

        public void GoToTeamSelect()
        {
            LoadScene(GameScenes.TeamSelect);
        }

        public void StartRun()
        {
            CurrentRun.Reset();
            CurrentRun.isActive = true;
            LoadScene(GameScenes.Loading);
        }

        public void OpenBoard()
        {
            LoadScene(GameScenes.Board);
        }

        public void StartFight()
        {
            LoadScene(GameScenes.Fight);
        }

        public void ShowResult(bool victory)
        {
            if (victory)
            {
                CurrentRun.wins++;
            }
            else
            {
                CurrentRun.losses++;
            }

            LoadScene(GameScenes.Result);
        }

        public void NextBoardNode()
        {
            CurrentRun.boardNodeIndex++;
            LoadScene(GameScenes.Board);
        }

        public void EndRun(int pointsEarned)
        {
            MetaProgression.progressionPoints += pointsEarned;
            SaveMeta();

            CurrentRun.Reset();
            LoadScene(GameScenes.Home);
        }

        public void SaveMeta()
        {
            MetaSaveService.Save(MetaProgression);
        }

        private static void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        private static GameFlowState StateFromScene(string sceneName)
        {
            switch (sceneName)
            {
                case GameScenes.Home:
                    return GameFlowState.Home;
                case GameScenes.Collection:
                    return GameFlowState.Collection;
                case GameScenes.TeamSelect:
                    return GameFlowState.TeamSelect;
                case GameScenes.Loading:
                    return GameFlowState.Loading;
                case GameScenes.Board:
                    return GameFlowState.Board;
                case GameScenes.Fight:
                    return GameFlowState.Fight;
                case GameScenes.Result:
                    return GameFlowState.Result;
                default:
                    return GameFlowState.Home;
            }
        }
    }
}
