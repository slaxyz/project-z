using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectZ.Combat
{
    [CreateAssetMenu(fileName = "CombatSpell", menuName = "Project Z/Combat/Spell")]
    public class CombatSpellAsset : ScriptableObject
    {
        [SerializeField] private string spellId = "spell_id";
        [SerializeField] private string displayName = "Spell";
        [SerializeField] private CardEffectType effectType = CardEffectType.Damage;
        [SerializeField] private int value = 0;
        [SerializeField] private List<SpellEffectLineDefinition> effectLines = new List<SpellEffectLineDefinition>();
        [SerializeField] private List<ElementCostEntry> costs = new List<ElementCostEntry>();
        [SerializeField] private bool isEnemyUsable = true;
        [SerializeField] private List<string> tags = new List<string>();

        public string SpellId => spellId;
        public string DisplayName => displayName;
        public bool IsEnemyUsable => isEnemyUsable;
        public CardEffectType EffectType => effectType;
        public int Value => value;
        public IReadOnlyList<SpellEffectLineDefinition> EffectLines => effectLines;
        public IReadOnlyList<ElementCostEntry> CostEntries => costs;
        public int CostEntriesCount => costs != null ? costs.Count : 0;

        public bool TryGetPrimaryElement(out ElementType element)
        {
            element = default;
            if (costs == null || costs.Count == 0)
            {
                return false;
            }

            var firstCost = costs.Where(cost => cost != null && cost.amount > 0).FirstOrDefault();
            if (firstCost == null)
            {
                return false;
            }

            foreach (var cost in costs)
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

            element = firstCost.element;
            return true;
        }

        public bool IsValidForEnemy()
        {
            return !string.IsNullOrWhiteSpace(spellId)
                && !string.IsNullOrWhiteSpace(displayName)
                && value >= 0
                && costs != null
                && costs.Count > 0
                && costs.All(cost => cost != null && cost.amount > 0)
                && isEnemyUsable;
        }

        public EnemyIntentDefinition ToEnemyIntentDefinition(string intentLabelOverride = null)
        {
            var runtimeCost = new Dictionary<ElementType, int>();
            foreach (var cost in costs.Where(cost => cost != null && cost.amount > 0))
            {
                if (runtimeCost.ContainsKey(cost.element))
                {
                    runtimeCost[cost.element] += cost.amount;
                }
                else
                {
                    runtimeCost[cost.element] = cost.amount;
                }
            }

            var label = string.IsNullOrWhiteSpace(intentLabelOverride) ? displayName : intentLabelOverride;
            return new EnemyIntentDefinition(
                label,
                new CardCost(runtimeCost),
                new CardEffect(effectType, value),
                spellId);
        }

        public IReadOnlyList<ElementType> BuildRuneCostSequence()
        {
            var runeSequence = new List<ElementType>();
            if (costs == null)
            {
                return runeSequence;
            }

            foreach (var cost in costs)
            {
                if (cost == null || cost.amount <= 0)
                {
                    continue;
                }

                for (var i = 0; i < cost.amount; i++)
                {
                    runeSequence.Add(cost.element);
                }
            }

            return runeSequence;
        }

        public CardDefinition ToCardDefinition(string cardNameOverride = null)
        {
            var runtimeCost = new Dictionary<ElementType, int>();
            foreach (var cost in costs.Where(cost => cost != null && cost.amount > 0))
            {
                if (runtimeCost.ContainsKey(cost.element))
                {
                    runtimeCost[cost.element] += cost.amount;
                }
                else
                {
                    runtimeCost[cost.element] = cost.amount;
                }
            }

            var name = string.IsNullOrWhiteSpace(cardNameOverride) ? displayName : cardNameOverride;
            return new CardDefinition(
                name,
                new CardCost(runtimeCost),
                new CardEffect(effectType, value),
                spellId);
        }
    }
}
