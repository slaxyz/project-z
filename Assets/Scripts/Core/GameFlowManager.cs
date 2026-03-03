using ProjectZ.Meta;
using ProjectZ.Run;
using System.Collections.Generic;
using System.Linq;
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
        public string NextSceneAfterLoading { get; private set; } = GameScenes.Board;
        public float LoadingProgress { get; private set; }
        public bool HasLastFightResult { get; private set; }
        public bool LastFightWasVictory { get; private set; }

        [SerializeField] private float minLoadingDuration = 0.7f;
        private Coroutine _loadingCoroutine;

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

            if (CurrentState == GameFlowState.Loading)
            {
                if (_loadingCoroutine != null)
                {
                    StopCoroutine(_loadingCoroutine);
                }

                _loadingCoroutine = StartCoroutine(LoadNextSceneAfterLoading());
            }
            else
            {
                LoadingProgress = 0f;
            }
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
            if (!CurrentRun.HasValidTeam())
            {
                Debug.LogWarning("StartRun blocked: select exactly 3 unique champions first.");
                return;
            }

            var teamSnapshot = CurrentRun.selectedChampionIds.ToList();
            CurrentRun.Reset();
            CurrentRun.SetTeam(teamSnapshot);
            CurrentRun.isActive = true;
            HasLastFightResult = false;
            NextSceneAfterLoading = GameScenes.Board;
            LoadScene(GameScenes.Loading);
        }

        public bool ToggleChampionSelection(string championId)
        {
            if (CurrentRun.selectedChampionIds.Contains(championId))
            {
                CurrentRun.selectedChampionIds.Remove(championId);
                return true;
            }

            if (CurrentRun.selectedChampionIds.Count >= 3)
            {
                return false;
            }

            CurrentRun.selectedChampionIds.Add(championId);
            return true;
        }

        public bool IsChampionSelected(string championId)
        {
            return CurrentRun.selectedChampionIds.Contains(championId);
        }

        public int SelectedChampionCount()
        {
            return CurrentRun.selectedChampionIds.Count;
        }

        public IReadOnlyList<string> SelectedChampionIds()
        {
            return CurrentRun.selectedChampionIds;
        }

        public void OpenBoard()
        {
            LoadScene(GameScenes.Board);
        }

        public void StartFight()
        {
            if (!CanStartFight())
            {
                Debug.LogWarning("StartFight blocked: must be in Board during an active run.");
                return;
            }

            LoadScene(GameScenes.Fight);
        }

        public void ShowResult(bool victory)
        {
            if (!CanShowFightResult())
            {
                Debug.LogWarning("ShowResult blocked: results can only be shown from Fight during an active run.");
                return;
            }

            HasLastFightResult = true;
            LastFightWasVictory = victory;

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
            if (!CanGoToNextBoardNode())
            {
                Debug.LogWarning("NextBoardNode blocked: only available after a victory result.");
                return;
            }

            CurrentRun.boardNodeIndex++;
            HasLastFightResult = false;
            LoadScene(GameScenes.Board);
        }

        public void EndRun(int pointsEarned)
        {
            if (!CanEndRun())
            {
                Debug.LogWarning("EndRun blocked: only available on Result screen after a fight result.");
                return;
            }

            MetaProgression.progressionPoints += pointsEarned;
            SaveMeta();

            CurrentRun.Reset();
            HasLastFightResult = false;
            LastFightWasVictory = false;
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

        public bool CanStartFight()
        {
            return CurrentRun.isActive && CurrentState == GameFlowState.Board;
        }

        public bool CanShowFightResult()
        {
            return CurrentRun.isActive && CurrentState == GameFlowState.Fight;
        }

        public bool CanGoToNextBoardNode()
        {
            return CurrentRun.isActive && CurrentState == GameFlowState.Result && HasLastFightResult && LastFightWasVictory;
        }

        public bool CanEndRun()
        {
            return CurrentRun.isActive && CurrentState == GameFlowState.Result && HasLastFightResult;
        }

        private System.Collections.IEnumerator LoadNextSceneAfterLoading()
        {
            var op = SceneManager.LoadSceneAsync(NextSceneAfterLoading);
            if (op == null)
            {
                yield break;
            }

            op.allowSceneActivation = false;
            var elapsed = 0f;

            while (true)
            {
                elapsed += Time.deltaTime;
                LoadingProgress = Mathf.Clamp01(op.progress / 0.9f);

                var doneLoading = op.progress >= 0.9f;
                var minimumTimeReached = elapsed >= minLoadingDuration;

                if (doneLoading && minimumTimeReached)
                {
                    break;
                }

                yield return null;
            }

            LoadingProgress = 1f;
            op.allowSceneActivation = true;
            _loadingCoroutine = null;
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
