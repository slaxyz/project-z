using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectZ.Combat
{
    [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "Project Z/Combat/Enemy Catalog")]
    public class EnemyCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<EnemyAssetEntry> enemies = new List<EnemyAssetEntry>();

        public List<EnemyDefinition> BuildRuntimeDefinitions(IReadOnlyDictionary<string, CombatSpellAsset> spellIndex)
        {
            return enemies
                .Where(entry => entry != null && entry.IsValid())
                .Select(entry => entry.ToRuntimeDefinition(spellIndex))
                .Where(definition => definition != null)
                .ToList();
        }
    }

    [System.Serializable]
    public class EnemyAssetEntry
    {
        [SerializeField] private string enemyId = "enemy_id";
        [SerializeField] private string displayName = "Enemy";
        [SerializeField] private int maxHp = 40;
        [SerializeField] private List<EnemyIntentAssetEntry> intents = new List<EnemyIntentAssetEntry>();

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(enemyId)
                && !string.IsNullOrWhiteSpace(displayName)
                && maxHp > 0
                && intents != null
                && intents.Count > 0;
        }

        public EnemyDefinition ToRuntimeDefinition(IReadOnlyDictionary<string, CombatSpellAsset> spellIndex)
        {
            var runtimeIntents = intents
                .Where(intent => intent != null && intent.IsValid())
                .Select(intent => intent.ToRuntimeIntent(enemyId, spellIndex))
                .Where(intent => intent != null)
                .ToList();

            if (runtimeIntents.Count == 0)
            {
                Debug.LogWarning("Enemy " + enemyId + " has no valid intents after spell resolution");
                return null;
            }

            return new EnemyDefinition(
                enemyId,
                displayName,
                maxHp,
                runtimeIntents);
        }
    }

    [System.Serializable]
    public class EnemyIntentAssetEntry
    {
        [SerializeField] private string spellId = "spell_id";
        [SerializeField] private string intentLabelOverride;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(spellId);
        }

        public EnemyIntentDefinition ToRuntimeIntent(string enemyId, IReadOnlyDictionary<string, CombatSpellAsset> spellIndex)
        {
            if (spellIndex == null || !spellIndex.TryGetValue(spellId, out var spell) || spell == null)
            {
                Debug.LogWarning("Enemy intent references unknown spellId: " + spellId + " (enemyId: " + enemyId + ")");
                return null;
            }

            return spell.ToEnemyIntentDefinition(intentLabelOverride);
        }
    }
}
