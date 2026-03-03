using ProjectZ.Core;
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

        private readonly System.Random _rng = new System.Random();
        private readonly List<ElementType> _gems = new List<ElementType>();
        private readonly List<ChampionCombatState> _champions = new List<ChampionCombatState>();

        private int _activeChampionIndex;
        private int _turn = 1;
        private int _rerollsRemaining;
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
                    BuildMockHand(i)));
            }

            _activeChampionIndex = 0;
        }

        private static string DisplayNameFor(string championId)
        {
            var match = Run.ChampionCatalog.All.FirstOrDefault(c => c.Id == championId);
            return string.IsNullOrEmpty(match.DisplayName) ? championId : match.DisplayName;
        }

        private static List<CardDefinition> BuildMockHand(int championIndex)
        {
            if (championIndex == 0)
            {
                return new List<CardDefinition>
                {
                    new CardDefinition("Bulwark Strike", Cost(ElementType.Earth, 2)),
                    new CardDefinition("Iron Guard", Cost(ElementType.Earth, 1, ElementType.Fire, 1)),
                    new CardDefinition("Shield Pulse", Cost(ElementType.Water, 1)),
                    new CardDefinition("Meteor Step", Cost(ElementType.Fire, 2))
                };
            }

            if (championIndex == 1)
            {
                return new List<CardDefinition>
                {
                    new CardDefinition("Arc Bolt", Cost(ElementType.Air, 2)),
                    new CardDefinition("Frost Sigil", Cost(ElementType.Water, 2)),
                    new CardDefinition("Flame Vortex", Cost(ElementType.Fire, 1, ElementType.Air, 1)),
                    new CardDefinition("Mana Weave", Cost(ElementType.Water, 1, ElementType.Earth, 1))
                };
            }

            return new List<CardDefinition>
            {
                new CardDefinition("Piercing Shot", Cost(ElementType.Air, 1, ElementType.Fire, 1)),
                new CardDefinition("Tidal Arrow", Cost(ElementType.Water, 2)),
                new CardDefinition("Hunter Mark", Cost(ElementType.Earth, 1)),
                new CardDefinition("Swift Volley", Cost(ElementType.Air, 2))
            };
        }

        private static Dictionary<ElementType, int> Cost(params object[] pairs)
        {
            var cost = new Dictionary<ElementType, int>();
            for (var i = 0; i < pairs.Length; i += 2)
            {
                var element = (ElementType)pairs[i];
                var value = (int)pairs[i + 1];
                cost[element] = value;
            }

            return cost;
        }

        private void StartTurn()
        {
            _rerollsRemaining = MaxRerollsPerTurn;
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
            var counts = CountGems();
            foreach (var pair in card.Cost)
            {
                if (!counts.TryGetValue(pair.Key, out var available) || available < pair.Value)
                {
                    return false;
                }
            }

            return true;
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
            _turn++;
            StartTurn();
        }

        private void SwitchChampion(int index)
        {
            if (index < 0 || index >= _champions.Count)
            {
                return;
            }

            _activeChampionIndex = index;
            _lastAction = "Switched to " + _champions[index].DisplayName;
        }

        private void PlayCard(CardDefinition card)
        {
            if (!CanPlay(card))
            {
                _lastAction = "Cannot play " + card.Name + " (missing gems)";
                return;
            }

            _lastAction = "Played " + card.Name + " (gems not consumed)";
        }

        private void ResolveFight(bool victory)
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

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

            GUILayout.BeginArea(new Rect(18f, 18f, 610f, 540f), GUI.skin.box);
            GUILayout.Label("Fight - Prototype (Step 1)");
            GUILayout.Label("Turn: " + _turn + " | Rerolls left: " + _rerollsRemaining + " / " + MaxRerollsPerTurn);
            GUILayout.Label("Last action: " + _lastAction);

            GUILayout.Space(8f);
            GUILayout.Label("Champions (switch freely)");
            GUILayout.BeginHorizontal();
            for (var i = 0; i < _champions.Count; i++)
            {
                var isActive = i == _activeChampionIndex;
                var label = (isActive ? "[Active] " : "") + _champions[i].DisplayName;
                if (GUILayout.Button(label))
                {
                    SwitchChampion(i);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Gem Pool (not consumed on play)");
            GUILayout.Label(string.Join(" | ", _gems.Select(FormatElement)));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reroll Gems"))
            {
                RerollGems();
            }

            if (GUILayout.Button("End Turn"))
            {
                EndTurn();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Active Champion Hand: " + active.DisplayName);
            foreach (var card in active.Hand)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(card.Name + " | Cost: " + FormatCost(card.Cost), GUILayout.Width(430f));

                GUI.enabled = CanPlay(card);
                if (GUILayout.Button("Play", GUILayout.Width(120f)))
                {
                    PlayCard(card);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Temporary resolve buttons (until enemy AI is implemented)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Resolve Victory"))
            {
                ResolveFight(true);
            }

            if (GUILayout.Button("Resolve Defeat"))
            {
                ResolveFight(false);
            }

            GUILayout.EndHorizontal();
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

        private static string FormatCost(Dictionary<ElementType, int> cost)
        {
            return string.Join(", ",
                cost.OrderBy(p => p.Key)
                    .Select(p => p.Value + " " + FormatElement(p.Key)));
        }

        private enum ElementType
        {
            Fire,
            Water,
            Earth,
            Air
        }

        private class ChampionCombatState
        {
            public ChampionCombatState(string id, string displayName, List<CardDefinition> hand)
            {
                Id = id;
                DisplayName = displayName;
                Hand = hand;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public List<CardDefinition> Hand { get; }
        }

        private class CardDefinition
        {
            public CardDefinition(string name, Dictionary<ElementType, int> cost)
            {
                Name = name;
                Cost = cost;
            }

            public string Name { get; }
            public Dictionary<ElementType, int> Cost { get; }
        }
    }
}
