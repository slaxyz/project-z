using System.Collections.Generic;

namespace ProjectZ.Run
{
    public readonly struct ChampionDefinition
    {
        public ChampionDefinition(string id, string displayName, string role)
        {
            Id = id;
            DisplayName = displayName;
            Role = role;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Role { get; }
    }

    public static class ChampionCatalog
    {
        private static readonly List<ChampionDefinition> Champions = new List<ChampionDefinition>
        {
            new ChampionDefinition("warden", "Warden", "Frontline tank"),
            new ChampionDefinition("arcanist", "Arcanist", "Spell burst"),
            new ChampionDefinition("ranger", "Ranger", "Ranged DPS"),
            new ChampionDefinition("shade", "Shade", "Assassin"),
            new ChampionDefinition("oracle", "Oracle", "Support control")
        };

        public static IReadOnlyList<ChampionDefinition> All
        {
            get { return Champions; }
        }
    }
}
