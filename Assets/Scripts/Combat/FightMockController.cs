using ProjectZ.Core;
using ProjectZ.Run;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectZ.Combat
{
    public class FightMockController : MonoBehaviour
    {
        private const string DisableDebugUiMarkerName = "DISABLE_DEBUG_UI";
        private const int GemsPerTurn = 4;
        private const int MaxRerollsPerTurn = 2;
        private const int CardsDrawnPerTurn = 4;
        private const int CombatLogMaxEntries = 8;
        private const int DefaultCombatsPerZone = 8;
        private static readonly ElementType[] AllElements = (ElementType[])System.Enum.GetValues(typeof(ElementType));

        private readonly System.Random _rng = new System.Random();
        private readonly List<GemSlot> _gems = new List<GemSlot>();
        private readonly List<GemSlot> _enemyGems = new List<GemSlot>();
        private readonly List<ChampionCombatState> _champions = new List<ChampionCombatState>();
        private readonly List<EnemyDefinition> _availableEnemies = new List<EnemyDefinition>();
        private readonly List<string> _combatLog = new List<string>();
        private readonly HashSet<CardDefinition> _playedCardsThisTurn = new HashSet<CardDefinition>();
        private readonly HashSet<EnemyIntentDefinition> _enemyUsedIntentsThisTurn = new HashSet<EnemyIntentDefinition>();
        private Dictionary<string, CombatSpellAsset> _spellIndexCache;

        private int _activeChampionIndex;
        private int _turn = 1;
        private int _rerollsRemaining;
        private int _enemyRerollsRemaining;
        private EnemyCombatState _enemy;
        private CombatSpawnRulesAsset _spawnRules;
        private bool _fightResolved;
        private string _lastAction = "Fight started";
        private string _lastLoggedMarker;
        private Vector2 _combatLogScroll;
        private EnemyBiome? _debugBiomeOverride;
        private EnemyTier? _debugTierOverride;
        private string _debugEnemyIdOverride;

        public bool IsFightResolved => _fightResolved;
        public int RerollsRemaining => _rerollsRemaining;
        public int MaxRerolls => MaxRerollsPerTurn;
        public bool CanRefreshRunes => !_fightResolved && _rerollsRemaining > 0;
        public int RuneCount => _gems.Count;
        public string ActiveChampionId
        {
            get
            {
                if (_champions.Count == 0 || _activeChampionIndex < 0 || _activeChampionIndex >= _champions.Count)
                {
                    return string.Empty;
                }

                var champion = _champions[_activeChampionIndex];
                return champion != null ? champion.Id : string.Empty;
            }
        }
        
        public bool TryGetTeamHealthTotals(out int currentHp, out int maxHp)
        {
            currentHp = 0;
            maxHp = 0;

            if (_champions.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < _champions.Count; i++)
            {
                var champion = _champions[i];
                if (champion == null)
                {
                    continue;
                }

                currentHp += champion.CurrentHp;
                maxHp += champion.MaxHp;
            }

            return maxHp > 0;
        }

        public bool TryGetTeamHealthState(out int currentHp, out int maxHp, out int shieldHp)
        {
            currentHp = 0;
            maxHp = 0;
            shieldHp = 0;

            if (_champions.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < _champions.Count; i++)
            {
                var champion = _champions[i];
                if (champion == null)
                {
                    continue;
                }

                currentHp += champion.CurrentHp;
                maxHp += champion.MaxHp;
                shieldHp += champion.Block;
            }

            shieldHp = Mathf.Clamp(shieldHp, 0, maxHp);
            return maxHp > 0;
        }

        public bool TryGetEnemyHealthState(out int currentHp, out int maxHp, out int shieldHp)
        {
            currentHp = 0;
            maxHp = 0;
            shieldHp = 0;

            if (_enemy == null)
            {
                return false;
            }

            currentHp = _enemy.CurrentHp;
            maxHp = _enemy.MaxHp;
            shieldHp = Mathf.Clamp(_enemy.Block, 0, maxHp);
            return maxHp > 0;
        }

        public bool TryGetCurrentEnemyDefinition(out EnemyDefinition definition)
        {
            definition = _enemy != null ? _enemy.Definition : null;
            return definition != null;
        }

        public bool TryGetEnemyStatusEffects(out List<EnemyStatusEffectState> statusEffects)
        {
            statusEffects = new List<EnemyStatusEffectState>();
            if (_enemy == null)
            {
                return false;
            }

            for (var i = 0; i < _enemy.StatusEffects.Count; i++)
            {
                var status = _enemy.StatusEffects[i];
                if (status == null)
                {
                    continue;
                }

                statusEffects.Add(status);
            }

            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnFightScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.Fight)
            {
                return;
            }

            var existing = FindFirstObjectByType<FightMockController>();
            if (existing != null)
            {
                return;
            }

            var go = new GameObject("FightMockController");
            go.AddComponent<FightMockController>();
        }

        private void Start()
        {
            var manager = GameFlowManager.Instance;
            if (manager != null)
            {
                manager.EnsureChampionSpellLoadoutsInitialized();
            }

            BuildChampionStates();
            BuildEnemyDefinitions();
            _enemy = new EnemyCombatState(SelectEnemyDefinition());
            _fightResolved = false;
            StartTurn();
        }

        private void Update()
        {
            TrackCombatLog();
            HandleDebugOverrideHotkeys();
        }

        private void HandleDebugOverrideHotkeys()
        {
            if (IsDebugBiomeTogglePressed())
            {
                CycleBiomeOverride();
                _lastAction = "Biome override: " + (_debugBiomeOverride.HasValue ? _debugBiomeOverride.Value.ToString() : "Auto");
            }

            if (IsDebugTierTogglePressed())
            {
                CycleTierOverride();
                _lastAction = "Tier override: " + (_debugTierOverride.HasValue ? _debugTierOverride.Value.ToString() : "Auto");
            }

            if (IsDebugEnemyTogglePressed())
            {
                CycleEnemyOverride();
                _lastAction = "Enemy override: " + (string.IsNullOrEmpty(_debugEnemyIdOverride) ? "Auto" : _debugEnemyIdOverride);
            }
        }

        private static bool IsDebugBiomeTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F6);
#endif
        }

        private static bool IsDebugTierTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F7);
#endif
        }

        private static bool IsDebugEnemyTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F8);
