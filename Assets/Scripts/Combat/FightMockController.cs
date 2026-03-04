using ProjectZ.Core;
using ProjectZ.Run;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Combat
{
    public class FightMockController : MonoBehaviour
    {
        private const int GemsPerTurn = 5;
        private const int MaxRerollsPerTurn = 2;
        [SerializeField] private bool consumeGemsOnPlay;
        private const int EnemyMaxHp = 70;
        private const int EnemyAttackIntent = 8;

        private readonly System.Random _rng = new System.Random();
        private readonly List<ElementType> _gems = new List<ElementType>();
        private readonly List<ChampionCombatState> _champions = new List<ChampionCombatState>();

        private int _activeChampionIndex;
        private int _turn = 1;
        private int _rerollsRemaining;
        private int _enemyHp = EnemyMaxHp;
        private int _enemyBlock;
        private bool _fightResolved;
        private string _lastAction = "Fight started";

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
            _enemyHp = EnemyMaxHp;
            _enemyBlock = 0;
            _fightResolved = false;
            StartTurn();
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
            foreach (var champion in _champions)
            {
                champion.ResetBlock();
            }
            RollAllGems();
            _lastAction = "New turn started";
        }

        private void RollAllGems()
        {
            _gems.Clear();
            for (var i = 0; i < GemsPerTurn; i++)
            {
                _gems.Add((ElementType)_rng.Next(0, 4));
            }
        }

        private bool CanPlay(CardDefinition card)
        {
            if (_fightResolved || _champions.Count == 0)
            {
                return false;
            }

            var active = _champions[_activeChampionIndex];
            return active.IsAlive && card.Cost.CanAfford(CountGems());
        }

        private Dictionary<ElementType, int> CountGems()
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
                result[gem]++;
            }

            return result;
        }

        private void RerollGems()
        {
            if (_fightResolved)
            {
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
                return;
            }

            ResolveEnemyIntent();
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
                _lastAction = "Cannot play " + card.Name + " (missing gems)";
                return;
            }

            ApplyCardEffect(active, card);

            if (consumeGemsOnPlay)
            {
                ConsumeGems(card.Cost);
            }

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
            if (rawDamage <= 0 || _enemyHp <= 0)
            {
                return 0;
            }

            var damageAfterBlock = rawDamage;
            if (_enemyBlock > 0)
            {
                var absorbed = rawDamage < _enemyBlock ? rawDamage : _enemyBlock;
                _enemyBlock -= absorbed;
                damageAfterBlock -= absorbed;
            }

            if (damageAfterBlock <= 0)
            {
                return 0;
            }

            var previousHp = _enemyHp;
            _enemyHp -= damageAfterBlock;
            if (_enemyHp < 0)
            {
                _enemyHp = 0;
            }

            return previousHp - _enemyHp;
        }

        private void ConsumeGems(CardCost cost)
        {
            foreach (var requirement in cost.Requirements)
            {
                var toConsume = requirement.Value;
                for (var i = _gems.Count - 1; i >= 0 && toConsume > 0; i--)
                {
                    if (_gems[i] != requirement.Key)
                    {
                        continue;
                    }

                    _gems.RemoveAt(i);
                    toConsume--;
                }
            }
        }

        private void ResolveEnemyIntent()
        {
            var target = SelectEnemyTarget();
            if (target == null)
            {
                return;
            }

            var dealt = target.TakeDamage(EnemyAttackIntent);
            _lastAction = "Enemy intent hits " + target.DisplayName + " for " + dealt;

            if (!target.IsAlive)
            {
                _lastAction += " and knocks them out";
                TrySelectNextAliveChampion();
            }
        }

        private ChampionCombatState SelectEnemyTarget()
        {
            if (_champions.Count == 0)
            {
                return null;
            }

            if (_activeChampionIndex >= 0 && _activeChampionIndex < _champions.Count && _champions[_activeChampionIndex].IsAlive)
            {
                return _champions[_activeChampionIndex];
            }

            return _champions.FirstOrDefault(champion => champion.IsAlive);
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

            if (_enemyHp <= 0)
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

            var active = _champions[_activeChampionIndex];
            const float panelWidth = 610f;
            const float panelHeight = 620f;
            var panelX = Screen.width - panelWidth - 18f;
            var panelY = 18f;

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), GUI.skin.box);
            GUILayout.Label("Fight - Step 2");
            GUILayout.Label("Turn: " + _turn + " | Rerolls left: " + _rerollsRemaining + " / " + MaxRerollsPerTurn);
            GUILayout.Label("Enemy HP: " + _enemyHp + " / " + EnemyMaxHp + " | Enemy Block: " + _enemyBlock);
            GUILayout.Label("Enemy Intent: Attack " + EnemyAttackIntent + " on End Turn");
            GUILayout.Label("Last action: " + _lastAction);

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
            GUILayout.Label("Gem Pool (" + (consumeGemsOnPlay ? "consumed on play" : "not consumed on play") + ")");
            GUILayout.Label(string.Join(" | ", _gems.Select(FormatElement)));
            GUILayout.Label("Counts: " + FormatGemCounts(CountGems()));

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
                GUILayout.Label(card.Name + " | Cost: " + card.Cost.ToDisplayString() + " | Effect: " + FormatEffect(card.Effect), GUILayout.Width(460f));

                GUI.enabled = CanPlay(card);
                if (GUILayout.Button("Play", GUILayout.Width(120f)))
                {
                    PlayCard(card);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
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

        private static string FormatEffect(CardEffect effect)
        {
            return effect.Type + " " + effect.Amount;
        }
    }
}
