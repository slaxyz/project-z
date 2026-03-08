using ProjectZ.Meta;
using ProjectZ.Run;
using ProjectZ.Combat;
using ProjectZ.UI;
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
        public bool LastFightWasBoss { get; private set; }
        public int LastFightCoinsReward { get; private set; }

        [SerializeField] private float minLoadingDuration = 0.7f;
        [SerializeField] private int fallbackTilesPerZone = 4;
        [SerializeField] private int fallbackZoneCount = 2;
        [SerializeField] private int fallbackVictoryCoinReward = 12;
        private Coroutine _loadingCoroutine;
        private bool _isBoardTileValidated;
        private bool _isNextFightBoss;
        private bool _pendingAdvanceAfterBossCelebration;
        private RunLoopConfigAsset _runLoopConfig;
        private readonly List<string> _pendingSpellRewardOffers = new List<string>();
        private readonly List<string> _currentShopOffers = new List<string>();
        private const int MaxRunSpells = 4;
        private string _pendingReplacementIncomingSpellId;
        private string _pendingReplacementTargetChampionId;
        private bool _pendingReplacementFromShop;
        private int _pendingReplacementShopCost;
        private Dictionary<string, CombatSpellAsset> _spellIndexCache;
        private readonly System.Random _rewardRng = new System.Random();

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
            MetaProgression.EnsureCollections();
            EnsureDefaultUnlockedChampions();
            CurrentRun = new RunData();
            LoadRunLoopConfig();
            RestoreRunProgress();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CurrentState = StateFromScene(scene.name);
            EnsureSceneControllersForCurrentState();

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

        private void EnsureSceneControllersForCurrentState()
        {
            switch (CurrentState)
            {
                case GameFlowState.Collection:
                    EnsureController<CollectionSceneController>("CollectionSceneController");
                    break;
                case GameFlowState.Home:
                    EnsureController<LobbySceneController>("LobbySceneController");
                    break;
                case GameFlowState.TeamSelect:
                    EnsureController<TeamSelectSceneController>("TeamSelectSceneController");
                    break;
                case GameFlowState.Loading:
                    EnsureController<LoadingSceneController>("LoadingSceneController");
                    break;
                case GameFlowState.ZoneAnimation:
                    EnsureController<ZoneAnimationSceneController>("ZoneAnimationSceneController");
                    break;
                case GameFlowState.BossCelebration:
                    EnsureController<BossCelebrationSceneController>("BossCelebrationSceneController");
                    break;
                case GameFlowState.Board:
                    EnsureController<BoardSceneController>("BoardSceneController");
                    break;
                case GameFlowState.Fight:
                    EnsureController<FightMockController>("FightMockController");
                    break;
                case GameFlowState.Result:
                    EnsureController<ResultSceneController>("ResultSceneController");
                    break;
            }
        }

        private static void EnsureController<T>(string objectName) where T : Component
        {
            if (FindFirstObjectByType<T>() != null)
            {
                return;
            }

            var go = new GameObject(objectName);
            go.AddComponent<T>();
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

        public void OpenPlayEntry()
        {
            if (CurrentRun != null && CurrentRun.isActive && CurrentRun.HasValidTeam())
            {
                Debug.Log("Active run detected from Home. Going directly to run.");
                if (HasLastFightResult && LastFightWasVictory && (HasPendingSpellRewardChoice() || (IsWaitingSpellReplacementChoice() && !IsPendingReplacementFromShop())))
                {
                    LoadScene(GameScenes.Result);
                    return;
                }

                EnterBoardViaZoneAnimation();
                return;
            }

            LoadScene(GameScenes.TeamSelect);
        }

        public void BootToLobby()
        {
            NextSceneAfterLoading = GameScenes.Home;
            LoadScene(GameScenes.Loading);
        }

        public void StartRun()
        {
            if (CurrentRun.isActive && CurrentRun.HasValidTeam())
            {
                Debug.Log("Run already active. Continuing from saved progression.");
                EnterBoardViaZoneAnimation();
                return;
            }

            if (!CurrentRun.HasValidTeam())
            {
                Debug.LogWarning("StartRun blocked: select exactly 3 unique champions first.");
                return;
            }

            var teamSnapshot = CurrentRun.selectedChampionIds.ToList();
            CurrentRun.Reset();
            CurrentRun.SetTeam(teamSnapshot);
            CurrentRun.branchChoice = -1;
            EnsureChampionSpellLoadoutsInitialized();
            CurrentRun.isActive = true;
            HasLastFightResult = false;
            LastFightWasVictory = false;
            LastFightWasBoss = false;
            LastFightCoinsReward = 0;
            _isBoardTileValidated = false;
            _isNextFightBoss = false;
            _pendingAdvanceAfterBossCelebration = false;
            _pendingSpellRewardOffers.Clear();
            _currentShopOffers.Clear();
            ClearPendingReplacement();
            PersistRunProgress();
            Debug.Log("Run started: Zone 1, Tile 1.");
            EnterBoardViaZoneAnimation();
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

        public IReadOnlyList<ChampionDefinitionAsset> GetChampionCatalog()
        {
            return ChampionCatalog.AllAssets;
        }

        public bool IsChampionUnlocked(string championId)
        {
            return MetaProgression.IsChampionUnlocked(championId);
        }

        public int GetPlayerCoins()
        {
            return MetaProgression.progressionPoints;
        }

        public void DebugResetRunAndProgression()
        {
            CurrentRun.Reset();
            HasLastFightResult = false;
            LastFightWasVictory = false;
            LastFightWasBoss = false;
            LastFightCoinsReward = 0;
            _isBoardTileValidated = false;
            _isNextFightBoss = false;
            _pendingAdvanceAfterBossCelebration = false;
            _pendingSpellRewardOffers.Clear();
            _currentShopOffers.Clear();
            ClearPendingReplacement();
            MetaProgression.ClearRunProgress();

            MetaProgression.EnsureCollections();
            MetaProgression.progressionPoints = 0;
            MetaProgression.unlockedChampionIds.Clear();

            SaveMeta();
        }

        public void DebugAddCoins(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            MetaProgression.progressionPoints += amount;
            SaveMeta();
        }

        public string GetDefaultSelectedChampionIdForCollection()
        {
            var catalog = ChampionCatalog.AllAssets;
            if (catalog == null || catalog.Count == 0)
            {
                return string.Empty;
            }

            var first = catalog[0];
            if (first != null && !string.IsNullOrWhiteSpace(first.Id) && IsChampionUnlocked(first.Id))
            {
                return first.Id;
            }

            foreach (var champion in catalog)
            {
                if (champion == null || string.IsNullOrWhiteSpace(champion.Id))
                {
                    continue;
                }

                if (IsChampionUnlocked(champion.Id))
                {
                    return champion.Id;
                }
            }

            return first != null ? first.Id : string.Empty;
        }

        public bool TryUnlockChampion(string championId, out string reason)
        {
            if (string.IsNullOrWhiteSpace(championId))
            {
                reason = "Invalid champion id.";
                return false;
            }

            var champion = ChampionCatalog.FindById(championId);
            if (champion == null)
            {
                reason = "Champion not found.";
                return false;
            }

            if (MetaProgression.IsChampionUnlocked(championId))
            {
                reason = "Already unlocked.";
                return false;
            }

            var cost = Mathf.Max(0, champion.UnlockCost);
            if (MetaProgression.progressionPoints < cost)
            {
                reason = "Not enough coins.";
                return false;
            }

            MetaProgression.progressionPoints -= cost;
            MetaProgression.UnlockChampion(championId);
            SaveMeta();
            reason = "Champion unlocked.";
            return true;
        }

        public IReadOnlyList<string> SelectedChampionIds()
        {
            return CurrentRun.selectedChampionIds;
        }

        public int GetCurrentZoneNumber()
        {
            return CurrentRun.zoneIndex + 1;
        }

        public int GetActiveTileIndex()
        {
            return CurrentRun.tileIndex;
        }

        public int GetTilesForCurrentZone()
        {
            return 8;
        }

        public BoardTileType GetCurrentTileType()
        {
            var index = Mathf.Clamp(CurrentRun.tileIndex, 0, 7);
            if (index == 0 || index == 1)
            {
                return BoardTileType.Fight;
            }

            if (index == 2)
            {
                if (CurrentRun.branchChoice < 0)
                {
                    return BoardTileType.BranchChoice;
                }

                return CurrentRun.branchChoice == 0 ? BoardTileType.Fight : BoardTileType.Event;
            }

            if (index == 3)
            {
                return CurrentRun.branchChoice == 0 ? BoardTileType.Event : BoardTileType.Fight;
            }

            if (index == 4 || index == 5)
            {
                return BoardTileType.Fight;
            }

            if (index == 6)
            {
                return BoardTileType.Shop;
            }

            return BoardTileType.Boss;
        }

        public bool IsWaitingBranchChoice()
        {
            return GetCurrentTileType() == BoardTileType.BranchChoice;
        }

        public void ChooseBranch(int branch)
        {
            if (!IsWaitingBranchChoice())
            {
                return;
            }

            CurrentRun.branchChoice = branch > 0 ? 1 : 0;
            PersistRunProgress();
        }

        public bool IsCurrentBoardTileValidated()
        {
            return _isBoardTileValidated;
        }

        public bool IsNextFightBoss()
        {
            return _isNextFightBoss;
        }

        public bool TryValidateCurrentBoardTile(BoardTileType tileType)
        {
            if (!CurrentRun.isActive || CurrentState != GameFlowState.Board)
            {
                Debug.LogWarning("Validate tile blocked: must be in Board during an active run.");
                return false;
            }

            if (tileType == BoardTileType.BranchChoice)
            {
                Debug.LogWarning("Choose a branch first.");
                return false;
            }

            _isBoardTileValidated = true;
            _isNextFightBoss = tileType == BoardTileType.Boss;
            Debug.Log("Board tile validated. Fight can start.");
            return true;
        }

        public void OpenBoard()
        {
            LoadScene(GameScenes.Board);
        }

        public bool ResolveCurrentNonCombatTile(BoardTileType tileType)
        {
            if (!CurrentRun.isActive || CurrentState != GameFlowState.Board)
            {
                Debug.LogWarning("Resolve non-combat tile blocked: must be on Board during active run.");
                return false;
            }

            if (tileType == BoardTileType.Event)
            {
                CurrentRun.coinsGained += 12;
                Debug.Log("Event resolved: +12 run coins.");
                _currentShopOffers.Clear();
                ClearPendingReplacement();
                PersistRunProgress();
                AdvanceBoardProgress(false);
            }
            else if (tileType == BoardTileType.Shop)
            {
                EnsureCurrentShopOffers();
                Debug.Log("Entered shop.");
                PersistRunProgress();
            }
            else
            {
                return false;
            }
            return true;
        }

        public IReadOnlyList<string> GetCurrentShopOffers()
        {
            EnsureCurrentShopOffers();
            return _currentShopOffers;
        }

        public int GetSpellPrice(string spellId)
        {
            var spell = FindSpellById(spellId);
            if (spell == null)
            {
                return 20;
            }

            return Mathf.Clamp(10 + spell.Value * 2 + spell.CostEntriesCount * 4, 14, 65);
        }

        public void SkipShop()
        {
            _currentShopOffers.Clear();
            ClearPendingReplacement();
            PersistRunProgress();
            AdvanceBoardProgress(false);
        }

        public bool TrySelectShopSpell(string spellId, out string message)
        {
            if (!CurrentRun.isActive || CurrentState != GameFlowState.Board || GetCurrentTileType() != BoardTileType.Shop)
            {
                message = "Shop unavailable.";
                return false;
            }

            EnsureCurrentShopOffers();
            if (string.IsNullOrWhiteSpace(spellId) || !_currentShopOffers.Contains(spellId))
            {
                message = "Spell not in shop.";
                return false;
            }

            var cost = GetSpellPrice(spellId);
            if (CurrentRun.coinsGained < cost)
            {
                message = "Not enough run coins.";
                return false;
            }

            _pendingReplacementIncomingSpellId = spellId;
            _pendingReplacementTargetChampionId = null;
            _pendingReplacementFromShop = true;
            _pendingReplacementShopCost = cost;
            message = "Choose a champion.";
            PersistRunProgress();
            return true;
        }

        public void StartFight()
        {
            if (!CanStartFight())
            {
                Debug.LogWarning("StartFight blocked: validate the active board tile first.");
                return;
            }

            Debug.Log("Fight started.");
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
            LastFightWasBoss = _isNextFightBoss;
            LastFightCoinsReward = 0;

            if (victory)
            {
                CurrentRun.wins++;
                var rewardFromConfig = _runLoopConfig != null
                    ? _runLoopConfig.VictoryCoinReward
                    : Mathf.Max(0, fallbackVictoryCoinReward);
                LastFightCoinsReward = rewardFromConfig;
                if (LastFightWasBoss)
                {
                    LastFightCoinsReward += 6;
                }
                CurrentRun.coinsGained += LastFightCoinsReward;
                BuildSpellRewardOffers();
                Debug.Log("Fight won: +" + LastFightCoinsReward + " coins.");
            }
            else
            {
                CurrentRun.losses++;
                LastFightWasBoss = false;
                _pendingSpellRewardOffers.Clear();
                _currentShopOffers.Clear();
                ClearPendingReplacement();
                Debug.Log("Fight lost: no coins.");
            }

            PersistRunProgress();
            LoadScene(GameScenes.Result);
        }

        public void NextBoardNode()
        {
            if (!CanGoToNextBoardNode())
            {
                Debug.LogWarning("NextBoardNode blocked: only available after a victory result.");
                return;
            }
            if (HasPendingSpellRewardChoice())
            {
                Debug.LogWarning("Choose a spell reward (or skip) before moving to next node.");
                return;
            }
            if (IsWaitingSpellReplacementChoice())
            {
                Debug.LogWarning("Choose a spell to replace first.");
                return;
            }

            if (LastFightWasBoss)
            {
                _pendingAdvanceAfterBossCelebration = true;
                LoadScene(GameScenes.BossCelebration);
                return;
            }

            AdvanceBoardProgress(true);
        }

        public void EndRun(int pointsEarned)
        {
            if (!CanEndRun())
            {
                Debug.LogWarning("EndRun blocked: only available on Result screen after a fight result.");
                return;
            }

            MetaProgression.SetLastRunSummary(
                Mathf.Max(0, CurrentRun.coinsGained),
                Mathf.Max(0, CurrentRun.wins),
                Mathf.Max(0, CurrentRun.losses),
                Mathf.Max(0, CurrentRun.zoneIndex),
                Mathf.Max(0, CurrentRun.tileIndex));

            var totalReward = Mathf.Max(0, pointsEarned) + Mathf.Max(0, CurrentRun.coinsGained);
            MetaProgression.progressionPoints += totalReward;
            SaveMeta();

            CurrentRun.Reset();
            HasLastFightResult = false;
            LastFightWasVictory = false;
            LastFightWasBoss = false;
            LastFightCoinsReward = 0;
            _isBoardTileValidated = false;
            _isNextFightBoss = false;
            _pendingAdvanceAfterBossCelebration = false;
            _pendingSpellRewardOffers.Clear();
            _currentShopOffers.Clear();
            ClearPendingReplacement();
            MetaProgression.ClearRunProgress();
            SaveMeta();
            Debug.Log("Run ended. Reward added: " + totalReward + " coins.");
            LoadScene(GameScenes.Home);
        }

        public IReadOnlyList<string> GetPendingSpellRewardOffers()
        {
            return _pendingSpellRewardOffers;
        }

        public bool HasPendingSpellRewardChoice()
        {
            return _pendingSpellRewardOffers.Count > 0;
        }

        public IReadOnlyList<string> GetRunDeckSpellIds()
        {
            return CurrentRun.deckCardIds;
        }

        public IReadOnlyList<string> GetSelectedChampionIdsForRun()
        {
            return CurrentRun.selectedChampionIds;
        }

        public IReadOnlyList<string> GetChampionSpellLoadout(string championId)
        {
            return CurrentRun.GetChampionSpells(championId);
        }

        public string GetChampionDisplayName(string championId)
        {
            if (string.IsNullOrWhiteSpace(championId))
            {
                return "Unknown";
            }

            var champion = ChampionCatalog.FindById(championId);
            if (champion == null || string.IsNullOrWhiteSpace(champion.DisplayName))
            {
                return championId;
            }

            return champion.DisplayName;
        }

        public bool IsWaitingSpellReplacementChoice()
        {
            return !string.IsNullOrWhiteSpace(_pendingReplacementIncomingSpellId);
        }

        public string GetPendingIncomingSpellId()
        {
            return _pendingReplacementIncomingSpellId ?? string.Empty;
        }

        public bool IsPendingReplacementFromShop()
        {
            return _pendingReplacementFromShop;
        }

        public string GetPendingReplacementChampionId()
        {
            return _pendingReplacementTargetChampionId ?? string.Empty;
        }

        public void CancelPendingReplacementChampion()
        {
            _pendingReplacementTargetChampionId = null;
            PersistRunProgress();
        }

        public bool HasLastRunSummary()
        {
            return MetaProgression.hasLastRunSummary;
        }

        public string GetLastRunSummaryLabel()
        {
            if (!MetaProgression.hasLastRunSummary)
            {
                return "No run summary yet.";
            }

            return "Last Run: +" + MetaProgression.lastRunCoinsEarned
                + " coins | W/L " + MetaProgression.lastRunWins + "/" + MetaProgression.lastRunLosses
                + " | Zone " + (MetaProgression.lastRunZoneReached + 1)
                + " Tile " + (MetaProgression.lastRunLastTileIndex + 1);
        }

        public bool TrySelectReplacementChampion(string championId, out string message)
        {
            if (!IsWaitingSpellReplacementChoice())
            {
                message = "No spell selected.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(championId) || !CurrentRun.selectedChampionIds.Contains(championId))
            {
                message = "Invalid champion.";
                return false;
            }

            _pendingReplacementTargetChampionId = championId;
            PersistRunProgress();
            message = "Choose spell to replace.";
            return true;
        }

        public bool TrySelectSpellReward(string spellId, out string message)
        {
            if (string.IsNullOrWhiteSpace(spellId) || !_pendingSpellRewardOffers.Contains(spellId))
            {
                message = "Invalid spell reward.";
                return false;
            }

            _pendingReplacementIncomingSpellId = spellId;
            _pendingReplacementTargetChampionId = null;
            _pendingReplacementFromShop = false;
            _pendingReplacementShopCost = 0;
            _pendingSpellRewardOffers.Clear();
            message = "Choose a champion.";
            PersistRunProgress();
            return true;
        }

        public void SkipSpellReward()
        {
            _pendingSpellRewardOffers.Clear();
            ClearPendingReplacement();
            PersistRunProgress();
        }

        public bool TryReplaceRunSpell(string spellToReplaceId, out string message)
        {
            if (!IsWaitingSpellReplacementChoice())
            {
                message = "No replacement pending.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(spellToReplaceId))
            {
                message = "Spell to replace not found.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_pendingReplacementTargetChampionId))
            {
                message = "Choose a champion first.";
                return false;
            }

            var championSpells = CurrentRun.GetChampionSpells(_pendingReplacementTargetChampionId);
            if (!championSpells.Contains(spellToReplaceId))
            {
                message = "Selected champion does not own this spell.";
                return false;
            }

            if (_pendingReplacementFromShop)
            {
                if (CurrentRun.coinsGained < _pendingReplacementShopCost)
                {
                    message = "Not enough run coins.";
                    ClearPendingReplacement();
                    PersistRunProgress();
                    return false;
                }

                CurrentRun.coinsGained -= _pendingReplacementShopCost;
            }

            championSpells.Remove(spellToReplaceId);
            championSpells.Add(_pendingReplacementIncomingSpellId);
            RebuildGlobalDeckFromChampionLoadouts();

            if (_pendingReplacementFromShop)
            {
                _currentShopOffers.Clear();
                ClearPendingReplacement();
                PersistRunProgress();
                message = "Shop spell bought and replaced.";
                AdvanceBoardProgress(false);
                return true;
            }

            _pendingSpellRewardOffers.Clear();
            ClearPendingReplacement();
            PersistRunProgress();
            message = "Reward spell replaced.";
            return true;
        }

        public void ContinueAfterBossCelebration()
        {
            if (!_pendingAdvanceAfterBossCelebration)
            {
                return;
            }

            _pendingAdvanceAfterBossCelebration = false;
            LastFightWasBoss = false;
            AdvanceBoardProgress(true);
        }

        public void SaveMeta()
        {
            MetaSaveService.Save(MetaProgression);
        }

        public void EnterBoardViaZoneAnimation()
        {
            LoadScene(GameScenes.ZoneAnimation);
        }

        private void EnsureDefaultUnlockedChampions()
        {
            var changed = MetaProgression.EnsureDefaultUnlockedChampions(ChampionCatalog.GetDefaultUnlockedChampionIds(3));
            if (changed)
            {
                SaveMeta();
            }
        }

        private void LoadRunLoopConfig()
        {
            _runLoopConfig = Resources.Load<RunLoopConfigAsset>("Run/RunLoopConfig");
            if (_runLoopConfig == null)
            {
                Debug.LogWarning("RunLoopConfig missing at Resources/Run/RunLoopConfig. Using fallback values.");
                return;
            }

            Debug.Log("RunLoopConfig loaded.");
        }

        private void BuildSpellRewardOffers()
        {
            _pendingSpellRewardOffers.Clear();
            ClearPendingReplacement();
            var candidates = BuildAvailableRunSpellIds();
            if (candidates.Count == 0)
            {
                return;
            }

            var picks = Mathf.Min(3, candidates.Count);
            for (var i = 0; i < picks; i++)
            {
                var index = _rewardRng.Next(0, candidates.Count);
                _pendingSpellRewardOffers.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
        }

        private void EnsureCurrentShopOffers()
        {
            if (_currentShopOffers.Count > 0)
            {
                return;
            }

            var candidates = BuildAvailableRunSpellIds();
            if (candidates.Count == 0)
            {
                return;
            }

            var picks = Mathf.Min(3, candidates.Count);
            for (var i = 0; i < picks; i++)
            {
                var index = _rewardRng.Next(0, candidates.Count);
                _currentShopOffers.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
        }

        private List<string> BuildAvailableRunSpellIds()
        {
            var index = GetSpellIndex();
            return index.Keys
                .Where(id => !CurrentRun.deckCardIds.Contains(id))
                .OrderBy(id => id)
                .ToList();
        }

        private void EnsureChampionSpellLoadoutsInitialized()
        {
            var defaults = GetSpellIndex().Keys.OrderBy(id => id).Take(MaxRunSpells).ToList();
            foreach (var championId in CurrentRun.selectedChampionIds)
            {
                var loadout = CurrentRun.GetChampionSpells(championId);
                if (loadout.Count > 0)
                {
                    continue;
                }

                loadout.AddRange(defaults);
            }

            RebuildGlobalDeckFromChampionLoadouts();
        }

        private void RebuildGlobalDeckFromChampionLoadouts()
        {
            CurrentRun.deckCardIds.Clear();
            foreach (var loadout in CurrentRun.championSpellLoadouts)
            {
                if (loadout == null || loadout.spellIds == null)
                {
                    continue;
                }

                foreach (var spellId in loadout.spellIds)
                {
                    if (string.IsNullOrWhiteSpace(spellId) || CurrentRun.deckCardIds.Contains(spellId))
                    {
                        continue;
                    }

                    CurrentRun.deckCardIds.Add(spellId);
                }
            }
        }

        private Dictionary<string, CombatSpellAsset> GetSpellIndex()
        {
            if (_spellIndexCache != null)
            {
                return _spellIndexCache;
            }

            var library = Resources.Load<CombatSpellLibraryAsset>("Combat/SpellLibrary");
            _spellIndexCache = library != null
                ? library.BuildIndexById()
                : new Dictionary<string, CombatSpellAsset>();
            return _spellIndexCache;
        }

        private CombatSpellAsset FindSpellById(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return null;
            }

            var index = GetSpellIndex();
            return index.TryGetValue(spellId, out var spell) ? spell : null;
        }

        private void AddSpellToRunDeck(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return;
            }

            if (CurrentRun.deckCardIds.Contains(spellId))
            {
                return;
            }

            CurrentRun.deckCardIds.Add(spellId);
            Debug.Log("Run spell added: " + spellId);
        }

        private void ClearPendingReplacement()
        {
            _pendingReplacementIncomingSpellId = null;
            _pendingReplacementTargetChampionId = null;
            _pendingReplacementFromShop = false;
            _pendingReplacementShopCost = 0;
        }

        private void AdvanceBoardProgress(bool fromCombatVictory)
        {
            if (fromCombatVictory)
            {
                CurrentRun.boardNodeIndex++;
            }

            CurrentRun.tileIndex++;
            _isNextFightBoss = false;
            _isBoardTileValidated = false;
            LastFightWasBoss = false;
            HasLastFightResult = false;
            LastFightCoinsReward = 0;
            _pendingSpellRewardOffers.Clear();
            _currentShopOffers.Clear();
            ClearPendingReplacement();

            if (CurrentRun.tileIndex >= GetTilesForCurrentZone())
            {
                CurrentRun.zoneIndex++;
                CurrentRun.tileIndex = 0;
                CurrentRun.branchChoice = -1;

                var zoneCount = _runLoopConfig != null
                    ? _runLoopConfig.GetZoneCount(fallbackZoneCount)
                    : Mathf.Max(1, fallbackZoneCount);
                if (CurrentRun.zoneIndex >= zoneCount)
                {
                    Debug.Log("Run complete after Zone " + zoneCount + ".");
                    EndRun(0);
                    return;
                }

                PersistRunProgress();
                Debug.Log("Zone cleared. Moving to Zone " + GetCurrentZoneNumber() + ".");
                EnterBoardViaZoneAnimation();
                return;
            }

            PersistRunProgress();
            Debug.Log("Moving to Zone " + GetCurrentZoneNumber() + ", Tile " + (GetActiveTileIndex() + 1) + ".");
            EnterBoardViaZoneAnimation();
        }

        private void PersistRunProgress()
        {
            if (CurrentRun != null && CurrentRun.isActive)
            {
                MetaProgression.SetRunProgress(
                    CurrentRun.zoneIndex,
                    CurrentRun.tileIndex,
                    CurrentRun.branchChoice,
                    CurrentRun.boardNodeIndex,
                    CurrentRun.coinsGained,
                    CurrentRun.selectedChampionIds,
                    CurrentRun.deckCardIds,
                    CurrentRun.championSpellLoadouts,
                    HasLastFightResult,
                    LastFightWasVictory,
                    LastFightWasBoss,
                    LastFightCoinsReward,
                    _pendingSpellRewardOffers,
                    _currentShopOffers,
                    _pendingReplacementIncomingSpellId,
                    _pendingReplacementTargetChampionId,
                    _pendingReplacementFromShop,
                    _pendingReplacementShopCost);
            }
            else
            {
                MetaProgression.ClearRunProgress();
            }

            SaveMeta();
        }

        private void RestoreRunProgress()
        {
            if (!MetaProgression.hasActiveRunProgress)
            {
                return;
            }

            CurrentRun.zoneIndex = Mathf.Max(0, MetaProgression.runZoneIndex);
            CurrentRun.tileIndex = Mathf.Max(0, MetaProgression.runTileIndex);
            CurrentRun.branchChoice = MetaProgression.runBranchChoice;
            CurrentRun.boardNodeIndex = Mathf.Max(0, MetaProgression.runBoardNodeIndex);
            CurrentRun.coinsGained = Mathf.Max(0, MetaProgression.runCoinsGained);
            CurrentRun.SetTeam(MetaProgression.runSelectedChampionIds);
            CurrentRun.deckCardIds.Clear();
            CurrentRun.deckCardIds.AddRange(MetaProgression.runDeckSpellIds.Where(id => !string.IsNullOrWhiteSpace(id)));
            CurrentRun.championSpellLoadouts = MetaProgression.runChampionSpellLoadouts != null
                ? MetaProgression.runChampionSpellLoadouts
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.championId))
                    .Select(x => new ChampionSpellLoadout
                    {
                        championId = x.championId,
                        spellIds = x.spellIds != null
                            ? x.spellIds.Where(id => !string.IsNullOrWhiteSpace(id)).Take(MaxRunSpells).ToList()
                            : new List<string>()
                    })
                    .ToList()
                : new List<ChampionSpellLoadout>();
            EnsureChampionSpellLoadoutsInitialized();
            HasLastFightResult = MetaProgression.runHasLastFightResult;
            LastFightWasVictory = MetaProgression.runLastFightWasVictory;
            LastFightWasBoss = MetaProgression.runLastFightWasBoss;
            LastFightCoinsReward = Mathf.Max(0, MetaProgression.runLastFightCoinsReward);
            _pendingSpellRewardOffers.Clear();
            _pendingSpellRewardOffers.AddRange(MetaProgression.runPendingSpellRewardOffers.Where(id => !string.IsNullOrWhiteSpace(id)));
            _currentShopOffers.Clear();
            _currentShopOffers.AddRange(MetaProgression.runShopOffers.Where(id => !string.IsNullOrWhiteSpace(id)));
            _pendingReplacementIncomingSpellId = MetaProgression.runPendingReplacementIncomingSpellId;
            _pendingReplacementTargetChampionId = MetaProgression.runPendingReplacementChampionId;
            _pendingReplacementFromShop = MetaProgression.runPendingReplacementFromShop;
            _pendingReplacementShopCost = Mathf.Max(0, MetaProgression.runPendingReplacementShopCost);
            ValidatePendingStateAfterRestore();
            CurrentRun.isActive = true;
            Debug.Log("Run progress restored: zone=" + GetCurrentZoneNumber() + ", tile=" + (GetActiveTileIndex() + 1) + ".");
        }

        private void ValidatePendingStateAfterRestore()
        {
            if (!IsWaitingSpellReplacementChoice())
            {
                return;
            }

            var incomingExists = FindSpellById(_pendingReplacementIncomingSpellId) != null;
            if (!incomingExists)
            {
                ClearPendingReplacement();
                return;
            }

            if (!string.IsNullOrWhiteSpace(_pendingReplacementTargetChampionId))
            {
                var spells = CurrentRun.GetChampionSpells(_pendingReplacementTargetChampionId);
                if (spells == null || spells.Count == 0)
                {
                    _pendingReplacementTargetChampionId = null;
                }
            }
        }

        private static void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        public bool CanStartFight()
        {
            var tileType = GetCurrentTileType();
            var isCombatTile = tileType == BoardTileType.Fight || tileType == BoardTileType.Boss;
            return CurrentRun.isActive && CurrentState == GameFlowState.Board && _isBoardTileValidated && isCombatTile;
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
                case GameScenes.ZoneAnimation:
                    return GameFlowState.ZoneAnimation;
                case GameScenes.BossCelebration:
                    return GameFlowState.BossCelebration;
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