#endif
        }

        private EnemyDefinition SelectEnemyDefinition()
        {
            if (_availableEnemies.Count == 0)
            {
                BuildEnemyDefinitions();
            }

            ResolveSpawnContext(out var targetBiome, out var zoneCombatIndex, out var isBossFight);
            var targetTier = ResolveTargetTier(targetBiome, zoneCombatIndex, isBossFight);

            if (!string.IsNullOrEmpty(_debugEnemyIdOverride))
            {
                var forced = _availableEnemies.FirstOrDefault(enemy => enemy.Id == _debugEnemyIdOverride && enemy.Biome == targetBiome && enemy.Tier == targetTier)
                    ?? _availableEnemies.FirstOrDefault(enemy => enemy.Id == _debugEnemyIdOverride && enemy.Biome == targetBiome)
                    ?? _availableEnemies.FirstOrDefault(enemy => enemy.Id == _debugEnemyIdOverride);
                if (forced != null)
                {
                    return forced;
                }
            }

            var exactMatch = _availableEnemies
                .Where(enemy => enemy.Biome == targetBiome && enemy.Tier == targetTier)
                .ToList();
            if (exactMatch.Count > 0)
            {
                return exactMatch[_rng.Next(0, exactMatch.Count)];
            }

            var biomeMatch = _availableEnemies
                .Where(enemy => enemy.Biome == targetBiome)
                .ToList();
            if (biomeMatch.Count > 0)
            {
                return biomeMatch[_rng.Next(0, biomeMatch.Count)];
            }

            return _availableEnemies[_rng.Next(0, _availableEnemies.Count)];
        }

        private void BuildEnemyDefinitions()
        {
            _availableEnemies.Clear();

            var registry = RuntimeAssetRegistryAsset.Load();
            var spellLibrary = registry != null ? registry.SpellLibrary : null;
            if (spellLibrary == null)
            {
                Debug.LogWarning("SpellLibrary missing in RuntimeAssetRegistry.");
            }

            var spellIndex = spellLibrary != null
                ? spellLibrary.BuildIndexById()
                : new Dictionary<string, CombatSpellAsset>();

            _spawnRules = registry != null ? registry.SpawnRules : null;
            if (_spawnRules == null)
            {
                Debug.LogWarning("SpawnRules missing in RuntimeAssetRegistry.");
            }

            var asset = registry != null ? registry.EnemyCatalog : null;
            if (asset != null)
            {
                var fromAsset = asset.BuildRuntimeDefinitions(spellIndex);
                if (fromAsset.Count > 0)
                {
                    _availableEnemies.AddRange(fromAsset);
                }
            }
            else
            {
                Debug.LogWarning("EnemyCatalog missing in RuntimeAssetRegistry.");
            }

            if (_availableEnemies.Count == 0)
            {
                _availableEnemies.AddRange(BuildFallbackEnemyCatalog());
            }

            NormalizeEnemyDefinitions(spellIndex);
        }

        private void NormalizeEnemyDefinitions(IReadOnlyDictionary<string, CombatSpellAsset> spellIndex)
        {
            for (var i = 0; i < _availableEnemies.Count; i++)
            {
                var enemy = _availableEnemies[i];
                if (enemy == null || enemy.Intents == null || enemy.Intents.Count == 0)
                {
                    continue;
                }

                var primaryElement = enemy.TypeDefinition != null ? enemy.TypeDefinition.Element : ElementType.Fire;
                var normalized = new List<EnemyIntentDefinition>();
                var seenSpellIds = new HashSet<string>();

                foreach (var intent in enemy.Intents)
                {
                    if (intent == null || intent.Cost == null)
                    {
                        continue;
                    }

                    if (!intent.Cost.TryGetPrimaryElement(out var intentElement) || intentElement != primaryElement)
                    {
                        continue;
                    }

                    normalized.Add(intent);
                    if (!string.IsNullOrWhiteSpace(intent.SpellId))
                    {
                        seenSpellIds.Add(intent.SpellId);
                    }
                }

                if (normalized.Count < 4 && spellIndex != null)
                {
                    var matchingSpells = spellIndex.Values
                        .Where(spell => spell != null && spell.IsValidForEnemy() && spell.TryGetPrimaryElement(out var spellElement) && spellElement == primaryElement)
                        .OrderBy(spell => spell.CostEntries != null ? spell.CostEntries.Where(entry => entry != null && entry.amount > 0).Sum(entry => entry.amount) : int.MaxValue)
                        .ThenBy(spell => spell.DisplayName)
                        .ToList();

                    foreach (var spell in matchingSpells)
                    {
                        if (normalized.Count >= 4)
                        {
                            break;
                        }

                        if (spell == null || seenSpellIds.Contains(spell.SpellId))
                        {
                            continue;
                        }

                        normalized.Add(spell.ToEnemyIntentDefinition());
                        seenSpellIds.Add(spell.SpellId);
                    }
                }

                if (normalized.Count < 4)
                {
                    foreach (var intent in enemy.Intents)
                    {
                        if (normalized.Count >= 4)
                        {
                            break;
                        }

                        if (intent == null)
                        {
                            continue;
                        }

                        normalized.Add(intent);
                    }
                }

                if (normalized.Count == 0)
                {
                    continue;
                }

                var fallbackIntent = normalized[0];
                while (normalized.Count < 4)
                {
                    normalized.Add(fallbackIntent);
                }

                enemy.Intents.Clear();
                enemy.Intents.AddRange(normalized.Take(4));
            }
        }

        private static List<EnemyDefinition> BuildFallbackEnemyCatalog()
        {
            return new List<EnemyDefinition>
            {
                new EnemyDefinition(
                    "glass_shifter",
                    "Silica Mirage",
                    45,
                    EnemyBiome.Zone1,
                    EnemyTier.Minion,
                    new List<EnemyIntentDefinition>
                    {
                        new EnemyIntentDefinition("Refracted Ray", Cost(ElementType.Fire, 1, ElementType.Mystic, 1), new CardEffect(CardEffectType.Damage, 9)),
                        new EnemyIntentDefinition("Heat Veil", Cost(ElementType.Mystic, 2), new CardEffect(CardEffectType.Shield, 8)),
                        new EnemyIntentDefinition("Blinding Glare", Cost(ElementType.Fire, 1), new CardEffect(CardEffectType.Damage, 5))
                    }),
                new EnemyDefinition(
                    "quicksand_naga",
                    "Siltis the Buried",
                    80,
                    EnemyBiome.Zone2,
                    EnemyTier.Champion,
                    new List<EnemyIntentDefinition>
                    {
                        new EnemyIntentDefinition("Gritty Embrace", Cost(ElementType.Ground, 2), new CardEffect(CardEffectType.Damage, 12)),
                        new EnemyIntentDefinition("Quicksand Sink", Cost(ElementType.Ground, 3), new CardEffect(CardEffectType.Damage, 15)),
                        new EnemyIntentDefinition("Sandstorm Burst", Cost(ElementType.Ground, 2, ElementType.Fire, 1), new CardEffect(CardEffectType.Damage, 18))
                    }),
                new EnemyDefinition(
                    "solar_moloch",
                    "Magma Scout",
                    120,
                    EnemyBiome.Zone2,
                    EnemyTier.Boss,
                    new List<EnemyIntentDefinition>
                    {
                        new EnemyIntentDefinition("Searing Blood Jet", Cost(ElementType.Fire, 3), new CardEffect(CardEffectType.Damage, 14)),
                        new EnemyIntentDefinition("Solar Intake", Cost(ElementType.Fire, 2, ElementType.Ground, 1), new CardEffect(CardEffectType.Shield, 12)),
                        new EnemyIntentDefinition("Spike Eruption", Cost(ElementType.Fire, 2, ElementType.Ground, 2), new CardEffect(CardEffectType.Damage, 22))
                    }),
                new EnemyDefinition(
                    "static_skink",
                    "Bolt Runner",
                    35,
                    EnemyBiome.Zone1,
                    EnemyTier.Elite,
                    new List<EnemyIntentDefinition>
                    {
                        new EnemyIntentDefinition("Toxic Discharge", Cost(ElementType.Poison, 1), new CardEffect(CardEffectType.Damage, 7)),
                        new EnemyIntentDefinition("Overload", Cost(ElementType.Poison, 2), new CardEffect(CardEffectType.Damage, 6)),
                        new EnemyIntentDefinition("Sand Bolt", Cost(ElementType.Fire, 3, ElementType.Ground, 1), new CardEffect(CardEffectType.Damage, 25))
                    })
            };
        }

        private void BuildChampionStates()
        {
            _champions.Clear();

            var manager = GameFlowManager.Instance;
            var ids = manager != null ? manager.CurrentRun.selectedChampionIds : new List<string>();
            var sourceIds = ids.Count == 3 ? ids : new List<string> { "warden", "arcanist", "ranger" };
            var registry = RuntimeAssetRegistryAsset.Load();
            var spellLibrary = registry != null ? registry.SpellLibrary : null;
            var spellIndex = spellLibrary != null
                ? spellLibrary.BuildIndexById()
                : new Dictionary<string, CombatSpellAsset>();

            for (var i = 0; i < sourceIds.Count; i++)
            {
                var championId = sourceIds[i];
                var spellPool = BuildRunSpellPool(championId, i, manager, spellIndex);
                var championAsset = ChampionCatalog.FindById(championId);
                var baseHp = championAsset != null ? Mathf.Max(1, championAsset.BaseHp) : 40;
                _champions.Add(new ChampionCombatState(
                    championId,
                    DisplayNameFor(championId),
                    baseHp,
                    championAsset != null ? championAsset.Element : ElementType.Fire,
                    spellPool));
            }

            _activeChampionIndex = 0;
        }

        private static List<string> BuildRunSpellPool(
            string championId,
            int championIndex,
            GameFlowManager manager,
            IReadOnlyDictionary<string, CombatSpellAsset> spellIndex)
        {
            if (manager == null)
            {
                return BuildMockHand(championIndex)
                    .Select(card => card.SpellId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();
            }

            var loadout = manager.GetChampionSpellLoadout(championId);
            if (loadout == null || loadout.Count == 0)
            {
                return BuildMockHand(championIndex)
                    .Select(card => card.SpellId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();
            }

            var spellPool = new List<string>();
            foreach (var spellId in loadout)
            {
                if (string.IsNullOrWhiteSpace(spellId))
                {
                    continue;
                }

                if (!spellIndex.TryGetValue(spellId, out var spellAsset) || spellAsset == null)
                {
                    continue;
                }

                spellPool.Add(spellAsset.SpellId);
            }

            return spellPool.Count > 0
                ? spellPool
                : BuildMockHand(championIndex)
                    .Select(card => card.SpellId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();
        }

        private static string DisplayNameFor(string championId)
        {
            var match = ChampionCatalog.All.FirstOrDefault(c => c.Id == championId);
            return string.IsNullOrEmpty(match.DisplayName) ? championId : match.DisplayName;
        }

        private static List<CardDefinition> BuildMockHand(int championIndex)
        {
            if (championIndex == 0)
            {
                return new List<CardDefinition>
                {
                    new CardDefinition("Bulwark Strike", Cost(ElementType.Ground, 2), new CardEffect(CardEffectType.Damage, 10), "mock_bulwark_strike"),
                    new CardDefinition("Iron Guard", Cost(ElementType.Ground, 1, ElementType.Fire, 1), new CardEffect(CardEffectType.Shield, 9), "mock_iron_guard"),
                    new CardDefinition("Shield Pulse", Cost(ElementType.Water, 1), new CardEffect(CardEffectType.Shield, 6), "mock_shield_pulse"),
                    new CardDefinition("Stand Fast", Cost(ElementType.Nature, 1), new CardEffect(CardEffectType.Heal, 6), "mock_stand_fast")
                };
            }

            if (championIndex == 1)
            {
                return new List<CardDefinition>
                {
                    new CardDefinition("Arc Bolt", Cost(ElementType.Mystic, 2), new CardEffect(CardEffectType.Damage, 12), "mock_arc_bolt"),
                    new CardDefinition("Frost Sigil", Cost(ElementType.Water, 2), new CardEffect(CardEffectType.Shield, 7), "mock_frost_sigil"),
                    new CardDefinition("Flame Vortex", Cost(ElementType.Fire, 1, ElementType.Mystic, 1), new CardEffect(CardEffectType.Damage, 14), "mock_flame_vortex"),
                    new CardDefinition("Mana Weave", Cost(ElementType.Water, 1, ElementType.Nature, 1), new CardEffect(CardEffectType.Heal, 8), "mock_mana_weave")
                };
            }

            return new List<CardDefinition>
            {
                new CardDefinition("Piercing Shot", Cost(ElementType.Poison, 1, ElementType.Fire, 1), new CardEffect(CardEffectType.Damage, 11), "mock_piercing_shot"),
                new CardDefinition("Tidal Arrow", Cost(ElementType.Water, 2), new CardEffect(CardEffectType.Damage, 10), "mock_tidal_arrow"),
                new CardDefinition("Hunter Mark", Cost(ElementType.Ground, 1), new CardEffect(CardEffectType.Shield, 5), "mock_hunter_mark"),
                new CardDefinition("Swift Volley", Cost(ElementType.Poison, 1, ElementType.Mystic, 1), new CardEffect(CardEffectType.Heal, 5), "mock_swift_volley")
            };
        }

        private static CardCost Cost(params object[] pairs)
        {
            var cost = new Dictionary<ElementType, int>();
            for (var i = 0; i < pairs.Length; i += 2)
            {
                var element = (ElementType)pairs[i];
                var value = (int)pairs[i + 1];
                cost[element] = value;
            }

            return new CardCost(cost);
        }

        private void StartTurn()
        {
            _rerollsRemaining = MaxRerollsPerTurn;
            _playedCardsThisTurn.Clear();
            _enemyRerollsRemaining = MaxRerollsPerTurn;
            _enemyUsedIntentsThisTurn.Clear();
            foreach (var champion in _champions)
            {
                champion.ResetBlock();
            }
            EnsureGemSlots();
            RollAllGems(GetPlayerPrimaryElement());
            RollEnemyGems(GetEnemyPrimaryElement());
            DrawHandsForTurn();
            _lastAction = "New turn started";
        }

        private void DrawHandsForTurn()
        {
            var spellIndex = GetSpellIndex();
            foreach (var champion in _champions)
            {
                if (champion == null)
                {
                    continue;
                }

                champion.ReplaceHand(BuildHandForTurn(champion, spellIndex));
            }
        }

        private List<CardDefinition> BuildHandForTurn(ChampionCombatState champion, IReadOnlyDictionary<string, CombatSpellAsset> spellIndex)
        {
            var hand = new List<CardDefinition>();
            if (champion == null || champion.AvailableSpellIds == null || champion.AvailableSpellIds.Count == 0)
            {
                return hand;
            }

            var availablePool = champion.AvailableSpellIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            while (hand.Count < CardsDrawnPerTurn && availablePool.Count > 0)
            {
                var drawIndex = _rng.Next(0, availablePool.Count);
                var spellId = availablePool[drawIndex];
                availablePool.RemoveAt(drawIndex);

                if (!spellIndex.TryGetValue(spellId, out var spellAsset) || spellAsset == null)
                {
                    continue;
                }

                hand.Add(spellAsset.ToCardDefinition());
            }

            return hand;
        }

        public bool TryGetChampionHandSpellIds(string championId, out List<string> spellIds)
        {
            spellIds = new List<string>();
            if (string.IsNullOrWhiteSpace(championId))
            {
                return false;
            }

            var champion = _champions.FirstOrDefault(entry => entry != null && entry.Id == championId);
            if (champion == null || champion.Hand == null)
            {
                return false;
            }

            spellIds.AddRange(champion.Hand
                .Where(card => card != null && !string.IsNullOrWhiteSpace(card.SpellId))
                .Select(card => card.SpellId));

            return true;
        }

        private void RollAllGems(ElementType primaryElement)
        {
            EnsureGemSlots();

            var elements = new List<ElementType>
            {
                primaryElement,
                primaryElement,
                GetRandomElement(primaryElement),
                GetRandomElement(primaryElement)
            };

            for (var i = elements.Count - 1; i > 0; i--)
            {
                var swapIndex = _rng.Next(0, i + 1);
                var temp = elements[i];
                elements[i] = elements[swapIndex];
                elements[swapIndex] = temp;
            }

            for (var i = 0; i < _gems.Count && i < elements.Count; i++)
            {
                _gems[i].ResetForTurn(elements[i]);
            }
        }

        private bool CanPlay(CardDefinition card)
        {
            if (_fightResolved || _champions.Count == 0)
            {
                return false;
            }

            var active = _champions[_activeChampionIndex];
            return active.IsAlive
                && !_playedCardsThisTurn.Contains(card)
                && card.Cost.CanAfford(CountAvailableGems());
        }

        private Dictionary<ElementType, int> CountAvailableGems()
        {
            var result = CreateElementCountMap();

            foreach (var gem in _gems)
            {
                if (gem.IsAvailable)
                {
                    result[gem.Element]++;
                }
            }

            return result;
        }

        private Dictionary<ElementType, int> CountUnavailableGems()
        {
            var result = CreateElementCountMap();

            foreach (var gem in _gems)
            {
                if (!gem.IsAvailable)
                {
                    result[gem.Element]++;
                }
            }

            return result;
        }

        private void RollEnemyGems(ElementType primaryElement)
        {
            _enemyGems.Clear();

            var elements = new List<ElementType>
            {
                primaryElement,
                primaryElement,
                GetRandomElement(),
                GetRandomElement()
            };

            for (var i = elements.Count - 1; i > 0; i--)
            {
                var swapIndex = _rng.Next(0, i + 1);
                var temp = elements[i];
                elements[i] = elements[swapIndex];
                elements[swapIndex] = temp;
            }

            for (var i = 0; i < elements.Count; i++)
            {
                _enemyGems.Add(new GemSlot(elements[i]));
            }
        }

        private Dictionary<ElementType, int> CountEnemyAvailableGems()
        {
            var result = CreateElementCountMap();

            foreach (var gem in _enemyGems)
            {
                if (gem.IsAvailable)
                {
                    result[gem.Element]++;
                }
            }

            return result;
        }

        private void RerollGems()
        {
            if (_fightResolved)
            {
                _lastAction = "Cannot reroll: fight is already resolved";
                return;
            }

            if (_rerollsRemaining <= 0)
            {
                _lastAction = "No rerolls remaining";
                return;
            }

            _rerollsRemaining--;
            var primaryElement = GetPlayerPrimaryElement();
            foreach (var gem in _gems)
            {
                if (gem.IsLocked)
                {
                    continue;
                }

                gem.Reroll(GetRandomElement(primaryElement));
            }

            _lastAction = "Rerolled unlocked runes";
        }

        public bool RequestRefreshRunes()
        {
            var before = _rerollsRemaining;
            RerollGems();
            return _rerollsRemaining != before;
        }

        public bool TryToggleRuneLock(int index)
        {
            if (_fightResolved || index < 0 || index >= _gems.Count)
            {
                return false;
            }

            var gem = _gems[index];
            var isLocked = gem.ToggleLock();
            _lastAction = "Rune " + (index + 1) + " " + (isLocked ? "locked" : "unlocked");
            return true;
        }

        public bool TryGetRuneState(int index, out ElementType element, out bool isLocked, out bool isAvailable)
        {
            element = default;
            isLocked = false;
            isAvailable = false;

            if (index < 0 || index >= _gems.Count)
            {
                return false;
            }

            var gem = _gems[index];
            element = gem.Element;
            isLocked = gem.IsLocked;
            isAvailable = gem.IsAvailable;
            return true;
        }

        private void EnsureGemSlots()
        {
            if (_gems.Count > GemsPerTurn)
            {
                _gems.RemoveRange(GemsPerTurn, _gems.Count - GemsPerTurn);
            }

            while (_gems.Count < GemsPerTurn)
            {
                _gems.Add(new GemSlot(GetRandomElement()));
            }
        }

        private ElementType GetRandomElement()
        {
            return AllElements[_rng.Next(0, AllElements.Length)];
        }

        private static Dictionary<ElementType, int> CreateElementCountMap()
        {
            var result = new Dictionary<ElementType, int>();
            foreach (var element in AllElements)
            {
                result[element] = 0;
            }

            return result;
        }

        private void EndTurn()
        {
            if (_fightResolved)
            {
                _lastAction = "Cannot end turn: fight is already resolved";
                return;
            }

            ExecuteEnemyTurn();
            if (TryResolveFight())
            {
                return;
            }

            _turn++;
            StartTurn();
        }

        private void SwitchChampion(int index)
        {
            if (_fightResolved)
            {
                _lastAction = "Cannot switch champion: fight is already resolved";
                return;
            }

            if (index < 0 || index >= _champions.Count)
            {
                return;
            }

            if (!_champions[index].IsAlive)
            {
                _lastAction = _champions[index].DisplayName + " is down and cannot be active";
                return;
            }

            _activeChampionIndex = index;
            _lastAction = "Switched to " + _champions[index].DisplayName;
        }

        public bool TrySetActiveChampion(string championId)
        {
            if (_fightResolved || string.IsNullOrWhiteSpace(championId))
            {
                return false;
            }

            for (var i = 0; i < _champions.Count; i++)
            {
                var champion = _champions[i];
                if (champion == null || !champion.IsAlive)
                {
                    continue;
                }

                if (!string.Equals(champion.Id, championId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                SwitchChampion(i);
                return true;
            }

            return false;
        }

        public bool TryPlaySpellById(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return false;
            }

            if (_fightResolved || _champions.Count == 0)
            {
                return false;
            }

            var spell = FindSpellById(spellId);
            if (spell == null)
            {
                _lastAction = "Unknown spell: " + spellId;
                return false;
            }

            var active = GetActiveAliveChampion();
            if (active == null)
            {
                _lastAction = "No active champion can cast " + spell.DisplayName;
                return false;
            }

            var card = active.Hand.FirstOrDefault(handCard =>
                handCard != null && string.Equals(handCard.SpellId, spell.SpellId, System.StringComparison.Ordinal));
            if (card == null)
            {
                _lastAction = active.DisplayName + " does not have " + spell.DisplayName + " in hand";
                return false;
            }

            return PlayCard(card, spellId);
        }

        private bool PlayCard(CardDefinition card, string spellId = null)
        {
            if (_fightResolved)
            {
                _lastAction = "Cannot play cards: fight is already resolved";
                return false;
            }

            var active = _champions[_activeChampionIndex];
            if (!active.IsAlive)
            {
                _lastAction = "Active champion is down";
                return false;
            }

            if (!CanPlay(card))
            {
                if (_playedCardsThisTurn.Contains(card))
                {
                    _lastAction = card.Name + " is already used this turn";
                }
                else
                {
                    _lastAction = "Cannot play " + card.Name + " (missing gems)";
                }
                return false;
            }

            ApplyCardEffect(active, card, ResolveSpellAsset(card, spellId));
            ConsumeGems(card.Cost);
            _playedCardsThisTurn.Add(card);

            ConsumePlayedSpell(active, card, spellId);

            if (TryResolveFight())
            {
                return true;
            }

            return true;
        }

        private void ApplyCardEffect(ChampionCombatState source, CardDefinition card, CombatSpellAsset spell)
        {
            var spellElement = ResolveSpellElement(spell);
            if (spell != null && spell.EffectLines != null && spell.EffectLines.Count > 0)
            {
                ApplySpellEffectLines(source, card, spell, spellElement);
                return;
            }

            ApplyCardEffectFallback(source, card, spellElement);
        }

        private CombatSpellAsset ResolveSpellAsset(CardDefinition card, string spellId)
        {
            var resolvedSpellId = !string.IsNullOrWhiteSpace(spellId)
                ? spellId
                : card != null ? card.SpellId : null;

            return FindSpellById(resolvedSpellId);
        }

        private void ApplySpellEffectLines(ChampionCombatState source, CardDefinition card, CombatSpellAsset spell, ElementType? spellElement)
        {
            var effectResults = new List<string>();
            var rawDamage = 0;
            foreach (var line in spell.EffectLines)
            {
                if (line == null || line.amount <= 0)
                {
                    continue;
                }

                switch (line.kind)
                {
                    case SpellEffectKind.Deal:
                        rawDamage += line.amount;
                        break;
                    case SpellEffectKind.Heal:
                        var healed = source.Heal(line.amount);
                        effectResults.Add(healed + " heal");
                        break;
                    case SpellEffectKind.Shield:
                        source.AddBlock(line.amount);
                        effectResults.Add(line.amount + " shield");
                        break;
                    case SpellEffectKind.Burn:
                        if (_enemy != null)
                        {
                            var appliedBurn = _enemy.AddBurn(line.amount, line.duration, line.statusIconResource);
                            effectResults.Add(appliedBurn + " burn");
                        }
                        break;
                }
            }

            if (rawDamage > 0)
            {
                var dealt = DealDamageToEnemy(rawDamage, spellElement);
                effectResults.Insert(0, dealt + " damage");
            }

            if (effectResults.Count == 0)
            {
                ApplyCardEffectFallback(source, card, spellElement);
                return;
            }

            _lastAction = source.DisplayName + " played " + card.Name + " and applied " + string.Join(", ", effectResults);
        }

        private void ApplyCardEffectFallback(ChampionCombatState source, CardDefinition card, ElementType? spellElement)
        {
            switch (card.Effect.Type)
            {
                case CardEffectType.Damage:
                    var dealt = DealDamageToEnemy(card.Effect.Amount, spellElement);
                    _lastAction = source.DisplayName + " played " + card.Name + " and dealt " + dealt + " damage";
                    break;
                case CardEffectType.Shield:
                    source.AddBlock(card.Effect.Amount);
                    _lastAction = source.DisplayName + " played " + card.Name + " and gained " + card.Effect.Amount + " block";
                    break;
                case CardEffectType.Heal:
                    var healed = source.Heal(card.Effect.Amount);
                    _lastAction = source.DisplayName + " played " + card.Name + " and healed " + healed + " HP";
                    break;
            }
        }

        private void ConsumePlayedSpell(ChampionCombatState active, CardDefinition card, string spellId)
        {
            if (active == null || card == null)
            {
                return;
            }

            if (active.Hand != null)
            {
                active.Hand.Remove(card);
            }
        }

        private int DealDamageToEnemy(int rawDamage, ElementType? sourceElement = null)
        {
            if (_enemy == null)
            {
                return 0;
            }

            return _enemy.TakeDamage(rawDamage, sourceElement);
        }

        private void ConsumeGems(CardCost cost)
        {
            foreach (var requirement in cost.Requirements)
            {
                var toConsume = requirement.Value;
                for (var i = 0; i < _gems.Count && toConsume > 0; i++)
                {
                    var gem = _gems[i];
                    if (gem.Element != requirement.Key || !gem.IsAvailable)
                    {
                        continue;
                    }

                    gem.Reload(GetRandomElement(GetPlayerPrimaryElement()));
                    toConsume--;
                }
            }
        }

        private void ConsumeEnemyGems(CardCost cost)
        {
            foreach (var requirement in cost.Requirements)
            {
                var toConsume = requirement.Value;
                for (var i = 0; i < _enemyGems.Count && toConsume > 0; i++)
                {
                    var gem = _enemyGems[i];
                    if (gem.Element != requirement.Key || !gem.IsAvailable)
                    {
                        continue;
                    }

                    gem.Reload(GetRandomElement(GetEnemyPrimaryElement()));
                    toConsume--;
                }
            }
        }

        private void ExecuteEnemyTurn()
        {
            if (_enemy == null || !_enemy.IsAlive)
            {
                return;
            }

            _enemy.BeginTurn();
            _enemy.ResetBlock();
            var actions = new List<string>();
            var maxActions = _enemy.Definition.Intents.Count;

            for (var actionIndex = 0; actionIndex < maxActions; actionIndex++)
            {
                if (_champions.All(champion => !champion.IsAlive))
                {
                    break;
                }

                if (!TryChooseEnemyAction(out var chosenIntent, out var chosenTarget))
                {
                    if (_enemyRerollsRemaining > 0)
                    {
                        _enemyRerollsRemaining--;
                        RollEnemyGems(GetEnemyPrimaryElement());
                        actions.Add(_enemy.Definition.DisplayName + " rerolls gems (" + _enemyRerollsRemaining + " left)");
                        continue;
                    }

                    actions.Add(_enemy.Definition.DisplayName + " cannot afford more intents");
                    break;
                }

                ConsumeEnemyGems(chosenIntent.Cost);
                _enemyUsedIntentsThisTurn.Add(chosenIntent);

                switch (chosenIntent.Effect.Type)
                {
                    case CardEffectType.Damage:
                        if (chosenTarget == null)
                        {
                            actions.Add(_enemy.Definition.DisplayName + " fails to find a target for " + chosenIntent.Name);
                            break;
                        }

                        var dealt = chosenTarget.TakeDamage(chosenIntent.Effect.Amount);
                        var damageLine = _enemy.Definition.DisplayName + " uses " + chosenIntent.Name + " on " + chosenTarget.DisplayName + " for " + dealt;
                        if (!chosenTarget.IsAlive)
                        {
                            damageLine += " (KO)";
                            TrySelectNextAliveChampion();
                        }

                        actions.Add(damageLine);
                        break;
                    case CardEffectType.Shield:
                        _enemy.AddBlock(chosenIntent.Effect.Amount);
                        actions.Add(_enemy.Definition.DisplayName + " uses " + chosenIntent.Name + " and gains " + chosenIntent.Effect.Amount + " block");
                        break;
                    case CardEffectType.Heal:
                        var healed = _enemy.Heal(chosenIntent.Effect.Amount);
                        actions.Add(_enemy.Definition.DisplayName + " uses " + chosenIntent.Name + " and heals " + healed);
                        break;
                }
            }

            _lastAction = actions.Count > 0
                ? string.Join(" | ", actions)
                : _enemy.Definition.DisplayName + " skips turn";
        }

        private bool TryChooseEnemyAction(out EnemyIntentDefinition bestIntent, out ChampionCombatState bestTarget)
        {
            bestIntent = null;
            bestTarget = null;

            var playable = GetPlayableEnemyIntents();
            if (playable.Count == 0)
            {
                return false;
            }

            var bestScore = float.MinValue;
            foreach (var intent in playable)
            {
                ChampionCombatState target = null;
                var score = ScoreEnemyIntent(intent, out target);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestIntent = intent;
                bestTarget = target;
            }

            return bestIntent != null;
        }

        private List<EnemyIntentDefinition> GetPlayableEnemyIntents()
        {
            if (_enemy == null || !_enemy.IsAlive)
            {
                return new List<EnemyIntentDefinition>();
            }

            var available = CountEnemyAvailableGems();
            return _enemy.Definition.Intents
                .Where(intent => !_enemyUsedIntentsThisTurn.Contains(intent) && intent.Cost.CanAfford(available))
                .ToList();
        }

        private float ScoreEnemyIntent(EnemyIntentDefinition intent, out ChampionCombatState target)
        {
            target = null;

            switch (intent.Effect.Type)
            {
                case CardEffectType.Damage:
                    target = SelectBestDamageTarget(intent.Effect.Amount);
                    if (target == null)
                    {
                        return -1000f;
                    }

                    var expectedDamage = EstimateDamageOnChampion(target, intent.Effect.Amount);
                    var killBonus = expectedDamage >= target.CurrentHp ? 40f : 0f;
                    var focusBonus = target == GetActiveAliveChampion() ? 5f : 0f;
                    return 30f + (expectedDamage * 2f) + killBonus + focusBonus;
                case CardEffectType.Shield:
                    var hpRatio = _enemy.MaxHp > 0 ? (float)_enemy.CurrentHp / _enemy.MaxHp : 1f;
                    var missingRatio = 1f - hpRatio;
                    var lowBlockBonus = _enemy.Block <= 4 ? 8f : 0f;
                    return 10f + intent.Effect.Amount + (missingRatio * 35f) + lowBlockBonus;
                case CardEffectType.Heal:
                    var missingHp = _enemy.MaxHp - _enemy.CurrentHp;
                    var effectiveHeal = missingHp > 0 ? Mathf.Min(intent.Effect.Amount, missingHp) : 0;
                    var emergencyBonus = _enemy.CurrentHp <= (_enemy.MaxHp * 0.35f) ? 20f : 0f;
                    return 12f + (effectiveHeal * 3f) + emergencyBonus;
                default:
                    return 0f;
            }
        }

        private ChampionCombatState SelectBestDamageTarget(int damageAmount)
        {
            var alive = _champions.Where(champion => champion.IsAlive).ToList();
            if (alive.Count == 0)
            {
                return null;
            }

            ChampionCombatState killTarget = null;
            var killTargetHp = int.MaxValue;
            foreach (var champion in alive)
            {
                var expectedDamage = EstimateDamageOnChampion(champion, damageAmount);
                if (expectedDamage < champion.CurrentHp)
                {
                    continue;
                }

                if (champion.CurrentHp >= killTargetHp)
                {
                    continue;
                }

                killTarget = champion;
                killTargetHp = champion.CurrentHp;
            }

            if (killTarget != null)
            {
                return killTarget;
            }

            ChampionCombatState best = null;
            var bestEffectiveHp = int.MaxValue;
            foreach (var champion in alive)
            {
                var effectiveHp = champion.CurrentHp + champion.Block;
                if (effectiveHp > bestEffectiveHp)
                {
                    continue;
                }

                if (effectiveHp == bestEffectiveHp && champion != GetActiveAliveChampion())
                {
                    continue;
                }

                bestEffectiveHp = effectiveHp;
                best = champion;
            }

            return best;
        }

        private int EstimateDamageOnChampion(ChampionCombatState champion, int rawDamage)
        {
            if (champion == null || !champion.IsAlive || rawDamage <= 0)
            {
                return 0;
            }

            var blocked = champion.Block < rawDamage ? champion.Block : rawDamage;
            return rawDamage - blocked;
        }

        private ChampionCombatState GetActiveAliveChampion()
        {
            if (_activeChampionIndex < 0 || _activeChampionIndex >= _champions.Count)
            {
                return null;
            }

            var active = _champions[_activeChampionIndex];
            return active.IsAlive ? active : null;
        }

        private void TrySelectNextAliveChampion()
        {
            var aliveIndex = _champions.FindIndex(champion => champion.IsAlive);
            if (aliveIndex >= 0)
            {
                _activeChampionIndex = aliveIndex;
            }
        }

        private bool TryResolveFight()
        {
            if (_fightResolved)
            {
                return true;
            }

            if (_enemy == null || !_enemy.IsAlive)
            {
                ResolveFight(true);
                return true;
            }

            if (_champions.All(champion => !champion.IsAlive))
            {
                ResolveFight(false);
                return true;
            }

            return false;
        }

        private void ResolveFight(bool victory)
        {
            if (_fightResolved)
            {
                return;
            }

            _fightResolved = true;
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            _lastAction = victory ? "Enemy defeated. Loading result..." : "Team defeated. Loading result...";
            manager.ShowResult(victory);
        }

        private void TrackCombatLog()
        {
            if (string.IsNullOrEmpty(_lastAction))
            {
                return;
            }

            var marker = _turn + "|" + _lastAction;
            if (_lastLoggedMarker == marker)
            {
                return;
            }

            _lastLoggedMarker = marker;
            _combatLog.Insert(0, "T" + _turn + " - " + _lastAction);
            if (_combatLog.Count > CombatLogMaxEntries)
            {
                _combatLog.RemoveAt(_combatLog.Count - 1);
            }
        }

        private EnemyBiome ResolveTargetBiome()
        {
            if (_debugBiomeOverride.HasValue)
            {
                return _debugBiomeOverride.Value;
            }

            var manager = GameFlowManager.Instance;
            var zoneIndex = manager != null ? manager.CurrentRun.zoneIndex : 0;
            return ResolveBiomeForRunZoneIndex(zoneIndex);
        }

        private int ResolveZoneCombatIndex(EnemyBiome biome)
        {
            var manager = GameFlowManager.Instance;
            var globalNodeIndex = manager != null ? manager.CurrentRun.boardNodeIndex : 0;
            var currentRunZoneIndex = manager != null ? Mathf.Max(0, manager.CurrentRun.zoneIndex) : 0;
            var previousZoneCombats = 0;

            for (var i = 0; i < currentRunZoneIndex; i++)
            {
                var previousBiome = ResolveBiomeForRunZoneIndex(i);
                previousZoneCombats += _spawnRules != null
                    ? _spawnRules.GetCombatsPerZone(previousBiome, DefaultCombatsPerZone)
                    : DefaultCombatsPerZone;
            }

            var localCombatIndex = globalNodeIndex - previousZoneCombats;
            return localCombatIndex < 0 ? 0 : localCombatIndex;
        }

        private bool IsBossFight(EnemyBiome biome, int zoneCombatIndex)
        {
            var manager = GameFlowManager.Instance;
            if (manager != null && manager.IsNextFightBoss())
            {
                return true;
            }

            var combatsPerZone = _spawnRules != null
                ? _spawnRules.GetCombatsPerZone(biome, DefaultCombatsPerZone)
                : DefaultCombatsPerZone;
            return zoneCombatIndex >= combatsPerZone - 1;
        }

        private void ResolveSpawnContext(out EnemyBiome biome, out int zoneCombatIndex, out bool isBossFight)
        {
            biome = ResolveTargetBiome();
            zoneCombatIndex = ResolveZoneCombatIndex(biome);
            isBossFight = IsBossFight(biome, zoneCombatIndex);
        }

        private EnemyTier ResolveTargetTier(EnemyBiome biome, int zoneCombatIndex, bool isBossFight)
        {
            if (_debugTierOverride.HasValue)
            {
                return _debugTierOverride.Value;
            }

            if (isBossFight)
            {
                return EnemyTier.Boss;
            }

            if (_spawnRules != null && _spawnRules.TryGetTierWeights(biome, zoneCombatIndex, out var weights))
            {
                return RollTierByWeights(weights);
            }

            Debug.LogWarning("No valid spawn tier rule for biome " + biome + " at zone index " + zoneCombatIndex + ". Using fallback tier.");
            switch (biome)
            {
                case EnemyBiome.Zone1:
                    return EnemyTier.Minion;
                case EnemyBiome.Zone2:
                    return EnemyTier.Elite;
                case EnemyBiome.Zone3:
                    return EnemyTier.Champion;
                default:
                    return EnemyTier.Minion;
            }
        }

        private EnemyTier RollTierByWeights(List<EnemyTierWeightEntry> weights)
        {
            var filtered = weights
                .Where(entry => entry != null && entry.weight > 0 && entry.tier != EnemyTier.Apex)
                .ToList();
            if (filtered.Count == 0)
            {
                return EnemyTier.Minion;
            }

            var total = filtered.Sum(entry => entry.weight);
            if (total <= 0)
            {
                return EnemyTier.Minion;
            }

            var roll = _rng.Next(1, total + 1);
            var cumulative = 0;
            foreach (var entry in filtered)
            {
                cumulative += entry.weight;
                if (roll <= cumulative)
                {
                    return entry.tier;
                }
            }

            return filtered[filtered.Count - 1].tier;
        }

        private void CycleBiomeOverride()
        {
            if (!_debugBiomeOverride.HasValue)
            {
                _debugBiomeOverride = EnemyBiome.Zone1;
                return;
            }

            if (_debugBiomeOverride.Value == EnemyBiome.Zone1)
            {
                _debugBiomeOverride = EnemyBiome.Zone2;
                return;
            }

            if (_debugBiomeOverride.Value == EnemyBiome.Zone2)
            {
                _debugBiomeOverride = EnemyBiome.Zone3;
                return;
            }

            _debugBiomeOverride = null;
        }

        private EnemyBiome ResolveBiomeForRunZoneIndex(int runZoneIndex)
        {
            var registry = RuntimeAssetRegistryAsset.Load();
            var runLoopConfig = registry != null ? registry.RunLoopConfig : null;
            var zoneId = runLoopConfig != null
                ? runLoopConfig.GetZoneIdForRunIndex(runZoneIndex, runZoneIndex + 1)
                : runZoneIndex + 1;

            switch (zoneId)
            {
                case 1:
                    return EnemyBiome.Zone1;
                case 2:
                    return EnemyBiome.Zone2;
                case 3:
                    return EnemyBiome.Zone3;
                default:
                    return EnemyBiome.Zone1;
            }
        }

        private void CycleTierOverride()
        {
            if (!_debugTierOverride.HasValue)
            {
                _debugTierOverride = EnemyTier.Minion;
                return;
            }

            switch (_debugTierOverride.Value)
            {
                case EnemyTier.Minion:
                    _debugTierOverride = EnemyTier.Elite;
                    return;
                case EnemyTier.Elite:
                    _debugTierOverride = EnemyTier.Champion;
                    return;
                case EnemyTier.Champion:
                    _debugTierOverride = EnemyTier.Boss;
                    return;
                case EnemyTier.Boss:
                    _debugTierOverride = EnemyTier.Apex;
                    return;
                default:
                    _debugTierOverride = null;
                    return;
            }
        }

        private void CycleEnemyOverride()
        {
            if (_availableEnemies.Count == 0)
            {
                BuildEnemyDefinitions();
            }

            var ids = _availableEnemies.Select(enemy => enemy.Id).Distinct().OrderBy(id => id).ToList();
            if (ids.Count == 0)
            {
                _debugEnemyIdOverride = null;
                return;
            }

            if (string.IsNullOrEmpty(_debugEnemyIdOverride))
            {
                _debugEnemyIdOverride = ids[0];
                return;
            }

            var index = ids.IndexOf(_debugEnemyIdOverride);
            if (index < 0 || index >= ids.Count - 1)
            {
                _debugEnemyIdOverride = null;
                return;
            }

            _debugEnemyIdOverride = ids[index + 1];
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

        private ElementType GetPlayerPrimaryElement()
        {
            var active = GetActiveAliveChampion();
            return active != null ? active.Element : ElementType.Fire;
        }

        private ElementType GetEnemyPrimaryElement()
        {
            if (_enemy != null && _enemy.Definition != null)
            {
                return _enemy.Definition.TypeDefinition != null
                    ? _enemy.Definition.TypeDefinition.Element
                    : ElementType.Fire;
            }

            return ElementType.Fire;
        }

        private static ElementType? ResolveSpellElement(CombatSpellAsset spell)
        {
            if (spell == null)
            {
                return null;
            }

            return spell.TryGetPrimaryElement(out var element) ? element : (ElementType?)null;
        }

        private ElementType GetRandomElement(ElementType preferredElement)
        {
            return _rng.NextDouble() < 0.10d ? preferredElement : GetRandomElement();
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

        public string DebugBiomeOverrideLabel()
        {
            return _debugBiomeOverride.HasValue ? _debugBiomeOverride.Value.ToString() : "Auto";
        }

        public string DebugTierOverrideLabel()
        {
            return _debugTierOverride.HasValue ? _debugTierOverride.Value.ToString() : "Auto";
        }

        public string DebugEnemyOverrideLabel()
        {
            return string.IsNullOrEmpty(_debugEnemyIdOverride) ? "Auto" : _debugEnemyIdOverride;
        }

        public void DebugCycleBiomeOverride()
        {
            CycleBiomeOverride();
        }

        public void DebugCycleTierOverride()
        {
            CycleTierOverride();
        }

        public void DebugCycleEnemyOverride()
        {
            CycleEnemyOverride();
        }

        public void DebugRespawnEnemyNow()
        {
            _fightResolved = false;
            _enemy = new EnemyCombatState(SelectEnemyDefinition());
            _turn = 1;
            _combatLog.Clear();
            _lastLoggedMarker = null;
            StartTurn();
            _lastAction = "Debug respawn enemy: " + _enemy.Definition.DisplayName;
        }

        public void DebugOneShotEnemyNow()
        {
            if (_fightResolved || _enemy == null)
            {
                return;
            }

            var damage = Mathf.Max(1, _enemy.CurrentHp + _enemy.Block);
            _enemy.TakeDamage(damage);
            _lastAction = "Debug OS: enemy defeated";
            TryResolveFight();
        }

        public void RequestEndTurn()
        {
            EndTurn();
        }

        private void OnGUI()
        {
            if (GameObject.Find(DisableDebugUiMarkerName) != null)
            {
                return;
            }

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
                if (_champions.Count == 0)
                {
                    GUILayout.BeginArea(new Rect(safeArea.x + 20f, safeArea.y + 20f, 460f, 120f), GUI.skin.box);
                    GUILayout.Label("Fight setup missing: no champions selected.");
                    if (GUILayout.Button("Back Home"))
                    {
                        manager.GoToHome();
                    }

                    GUILayout.EndArea();
                    return;
                }

                if (_enemy == null)
                {
                    return;
                }

                var active = _champions[_activeChampionIndex];
                const float panelWidth = 610f;
                const float panelHeight = 620f;
                var panelX = safeArea.x + safeArea.width - panelWidth - 18f;
                var panelY = safeArea.y + 18f;

                GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), GUI.skin.box);
                GUILayout.Label("Fight");
                GUILayout.Label("Debug keys: F6 biome | F7 tier | F8 enemy");
                GUILayout.Label("Turn: " + _turn + " | Rerolls left: " + _rerollsRemaining + " / " + MaxRerollsPerTurn);
                GUILayout.Label("Enemy: " + _enemy.Definition.DisplayName + " | HP: " + _enemy.CurrentHp + " / " + _enemy.MaxHp + " | Block: " + _enemy.Block);
                if (_debugBiomeOverride.HasValue || _debugTierOverride.HasValue || !string.IsNullOrEmpty(_debugEnemyIdOverride))
                {
                    var biomeOverrideText = _debugBiomeOverride.HasValue ? _debugBiomeOverride.Value.ToString() : "Auto";
                    var tierOverrideText = _debugTierOverride.HasValue ? _debugTierOverride.Value.ToString() : "Auto";
                    var enemyOverrideText = string.IsNullOrEmpty(_debugEnemyIdOverride) ? "Auto" : _debugEnemyIdOverride;
                    GUILayout.Label("Overrides: Biome=" + biomeOverrideText + " | Tier=" + tierOverrideText + " | Enemy=" + enemyOverrideText + " (F6/F7/F8)");
                }

                GUILayout.Space(8f);
                GUILayout.Label("Champions");
                GUILayout.BeginHorizontal();
                for (var i = 0; i < _champions.Count; i++)
                {
                    var isActive = i == _activeChampionIndex;
                    var champion = _champions[i];
                    var status = champion.IsAlive ? "HP " + champion.CurrentHp + "/" + champion.MaxHp + " | Block " + champion.Block : "KO";
                    var label = (isActive ? "[Active] " : "") + champion.DisplayName + " - " + status;
                    GUI.enabled = champion.IsAlive && !_fightResolved;
                    if (GUILayout.Button(label))
                    {
                        SwitchChampion(i);
                    }
                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(10f);
                GUILayout.Label("Gem Pool (gems stay available; only spells are consumed)");
                GUILayout.Label(string.Join(" | ", _gems.Select(FormatGemSlot)));
                GUILayout.Label("Available: " + FormatGemCounts(CountAvailableGems()));
                GUILayout.Label("Unavailable: " + FormatGemCounts(CountUnavailableGems()));

                GUILayout.BeginHorizontal();
                GUI.enabled = !_fightResolved;
                if (GUILayout.Button("Reroll Gems"))
                {
                    RerollGems();
                }

                if (GUILayout.Button("End Turn"))
                {
                    EndTurn();
                }
                GUI.enabled = true;

                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUI.enabled = !_fightResolved;
                if (GUILayout.Button("OS Enemy (Debug)", GUILayout.Height(34f)))
                {
                    DebugOneShotEnemyNow();
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.Space(10f);
                GUILayout.Label("Active Champion Hand: " + active.DisplayName);
                foreach (var card in active.Hand)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    var cardState = _playedCardsThisTurn.Contains(card) ? "USED" : "READY";
                    GUILayout.Label(card.Name + " | Cost: " + card.Cost.ToDisplayString() + " | Effect: " + FormatEffect(card.Effect) + " | " + cardState, GUILayout.Width(460f));

                    GUI.enabled = CanPlay(card);
                    if (GUILayout.Button("Play", GUILayout.Width(120f)))
                    {
                        PlayCard(card);
                    }

                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndArea();

                const float logWidth = 610f;
                const float logHeight = 210f;
                var logX = safeArea.x + safeArea.width - logWidth - 18f;
                var logY = safeArea.y + safeArea.height - logHeight - 18f;
                GUILayout.BeginArea(new Rect(logX, logY, logWidth, logHeight), GUI.skin.box);
                GUILayout.Label("Fight Log (latest first)");
                _combatLogScroll = GUILayout.BeginScrollView(_combatLogScroll, GUILayout.Height(170f));
                foreach (var line in _combatLog)
                {
                    GUILayout.Label("- " + line);
                }
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private static string FormatElement(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:
                    return "Fire";
                case ElementType.Nature:
                    return "Nature";
                case ElementType.Water:
                    return "Water";
                case ElementType.Poison:
                    return "Poison";
                case ElementType.Ground:
                    return "Ground";
                case ElementType.Mystic:
                    return "Mystic";
                default:
                    return "Unknown";
            }
        }

        private static string FormatGemCounts(IReadOnlyDictionary<ElementType, int> counts)
        {
            return string.Join(", ",
                counts.OrderBy(p => p.Key)
                    .Select(p => p.Value + " " + FormatElement(p.Key)));
        }

        private static string FormatGemSlot(GemSlot gem)
        {
            return FormatElement(gem.Element) + (gem.IsAvailable ? " [Ready]" : " [Used]");
        }

        private static string FormatEffect(CardEffect effect)
        {
            return effect.Type + " " + effect.Amount;
        }

        private class GemSlot
        {
            public GemSlot(ElementType element)
            {
                Element = element;
                IsAvailable = true;
            }

            public ElementType Element { get; private set; }
            public bool IsAvailable { get; set; }
            public bool IsLocked { get; private set; }

            public void ResetForTurn(ElementType element)
            {
                Element = element;
                IsAvailable = true;
                IsLocked = false;
            }

            public void Reroll(ElementType element)
            {
                if (IsLocked)
                {
                    return;
                }

                Element = element;
                IsAvailable = true;
            }

            public void Reload(ElementType element)
            {
                Element = element;
                IsAvailable = true;
                IsLocked = false;
            }

            public bool ToggleLock()
            {
                IsLocked = !IsLocked;
                return IsLocked;
            }
        }
    }
}
