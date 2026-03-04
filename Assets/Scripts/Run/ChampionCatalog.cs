using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        private const string CatalogResourcePath = "Run/ChampionCatalog";
        private static readonly List<ChampionDefinition> FallbackChampions = new List<ChampionDefinition>
        {
            new ChampionDefinition("warden", "Warden", "Frontline tank"),
            new ChampionDefinition("arcanist", "Arcanist", "Spell burst"),
            new ChampionDefinition("ranger", "Ranger", "Ranged DPS"),
            new ChampionDefinition("shade", "Shade", "Assassin"),
            new ChampionDefinition("oracle", "Oracle", "Support control")
        };
        private static List<ChampionDefinition> _cachedDefinitions;
        private static List<ChampionDefinitionAsset> _cachedAssets;
        private static ChampionCatalogAsset _loadedCatalog;

        public static IReadOnlyList<ChampionDefinition> All
        {
            get
            {
                EnsureLoaded();
                return _cachedDefinitions;
            }
        }

        public static IReadOnlyList<ChampionDefinitionAsset> AllAssets
        {
            get
            {
                EnsureLoaded();
                return _cachedAssets;
            }
        }

        public static ChampionDefinitionAsset FindById(string championId)
        {
            EnsureLoaded();
            return _cachedAssets.FirstOrDefault(c => c != null && c.Id == championId);
        }

        public static IReadOnlyList<string> GetDefaultUnlockedChampionIds(int count = 3)
        {
            EnsureLoaded();
            return _cachedAssets
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id))
                .Take(Mathf.Max(0, count))
                .Select(c => c.Id)
                .ToList();
        }

        private static void EnsureLoaded()
        {
            if (_cachedDefinitions != null && _cachedAssets != null)
            {
                return;
            }

            _loadedCatalog = Resources.Load<ChampionCatalogAsset>(CatalogResourcePath);
            if (_loadedCatalog == null || _loadedCatalog.Champions == null || _loadedCatalog.Champions.Count == 0)
            {
                Debug.LogWarning("ChampionCatalog: missing or empty Resources/Run/ChampionCatalog. Using fallback catalog.");
                BuildFromFallback();
                return;
            }

            _cachedAssets = _loadedCatalog.Champions
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id))
                .ToList();

            if (_cachedAssets.Count == 0)
            {
                Debug.LogWarning("ChampionCatalog: no valid champion ids found in catalog asset. Using fallback catalog.");
                BuildFromFallback();
                return;
            }

            _cachedDefinitions = _cachedAssets
                .Select(c => new ChampionDefinition(c.Id, c.DisplayName, c.Role))
                .ToList();
        }

        private static void BuildFromFallback()
        {
            _cachedDefinitions = new List<ChampionDefinition>(FallbackChampions);
            _cachedAssets = FallbackChampions
                .Select(c => new ChampionDefinitionAsset(
                    c.Id,
                    c.DisplayName,
                    c.Role,
                    25,
                    "Fallback champion entry.",
                    100,
                    10))
                .ToList();
        }
    }
}
