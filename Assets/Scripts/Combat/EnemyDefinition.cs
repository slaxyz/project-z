using System.Collections.Generic;

namespace ProjectZ.Combat
{
    public class EnemyDefinition
    {
        public EnemyDefinition(string id, string displayName, int maxHp, EnemyBiome biome, EnemyTier tier, List<EnemyIntentDefinition> intents)
        {
            Id = id;
            DisplayName = displayName;
            MaxHp = maxHp;
            Biome = biome;
            Tier = tier;
            Intents = intents;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxHp { get; }
        public EnemyBiome Biome { get; }
        public EnemyTier Tier { get; }
        public List<EnemyIntentDefinition> Intents { get; }
    }
}
