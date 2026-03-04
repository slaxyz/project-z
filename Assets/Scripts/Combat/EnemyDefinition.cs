using System.Collections.Generic;

namespace ProjectZ.Combat
{
    public class EnemyDefinition
    {
        public EnemyDefinition(string id, string displayName, int maxHp, List<EnemyIntentDefinition> intents)
        {
            Id = id;
            DisplayName = displayName;
            MaxHp = maxHp;
            Intents = intents;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxHp { get; }
        public List<EnemyIntentDefinition> Intents { get; }
    }
}
