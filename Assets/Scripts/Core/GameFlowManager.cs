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
        public string LastFightEnemyId { get; private set; } = string.Empty;
        public string LastFightEnemyDisplayName { get; private set; } = string.Empty;
        public int SpellRewardRefreshesRemaining => Mathf.Max(0, _pendingSpellRewardRefreshesRemaining);

        [SerializeField] private float minLoadingDuration = 0.7f;
        [SerializeField] private int fallbackZoneCount = 2;
        [SerializeField] private int fallbackVictoryCoinReward = 12;
        private Coroutine _loadingCoroutine;
        private bool _isBoardTileValidated;
        private bool _isNextFightBoss;
        private bool _pendingAdvanceAfterBossCelebration;
        private RunLoopConfigAsset _runLoopConfig;
        private readonly List<string> _pendingSpellRewardOffers = new List<string>();
        private readonly List<string> _currentShopOffers = new List<string>();
        private const int DefaultSpellRewardRefreshes = 1;
        private const int MaxRunSpells = 4;
        private const int ShopOfferCount = 6;
        private const int TilesPerZone = 8;
        private const int ShopTileIndex = TilesPerZone - 2;
        private const int BossTileIndex = TilesPerZone - 1;
        private string _pendingReplacementIncomingSpellId;
        private string _pendingReplacementTargetChampionId;
        private bool _pendingReplacementFromShop;
        private int _pendingReplacementShopCost;
        private int _pendingSpellRewardRefreshesRemaining;
        private Dictionary<string, CombatSpellAsset> _spellIndexCache;
        private Dictionary<string, EnemyDefinition> _enemyIndexCache;
        private readonly System.Random _rewardRng = new System.Random();
        private readonly HashSet<string> _shopPurchasedSpellIds = new HashSet<string>();

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
                case GameFlowState.Shop:
                    EnsureController<ShopSceneController>("ShopSceneController");
                    break;
                case GameFlowState.Fight:
                    EnsureController<FightMockController>("FightMockController");
                    break;
                case GameFlowState.Result:
                    if (SceneManager.GetActiveScene().name == GameScenes.Result)
                    {
                        EnsureController<ResultSceneController>("ResultSceneController");
                    }
                    else
                    {
                        EnsureController<FightEndSceneController>("FightEndSceneController");
                        EnsureController<EnemyFightPanelView>("EnemyFightPanelView");
                    }
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
                if (HasLastFightResult)
                {
                    LoadScene(GameScenes.FightEnd);
                    return;
                }

                LoadScene(GameScenes.Board);
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
                LoadScene(GameScenes.Board);
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
            MetaProgression.lifetimeRunCount = Mathf.Max(0, MetaProgression.lifetimeRunCount) + 1;
            InitializeRunInventory();
            CurrentRun.isActive = true;
            HasLastFightResult = false;
            LastFightWasVictory = false;
            LastFightWasBoss = false;
            LastFightCoinsReward = 0;
            LastFightEnemyId = string.Empty;
            LastFightEnemyDisplayName = string.Empty;
            _isBoardTileValidated = false;
            _isNextFightBoss = false;
            _pendingAdvanceAfterBossCelebration = false;
            _pendingSpellRewardOffers.Clear();
            _currentShopOffers.Clear();
            _shopPurchasedSpellIds.Clear();
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
            LastFightEnemyId = string.Empty;
            LastFightEnemyDisplayName = string.Empty;
            _isBoardTileValidated = false;
            _isNextFightBoss = false;
            _pendingAdvanceAfterBossCelebration = false;
            _pendingSpellRewardOffers.Clear();
            _currentShopOffers.Clear();
            _shopPurchasedSpellIds.Clear();
            ClearPendingReplacement();
            MetaProgression.ClearRunProgress();

            MetaProgression.EnsureCollections();
            MetaProgression.progressionPoints = 0;
            MetaProgression.unlockedSpellIds.Clear();
            MetaProgression.unlockedChampionIds.Clear();
            MetaProgression.EnsureDefaultUnlockedChampions(ChampionCatalog.GetDefaultUnlockedChampionIds(3));

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

        public void DebugOpenShop()
        {
            var team = CurrentRun.HasValidTeam()
                ? CurrentRun.selectedChampionIds.ToList()
                : ChampionCatalog.GetDefaultUnlockedChampionIds(3).ToList();

            CurrentRun.Reset();
            CurrentRun.SetTeam(team);
            CurrentRun.isActive = true;
            CurrentRun.zoneIndex = 0;
            CurrentRun.tileIndex = ShopTileIndex;
            CurrentRun.branchChoice = 0;
            CurrentRun.coinsGained = Mathf.Max(CurrentRun.coinsGained, 100);

            InitializeRunInventory();
            HasLastFightResult = false;
            LastFightWasVictory = false;
            LastFightWasBoss = false;
            LastFightCoinsReward = 0;
            LastFightEnemyId = string.Empty;
            LastFightEnemyDisplayName = string.Empty;
            _isBoardTileValidated = false;
            _isNextFightBoss = false;
            _pendingAdvanceAfterBossCelebration = false;
            _pendingSpellRewardOffers.Clear();
            _currentShopOffers.Clear();
            _shopPurchasedSpellIds.Clear();
            ClearPendingReplacement();
            EnsureCurrentShopOffers();
            PersistRunProgress();

            LoadScene(GameScenes.Shop);
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
            return TilesPerZone;
        }

        public BoardTileType GetCurrentTileType()
        {
            var index = Mathf.Clamp(CurrentRun.tileIndex, 0, BossTileIndex);
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

            if (index == ShopTileIndex)
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

        public void EnterShopFromBoard()
        {
            if (!CurrentRun.isActive || CurrentState != GameFlowState.Board || GetCurrentTileType() != BoardTileType.Shop)
            {
                Debug.LogWarning("EnterShop blocked: must validate the active Shop tile from Board.");
                return;
            }

            if (!TryValidateCurrentBoardTile(BoardTileType.Shop))
            {
                return;
            }

            EnsureCurrentShopOffers();
            _isBoardTileValidated = false;
            PersistRunProgress();
            LoadScene(GameScenes.Shop);
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
            if (!CurrentRun.isActive || CurrentState != GameFlowState.Shop || GetCurrentTileType() != BoardTileType.Shop)
            {
                Debug.LogWarning("SkipShop blocked: shop can only be skipped from ShopScene.");
                return;
            }

            _currentShopOffers.Clear();
            ClearPendingReplacement();
            PersistRunProgress();
            AdvanceBoardProgress(false);
        }

        public bool TrySelectShopSpell(string spellId, out string message)
        {
            if (!CurrentRun.isActive || CurrentState != GameFlowState.Shop || GetCurrentTileType() != BoardTileType.Shop)
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

            if (IsSpellUnlocked(spellId))
            {
                message = "Already bought.";
                return false;
            }

            CurrentRun.coinsGained -= cost;
            MetaProgression.UnlockSpell(spellId);
            AddSpellToRunInventory(spellId);
            _shopPurchasedSpellIds.Add(spellId);
            ClearPendingReplacement();
            message = "Spell bought.";
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
            LastFightEnemyId = string.Empty;
            LastFightEnemyDisplayName = string.Empty;

            var fight = FindFirstObjectByType<FightMockController>();
            if (fight != null && fight.TryGetCurrentEnemyDefinition(out var enemy) && enemy != null)
            {
                LastFightEnemyId = enemy.Id;
                LastFightEnemyDisplayName = enemy.DisplayName;
            }

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
            LoadScene(GameScenes.FightEnd);
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

        public void EndRun(int pointsEarned, string nextScene = null)
        {
            if (!CanEndRun())
            {
                Debug.LogWarning("EndRun blocked: only available on FightEnd screen after a fight result.");
                return;
            }

            CompleteRun(pointsEarned, nextScene);
        }

        private void CompleteRun(int pointsEarned, string nextScene = null)
        {
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
            LastFightEnemyId = string.Empty;
            LastFightEnemyDisplayName = string.Empty;
            _isBoardTileValidated = false;
            _isNextFightBoss = false;
            _pendingAdvanceAfterBossCelebration = false;
            _pendingSpellRewardOffers.Clear();
            _currentShopOffers.Clear();
            _shopPurchasedSpellIds.Clear();
            ClearPendingReplacement();
            MetaProgression.ClearRunProgress();
            SaveMeta();
            Debug.Log("Run ended. Reward added: " + totalReward + " coins.");
            LoadScene(string.IsNullOrWhiteSpace(nextScene) ? GameScenes.Home : nextScene);
        }

        public IReadOnlyList<string> GetPendingSpellRewardOffers()
        {
            return _pendingSpellRewardOffers;
        }

        public bool HasPendingSpellRewardChoice()
        {
            return _pendingSpellRewardOffers.Count > 0;
        }

        public bool CanRefreshSpellRewardOffers()
        {
            return CurrentRun != null
                && CurrentRun.isActive
                && CurrentState == GameFlowState.Result
                && LastFightWasVictory
                && HasPendingSpellRewardChoice()
                && !IsWaitingSpellReplacementChoice()
                && SpellRewardRefreshesRemaining > 0;
        }

        public void AddSpellRewardRefreshes(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _pendingSpellRewardRefreshesRemaining = Mathf.Max(0, _pendingSpellRewardRefreshesRemaining) + amount;
            PersistRunProgress();
        }

        public IReadOnlyList<string> GetRunInventorySpellIds()
        {
            return CurrentRun.deckCardIds;
        }

        public IReadOnlyList<string> GetRunDeckSpellIds()
        {
            return GetRunInventorySpellIds();
        }

        public bool IsSpellInRunInventory(string spellId)
        {
            return !string.IsNullOrWhiteSpace(spellId) && CurrentRun.deckCardIds.Contains(spellId);
        }

        public bool IsSpellUnlocked(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return false;
            }

            MetaProgression.EnsureCollections();
            return MetaProgression.unlockedSpellIds.Contains(spellId);
        }

        public bool WasSpellBoughtFromShop(string spellId)
        {
            return !string.IsNullOrWhiteSpace(spellId) && _shopPurchasedSpellIds.Contains(spellId);
        }

        public bool AddSpellToRunInventory(string spellId)
        {
            if (!CurrentRun.isActive || string.IsNullOrWhiteSpace(spellId) || CurrentRun.deckCardIds.Contains(spellId))
            {
                return false;
            }

            CurrentRun.deckCardIds.Add(spellId);
            PersistRunProgress();
            return true;
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

        public string GetSpellDisplayName(string spellId)
        {
            var spell = FindSpellById(spellId);
            if (spell == null || string.IsNullOrWhiteSpace(spell.DisplayName))
            {
                return string.IsNullOrWhiteSpace(spellId) ? "Unknown" : spellId;
            }

            return spell.DisplayName;
        }

        public bool TryGetSpellAsset(string spellId, out CombatSpellAsset spell)
        {
            spell = FindSpellById(spellId);
            return spell != null;
        }

        public bool TryGetSpellPrimaryElement(string spellId, out ElementType spellElement)
        {
            return TryGetSpellElement(spellId, out spellElement);
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

        public int GetLifetimeRunCount()
        {
            return Mathf.Max(0, MetaProgression.lifetimeRunCount);
        }

        public string GetLifetimeRunLabel()
        {
            return "Run #" + Mathf.Max(1, GetLifetimeRunCount());
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

            if (!DoesSpellMatchChampionElement(championId, _pendingReplacementIncomingSpellId))
            {
                message = "Spell type does not match this champion.";
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

            AddSpellToRunInventory(spellId);
            _pendingSpellRewardOffers.Clear();
            ClearPendingReplacement();
            message = "Spell added to run inventory.";
            PersistRunProgress();
            return true;
        }

        public void SkipSpellReward()
        {
            _pendingSpellRewardOffers.Clear();
            ClearPendingReplacement();
            PersistRunProgress();
        }

        public bool RefreshSpellRewardOffers()
        {
            if (!CanRefreshSpellRewardOffers())
            {
                return false;
            }

            _pendingSpellRewardRefreshesRemaining = Mathf.Max(0, _pendingSpellRewardRefreshesRemaining - 1);
            RerollSpellRewardOffers();
            PersistRunProgress();
            return true;
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

            if (!DoesSpellMatchChampionElement(_pendingReplacementTargetChampionId, _pendingReplacementIncomingSpellId))
            {
                message = "Spell type does not match this champion.";
                ClearPendingReplacement();
                PersistRunProgress();
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
            EnsureRunInventoryContainsChampionLoadouts();

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
            if (MetaProgression.unlockedChampionIds.Contains("crusher"))
            {
                MetaProgression.unlockedChampionIds.Remove("crusher");
                changed = true;
            }

            if (!MetaProgression.unlockedChampionIds.Contains("slugger"))
            {
                MetaProgression.unlockedChampionIds.Add("slugger");
                changed = true;
            }

            if (changed)
            {
                SaveMeta();
            }
        }

        private void LoadRunLoopConfig()
        {
            var registry = RuntimeAssetRegistryAsset.Load();
            _runLoopConfig = registry != null ? registry.RunLoopConfig : null;
            if (_runLoopConfig == null)
            {
                Debug.LogWarning("RunLoopConfig missing in RuntimeAssetRegistry. Using fallback values.");
                return;
            }

            Debug.Log("RunLoopConfig loaded.");
        }

        private void BuildSpellRewardOffers()
        {
            _pendingSpellRewardRefreshesRemaining = DefaultSpellRewardRefreshes;
            RerollSpellRewardOffers();
        }

        private void RerollSpellRewardOffers()
        {
            _pendingSpellRewardOffers.Clear();
            ClearPendingReplacement();
            var candidates = BuildAvailableVictoryRewardSpellIds();
            if (candidates.Count == 0)
            {
                _pendingSpellRewardRefreshesRemaining = 0;
                return;
            }

            var picks = Mathf.Min(ShopOfferCount, candidates.Count);
            for (var i = 0; i < picks; i++)
            {
                var index = _rewardRng.Next(0, candidates.Count);
                _pendingSpellRewardOffers.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            if (_pendingSpellRewardOffers.Count == 0)
            {
                _pendingSpellRewardRefreshesRemaining = 0;
            }
        }

        private List<string> BuildAvailableVictoryRewardSpellIds()
        {
            return GetSpellIndex()
                .Values
                .Where(spell => spell != null && spell.IsValidForEnemy())
                .OrderBy(spell => spell.DisplayName)
                .Select(spell => spell.SpellId)
                .Distinct()
                .ToList();
        }

        private void EnsureCurrentShopOffers()
        {
            var spellIndex = GetSpellIndex();
            _currentShopOffers.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !spellIndex.ContainsKey(id) || (IsSpellUnlocked(id) && !WasSpellBoughtFromShop(id)));

            for (var i = _currentShopOffers.Count - 1; i >= 0; i--)
            {
                if (_currentShopOffers.IndexOf(_currentShopOffers[i]) != i)
                {
                    _currentShopOffers.RemoveAt(i);
                }
            }

            if (_currentShopOffers.Count >= ShopOfferCount)
            {
                return;
            }

            var candidates = BuildAvailableRunSpellIds();
            if (candidates.Count == 0)
            {
                return;
            }

            candidates.RemoveAll(id => _currentShopOffers.Contains(id));
            var picks = Mathf.Min(ShopOfferCount - _currentShopOffers.Count, candidates.Count);
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
            var selectedElements = CurrentRun.selectedChampionIds
                .Select(GetChampionElement)
                .Where(element => element.HasValue)
                .Select(element => element.Value)
                .Distinct()
                .ToHashSet();

            return index.Keys
                .Where(id => !IsSpellUnlocked(id))
                .Where(id => selectedElements.Count == 0 || selectedElements.Any(element => DoesSpellMatchElement(id, element)))
                .OrderBy(id => id)
                .ToList();
        }

        public void EnsureChampionSpellLoadoutsInitialized()
        {
            var selectedChampionIds = CurrentRun.selectedChampionIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .Take(3)
                .ToList();

            var legacyDefaults = BuildLegacyDefaultSpellLoadout();
            var shouldUpgradeLegacyLoadouts = selectedChampionIds.Count > 0
                && selectedChampionIds.All(championId => SpellListsMatch(GetExistingChampionSpellIds(championId), legacyDefaults));

            if (shouldUpgradeLegacyLoadouts)
            {
                CurrentRun.championSpellLoadouts.Clear();
            }

            foreach (var championId in selectedChampionIds)
            {
                var loadout = CurrentRun.GetChampionSpells(championId);
                var nextLoadout = shouldUpgradeLegacyLoadouts || loadout.Count == 0
                    ? BuildDefaultSpellLoadoutForChampion(championId)
                    : BuildSanitizedChampionLoadout(championId, loadout);

                if (SpellListsMatch(loadout, nextLoadout))
                {
                    continue;
                }

                loadout.Clear();
                loadout.AddRange(nextLoadout);
            }

            EnsureRunInventoryContainsChampionLoadouts();
        }

        private List<string> BuildLegacyDefaultSpellLoadout()
        {
            return GetSpellIndex().Keys
                .OrderBy(id => id)
                .Take(MaxRunSpells)
                .ToList();
        }

        private List<string> BuildDefaultSpellLoadoutForChampion(string championId)
        {
            return BuildAllowedSpellIdsForChampion(championId)
                .Take(MaxRunSpells)
                .ToList();
        }

        private List<string> BuildSanitizedChampionLoadout(string championId, IEnumerable<string> existingSpellIds)
        {
            var sanitized = FilterSpellIdsForChampion(championId, existingSpellIds);
            foreach (var spellId in BuildAllowedSpellIdsForChampion(championId))
            {
                if (sanitized.Count >= MaxRunSpells)
                {
                    break;
                }

                if (!sanitized.Contains(spellId))
                {
                    sanitized.Add(spellId);
                }
            }

            return sanitized;
        }

        private List<string> FilterSpellIdsForChampion(string championId, IEnumerable<string> spellIds)
        {
            var filtered = new List<string>();
            if (spellIds == null)
            {
                return filtered;
            }

            foreach (var spellId in spellIds)
            {
                if (string.IsNullOrWhiteSpace(spellId) || filtered.Contains(spellId))
                {
                    continue;
                }

                if (DoesSpellMatchChampionElement(championId, spellId))
                {
                    filtered.Add(spellId);
                }
            }

            return filtered;
        }

        private List<string> BuildAllowedSpellIdsForChampion(string championId)
        {
            var championElement = GetChampionElement(championId);
            if (!championElement.HasValue)
            {
                return new List<string>();
            }

            return GetSpellIndex().Values
                .Where(spell => spell != null && DoesSpellMatchElement(spell.SpellId, championElement.Value))
                .OrderBy(spell => spell.CostEntries != null ? spell.CostEntries.Where(entry => entry != null && entry.amount > 0).Sum(entry => entry.amount) : int.MaxValue)
                .ThenBy(spell => spell.DisplayName)
                .Select(spell => spell.SpellId)
                .ToList();
        }

        private List<string> GetExistingChampionSpellIds(string championId)
        {
            var loadout = CurrentRun.championSpellLoadouts
                .FirstOrDefault(entry => entry != null && entry.championId == championId);
            if (loadout == null || loadout.spellIds == null)
            {
                return new List<string>();
            }

            return loadout.spellIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
        }

        private static bool SpellListsMatch(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i], System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private ElementType? GetChampionElement(string championId)
        {
            if (string.IsNullOrWhiteSpace(championId))
            {
                return null;
            }

            var champion = ChampionCatalog.FindById(championId);
            return champion != null ? champion.Element : (ElementType?)null;
        }

        private bool DoesSpellMatchChampionElement(string championId, string spellId)
        {
            var championElement = GetChampionElement(championId);
            return championElement.HasValue && DoesSpellMatchElement(spellId, championElement.Value);
        }

        private bool DoesSpellMatchElement(string spellId, ElementType expectedElement)
        {
            return TryGetSpellElement(spellId, out var spellElement) && spellElement == expectedElement;
        }

        private bool TryGetSpellElement(string spellId, out ElementType spellElement)
        {
            spellElement = default;
            var spell = FindSpellById(spellId);
            if (spell == null || spell.CostEntries == null || spell.CostEntries.Count == 0)
            {
                return false;
            }

            var firstCost = spell.CostEntries.FirstOrDefault(cost => cost != null && cost.amount > 0);
            if (firstCost == null)
            {
                return false;
            }

            foreach (var cost in spell.CostEntries)
            {
                if (cost == null || cost.amount <= 0)
                {
                    continue;
                }

                if (cost.element != firstCost.element)
                {
                    return false;
                }
            }

            spellElement = firstCost.element;
            return true;
        }

        public void InitializeRunInventory()
        {
            EnsureChampionSpellLoadoutsInitialized();
        }

        private void EnsureRunInventoryContainsChampionLoadouts()
        {
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

            var registry = RuntimeAssetRegistryAsset.Load();
            var library = registry != null ? registry.SpellLibrary : null;
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

        private Dictionary<string, EnemyDefinition> GetEnemyIndex()
        {
            if (_enemyIndexCache != null)
            {
                return _enemyIndexCache;
            }

            var registry = RuntimeAssetRegistryAsset.Load();
            var catalog = registry != null ? registry.EnemyCatalog : null;
            if (catalog == null)
            {
                _enemyIndexCache = new Dictionary<string, EnemyDefinition>();
                return _enemyIndexCache;
            }

            var spellIndex = GetSpellIndex();
            var enemies = catalog.BuildRuntimeDefinitions(spellIndex);
            _enemyIndexCache = enemies
                .Where(enemy => enemy != null && !string.IsNullOrWhiteSpace(enemy.Id))
                .GroupBy(enemy => enemy.Id)
                .ToDictionary(group => group.Key, group => group.First());
            return _enemyIndexCache;
        }

        private bool TryGetEnemyDefinitionById(string enemyId, out EnemyDefinition enemy)
        {
            enemy = null;
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                return false;
            }

            var index = GetEnemyIndex();
            return index.TryGetValue(enemyId, out enemy) && enemy != null;
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
            LastFightEnemyId = string.Empty;
            LastFightEnemyDisplayName = string.Empty;
            _pendingSpellRewardOffers.Clear();
            _currentShopOffers.Clear();
            ClearPendingReplacement();

            if (CurrentRun.tileIndex >= GetTilesForCurrentZone())
            {
                var zoneCount = _runLoopConfig != null
                    ? _runLoopConfig.GetZoneCount(fallbackZoneCount)
                    : Mathf.Max(1, fallbackZoneCount);
                if (CurrentRun.zoneIndex + 1 >= zoneCount)
                {
                    Debug.Log("Run complete after Zone " + zoneCount + ".");
                    CurrentRun.tileIndex = GetTilesForCurrentZone() - 1;
                    CompleteRun(0);
                    return;
                }

                CurrentRun.zoneIndex++;
                CurrentRun.tileIndex = 0;
                CurrentRun.branchChoice = -1;
                PersistRunProgress();
                Debug.Log("Zone cleared. Moving to Zone " + GetCurrentZoneNumber() + ".");
                EnterBoardViaZoneAnimation();
                return;
            }

            PersistRunProgress();
            Debug.Log("Moving to Zone " + GetCurrentZoneNumber() + ", Tile " + (GetActiveTileIndex() + 1) + ".");
            LoadScene(GameScenes.Board);
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
                LastFightEnemyId,
                LastFightEnemyDisplayName,
                _pendingSpellRewardOffers,
                _pendingSpellRewardRefreshesRemaining,
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
            InitializeRunInventory();
            HasLastFightResult = MetaProgression.runHasLastFightResult;
            LastFightWasVictory = MetaProgression.runLastFightWasVictory;
            LastFightWasBoss = MetaProgression.runLastFightWasBoss;
            LastFightCoinsReward = Mathf.Max(0, MetaProgression.runLastFightCoinsReward);
            LastFightEnemyId = MetaProgression.runLastFightEnemyId ?? string.Empty;
            LastFightEnemyDisplayName = MetaProgression.runLastFightEnemyDisplayName ?? string.Empty;
            _pendingSpellRewardOffers.Clear();
            _pendingSpellRewardOffers.AddRange(MetaProgression.runPendingSpellRewardOffers.Where(id => !string.IsNullOrWhiteSpace(id)));
            _pendingSpellRewardRefreshesRemaining = Mathf.Max(0, MetaProgression.runPendingSpellRewardRefreshesRemaining);
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
                if (spells == null || spells.Count == 0 || !DoesSpellMatchChampionElement(_pendingReplacementTargetChampionId, _pendingReplacementIncomingSpellId))
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

        public bool TryGetLastFightEnemyDefinition(out EnemyDefinition enemy)
        {
            return TryGetEnemyDefinitionById(LastFightEnemyId, out enemy);
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
                case GameScenes.Shop:
                    return GameFlowState.Shop;
                case GameScenes.Fight:
                    return GameFlowState.Fight;
                case GameScenes.FightEnd:
                case GameScenes.Result:
                    return GameFlowState.Result;
                default:
                    return GameFlowState.Home;
            }
        }
    }
}
