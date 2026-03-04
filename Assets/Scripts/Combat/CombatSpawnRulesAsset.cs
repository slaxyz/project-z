using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectZ.Combat
{
    [CreateAssetMenu(fileName = "SpawnRules", menuName = "Project Z/Combat/Spawn Rules")]
    public class CombatSpawnRulesAsset : ScriptableObject
    {
        [SerializeField] private List<BiomeSpawnRulesEntry> biomeRules = new List<BiomeSpawnRulesEntry>();

        public bool TryGetTierWeights(EnemyBiome biome, int boardNodeIndex, out List<EnemyTierWeightEntry> weights)
        {
            weights = null;

            var biomeEntry = biomeRules.FirstOrDefault(entry => entry != null && entry.biome == biome);
            if (biomeEntry == null || biomeEntry.nodeRules == null || biomeEntry.nodeRules.Count == 0)
            {
                return false;
            }

            var nodeEntry = biomeEntry.nodeRules.FirstOrDefault(rule =>
                rule != null
                && boardNodeIndex >= rule.minNodeIndex
                && boardNodeIndex <= rule.maxNodeIndex
                && rule.tierWeights != null
                && rule.tierWeights.Any(weight => weight != null && weight.weight > 0));

            if (nodeEntry == null)
            {
                return false;
            }

            weights = nodeEntry.tierWeights
                .Where(weight => weight != null && weight.weight > 0)
                .ToList();

            return weights.Count > 0;
        }
    }

    [System.Serializable]
    public class BiomeSpawnRulesEntry
    {
        public EnemyBiome biome = EnemyBiome.Zone1;
        public List<NodeTierRuleEntry> nodeRules = new List<NodeTierRuleEntry>();
    }

    [System.Serializable]
    public class NodeTierRuleEntry
    {
        public int minNodeIndex;
        public int maxNodeIndex = 999;
        public List<EnemyTierWeightEntry> tierWeights = new List<EnemyTierWeightEntry>();
    }

    [System.Serializable]
    public class EnemyTierWeightEntry
    {
        public EnemyTier tier = EnemyTier.Minion;
        public int weight = 1;
    }
}
