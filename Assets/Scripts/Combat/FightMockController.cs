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
        private const int GemsPerTurn = 5;
        private const int MaxRerollsPerTurn = 2;
        private const int CombatLogMaxEntries = 8;
        private const int DefaultCombatsPerZone = 8;
        private const string SpellLibraryResourcePath = "Combat/SpellLibrary";
        private const string EnemyCatalogResourcePath = "Combat/EnemyCatalog";
        private const string SpawnRulesResourcePath = "Combat/SpawnRules";

        private readonly System.Random _rng = new System.Random();
        private readonly List<GemSlot> _gems = new List<GemSlot>();
        private readonly List<GemSlot> _enemyGems = new List<GemSlot>();
        private readonly List<ChampionCombatState> _champions = new List<ChampionCombatState>();
        private readonly List<EnemyDefinition> _availableEnemies = new List<EnemyDefinition>();
        private readonly List<string> _combatLog = new List<string>();
        private readonly HashSet<CardDefinition> _playedCardsThisTurn = new HashSet<CardDefinition>();
        private readonly HashSet<EnemyIntentDefinition> _enemyUsedIntentsThisTurn = new HashSet<EnemyIntentDefinition>();

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

            var spellLibrary = Resources.Load<CombatSpellLibraryAsset>(SpellLibraryResourcePath);
            if (spellLibrary == null)
            {
                Debug.LogWarning("SpellLibrary missing at Resources/Combat/SpellLibrary");
            }

            var spellIndex = spellLibrary != null
                ? spellLibrary.BuildIndexById()
                : new Dictionary<string, CombatSpellAsset>();

            _spawnRules = Resources.Load<CombatSpawnRulesAsset>(SpawnRulesResourcePath);
            if (_spawnRules == null)
            {
                Debug.LogWarning("SpawnRules missing at Resources/Combat/SpawnRules");
            }

            var asset = Resources.Load<EnemyCatalogAsset>(EnemyCatalogResourcePath);
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
                Debug.LogWarning("EnemyCatalog missing at Resources/Combat/EnemyCatalog");
            }

            if (_availableEnemies.Count == 0)
            {
                _availableEnemies.AddRange(BuildFallbackEnemyCatalog());
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
                        new EnemyIntentDefinition("Refracted Ray", Cost(ElementType.Fire, 1, ElementType.Earth, 1), new CardEffect(CardEffectType.Damage, 9)),
                        new EnemyIntentDefinition("Heat Veil", Cost(ElementType.Earth, 2), new CardEffect(CardEffectType.Shield, 8)),
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
                        new EnemyIntentDefinition("Gritty Embrace", Cost(ElementType.Earth, 2), new CardEffect(CardEffectType.Damage, 12)),
                        new EnemyIntentDefinition("Quicksand Sink", Cost(ElementType.Earth, 3), new CardEffect(CardEffectType.Damage, 15)),
                        new EnemyIntentDefinition("Sandstorm Burst", Cost(ElementType.Earth, 2, ElementType.Fire, 1), new CardEffect(CardEffectType.Damage, 18))
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
                        new EnemyIntentDefinition("Solar Intake", Cost(ElementType.Fire, 2, ElementType.Earth, 1), new CardEffect(CardEffectType.Shield, 12)),
                        new EnemyIntentDefinition("Spike Eruption", Cost(ElementType.Fire, 2, ElementType.Earth, 2), new CardEffect(CardEffectType.Damage, 22))
                    }),
                new EnemyDefinition(
                    "static_skink",
                    "Bolt Runner",
                    35,
                    EnemyBiome.Zone1,
                    EnemyTier.Elite,
                    new List<EnemyIntentDefinition>
                    {
                        new EnemyIntentDefinition("Static Discharge", Cost(ElementType.Fire, 1), new CardEffect(CardEffectType.Damage, 7)),
                        new EnemyIntentDefinition("Overload", Cost(ElementType.Fire, 2), new CardEffect(CardEffectType.Damage, 6)),
                        new EnemyIntentDefinition("Sand Bolt", Cost(ElementType.Fire, 3, ElementType.Earth, 1), new CardEffect(CardEffectType.Damage, 25))
                    })
            };
        }

        private void BuildChampionStates()
        {
            _champions.Clear();

            var manager = GameFlowManager.Instance;
            var ids = manager != null ? manager.CurrentRun.selectedChampionIds : new List<string>();
            var sourceIds = ids.Count == 3 ? ids : new List<string> { "warden", "arcanist", "ranger" };

            for (var i = 0; i < sourceIds.Count; i++)
            {
                var championId = sourceIds[i];
                _champions.Add(new ChampionCombatState(
                    championId,
                    DisplayNameFor(championId),
                    40,
                    BuildMockHand(i)));
            }

            _activeChampionIndex = 0;
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
                    new CardDefinition("Bulwark Strike", Cost(ElementType.Earth, 2), new CardEffect(CardEffectType.Damage, 10)),
                    new CardDefinition("Iron Guard", Cost(ElementType.Earth, 1, ElementType.Fire, 1), new CardEffect(CardEffectType.Shield, 9)),
                    new CardDefinition("Shield Pulse", Cost(ElementType.Water, 1), new CardEffect(CardEffectType.Shield, 6)),
                    new CardDefinition("Stand Fast", Cost(ElementType.Earth, 1), new CardEffect(CardEffectType.Heal, 6))
                };
            }

            if (championIndex == 1)
            {
                return new List<CardDefinition>
                {
                    new CardDefinition("Arc Bolt", Cost(ElementType.Air, 2), new CardEffect(CardEffectType.Damage, 12)),
                    new CardDefinition("Frost Sigil", Cost(ElementType.Water, 2), new CardEffect(CardEffectType.Shield, 7)),
                    new CardDefinition("Flame Vortex", Cost(ElementType.Fire, 1, ElementType.Air, 1), new CardEffect(CardEffectType.Damage, 14)),
                    new CardDefinition("Mana Weave", Cost(ElementType.Water, 1, ElementType.Earth, 1), new CardEffect(CardEffectType.Heal, 8))
                };
            }

            return new List<CardDefinition>
            {
                new CardDefinition("Piercing Shot", Cost(ElementType.Air, 1, ElementType.Fire, 1), new CardEffect(CardEffectType.Damage, 11)),
                new CardDefinition("Tidal Arrow", Cost(ElementType.Water, 2), new CardEffect(CardEffectType.Damage, 10)),
                new CardDefinition("Hunter Mark", Cost(ElementType.Earth, 1), new CardEffect(CardEffectType.Shield, 5)),
                new CardDefinition("Swift Volley", Cost(ElementType.Air, 2), new CardEffect(CardEffectType.Heal, 5))
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
            RollAllGems();
            RollEnemyGems();
            _lastAction = "New turn started";
        }

        private void RollAllGems()
        {
            _gems.Clear();
            for (var i = 0; i < GemsPerTurn; i++)
            {
                _gems.Add(new GemSlot((ElementType)_rng.Next(0, 4)));
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
            var result = new Dictionary<ElementType, int>
            {
                { ElementType.Fire, 0 },
                { ElementType.Water, 0 },
                { ElementType.Earth, 0 },
                { ElementType.Air, 0 }
            };

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
            var result = new Dictionary<ElementType, int>
            {
                { ElementType.Fire, 0 },
                { ElementType.Water, 0 },
                { ElementType.Earth, 0 },
                { ElementType.Air, 0 }
            };

            foreach (var gem in _gems)
            {
                if (!gem.IsAvailable)
                {
                    result[gem.Element]++;
                }
            }

            return result;
        }

        private void RollEnemyGems()
        {
            _enemyGems.Clear();
            for (var i = 0; i < GemsPerTurn; i++)
            {
                _enemyGems.Add(new GemSlot((ElementType)_rng.Next(0, 4)));
            }
        }

        private Dictionary<ElementType, int> CountEnemyAvailableGems()
        {
            var result = new Dictionary<ElementType, int>
            {
                { ElementType.Fire, 0 },
                { ElementType.Water, 0 },
                { ElementType.Earth, 0 },
                { ElementType.Air, 0 }
            };

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
            RollAllGems();
            _lastAction = "Rerolled gems";
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

        private void PlayCard(CardDefinition card)
        {
            if (_fightResolved)
            {
                _lastAction = "Cannot play cards: fight is already resolved";
                return;
            }

            var active = _champions[_activeChampionIndex];
            if (!active.IsAlive)
            {
                _lastAction = "Active champion is down";
                return;
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
                return;
            }

            ApplyCardEffect(active, card);
            _playedCardsThisTurn.Add(card);

            ConsumeGems(card.Cost);

            if (TryResolveFight())
            {
                return;
            }
        }

        private void ApplyCardEffect(ChampionCombatState source, CardDefinition card)
        {
            switch (card.Effect.Type)
            {
                case CardEffectType.Damage:
                    var dealt = DealDamageToEnemy(card.Effect.Amount);
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

        private int DealDamageToEnemy(int rawDamage)
        {
            if (_enemy == null)
            {
                return 0;
            }

            return _enemy.TakeDamage(rawDamage);
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

                    gem.IsAvailable = false;
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

                    gem.IsAvailable = false;
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
                        RollEnemyGems();
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
            var nodeIndex = manager != null ? manager.CurrentRun.boardNodeIndex : 0;
            var zone1Combats = _spawnRules != null
                ? _spawnRules.GetCombatsPerZone(EnemyBiome.Zone1, DefaultCombatsPerZone)
                : DefaultCombatsPerZone;
            return nodeIndex < zone1Combats ? EnemyBiome.Zone1 : EnemyBiome.Zone2;
        }

        private int ResolveZoneCombatIndex(EnemyBiome biome)
        {
            var manager = GameFlowManager.Instance;
            var globalNodeIndex = manager != null ? manager.CurrentRun.boardNodeIndex : 0;

            var zone1Combats = _spawnRules != null
                ? _spawnRules.GetCombatsPerZone(EnemyBiome.Zone1, DefaultCombatsPerZone)
                : DefaultCombatsPerZone;

            if (biome == EnemyBiome.Zone1)
            {
                return globalNodeIndex < 0 ? 0 : globalNodeIndex;
            }

            var zone2Index = globalNodeIndex - zone1Combats;
            return zone2Index < 0 ? 0 : zone2Index;
        }

        private bool IsBossFight(EnemyBiome biome, int zoneCombatIndex)
        {
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
            return biome == EnemyBiome.Zone1 ? EnemyTier.Minion : EnemyTier.Champion;
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

            _debugBiomeOverride = null;
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

        private void OnGUI()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (_champions.Count == 0)
            {
                GUILayout.BeginArea(new Rect(20f, 20f, 460f, 120f), GUI.skin.box);
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
            var panelX = Screen.width - panelWidth - 18f;
            var panelY = 18f;

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
            GUILayout.Label("Gem Pool (used gems become inactive until reroll/new turn)");
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
            var logX = Screen.width - logWidth - 18f;
            var logY = Screen.height - logHeight - 18f;
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

        private static string FormatElement(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:
                    return "Fire";
                case ElementType.Water:
                    return "Water";
                case ElementType.Earth:
                    return "Earth";
                case ElementType.Air:
                    return "Air";
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

        private string FormatEnemyPreview()
        {
            if (!TryChooseEnemyAction(out var intent, out var target))
            {
                return "No playable intent";
            }

            var targetPart = target != null ? " | Target: " + target.DisplayName : string.Empty;
            return intent.Name + " | Cost: " + intent.Cost.ToDisplayString() + " | Effect: " + FormatEffect(intent.Effect) + targetPart;
        }

        private class GemSlot
        {
            public GemSlot(ElementType element)
            {
                Element = element;
                IsAvailable = true;
            }

            public ElementType Element { get; }
            public bool IsAvailable { get; set; }
        }
    }
}
