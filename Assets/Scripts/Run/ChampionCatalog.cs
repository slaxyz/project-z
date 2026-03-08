using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProjectZ.Combat;
using System;

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
            var preferredOrder = new[] { "ace", "blaze", "slugger" };
            var preferred = preferredOrder
                .Where(id => _cachedAssets.Any(c => c != null && c.Id == id))
                .ToList();

            if (preferred.Count >= Mathf.Max(0, count))
            {
                return preferred.Take(count).ToList();
            }

            var fallback = _cachedAssets
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id) && !preferred.Contains(c.Id))
                .Select(c => c.Id)
                .ToList();

            return preferred
                .Concat(fallback)
                .Take(Mathf.Max(0, count))
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
                .Where(c => c != null)
                .ToList();

            if (_cachedAssets.Count == 0)
            {
                Debug.LogWarning("ChampionCatalog: no valid champion ids found in catalog asset. Using fallback catalog.");
                BuildFromFallback();
                return;
            }

            _cachedAssets = ValidateAndNormalizeChampionAssets(_cachedAssets);
            if (_cachedAssets.Count == 0)
            {
                Debug.LogWarning("ChampionCatalog: all entries invalid after validation. Using fallback catalog.");
                BuildFromFallback();
                return;
            }

            _cachedDefinitions = _cachedAssets
                .Select(c => new ChampionDefinition(c.Id, c.DisplayName, c.Role))
                .ToList();
        }

        private static List<ChampionDefinitionAsset> ValidateAndNormalizeChampionAssets(List<ChampionDefinitionAsset> source)
        {
            var result = new List<ChampionDefinitionAsset>();
            var seenIds = new HashSet<string>();

            foreach (var champion in source)
            {
                if (champion == null)
                {
                    continue;
                }

                var id = (champion.Id ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.LogWarning("ChampionCatalog: skipped entry with missing id.");
                    continue;
                }

                if (seenIds.Contains(id))
                {
                    Debug.LogWarning("ChampionCatalog: duplicate id '" + id + "' skipped.");
                    continue;
                }

                seenIds.Add(id);

                var displayName = string.IsNullOrWhiteSpace(champion.DisplayName) ? ToTitleFallback(id) : champion.DisplayName.Trim();
                var role = string.IsNullOrWhiteSpace(champion.Role) ? "Unknown role" : champion.Role.Trim();
                var pseudo = string.IsNullOrWhiteSpace(champion.Pseudo) ? displayName : champion.Pseudo.Trim();
                var fullName = string.IsNullOrWhiteSpace(champion.FullName) ? pseudo : champion.FullName.Trim();
                var shortLore = string.IsNullOrWhiteSpace(champion.ShortLore) ? "No lore yet." : champion.ShortLore.Trim();
                var description = string.IsNullOrWhiteSpace(champion.Description) ? shortLore : champion.Description.Trim();

                var tier = Mathf.Clamp(champion.TierStars, 3, 6);
                if (tier != champion.TierStars)
                {
                    Debug.LogWarning("ChampionCatalog: champion '" + id + "' tier out of range, clamped to " + tier + ".");
                }

                var element = champion.Element;
                if (!Enum.IsDefined(typeof(ElementType), element))
                {
                    Debug.LogWarning("ChampionCatalog: champion '" + id + "' has invalid element, defaulting to Fire.");
                    element = ElementType.Fire;
                }

                var championClass = champion.ChampionClass;
                if (!Enum.IsDefined(typeof(ChampionClassType), championClass))
                {
                    Debug.LogWarning("ChampionCatalog: champion '" + id + "' has invalid class, defaulting to Vanguard.");
                    championClass = ChampionClassType.Vanguard;
                }

                var unlockCost = Mathf.Max(0, champion.UnlockCost);
                var baseHp = Mathf.Max(1, champion.BaseHp);
                var baseAttack = Mathf.Max(1, champion.BaseAttack);
                var baseDefense = Mathf.Max(0, champion.BaseDefense);
                var baseSpecial = Mathf.Max(0, champion.BaseSpecial);

                result.Add(new ChampionDefinitionAsset(
                    champion.SourceNumericId,
                    id,
                    displayName,
                    pseudo,
                    fullName,
                    description,
                    champion.RarityDefinition,
                    champion.TypeDefinition,
                    champion.RoleDefinition,
                    champion.ClassDefinition,
                    champion.PassiveDefinition,
                    role,
                    tier,
                    element,
                    championClass,
                    unlockCost,
                    shortLore,
                    baseHp,
                    baseAttack,
                    baseDefense,
                    baseSpecial,
                    champion.AvatarSprite,
                    champion.SplashSprite));
            }

            return result;
        }

        private static string ToTitleFallback(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "Unknown";
            }

            var normalized = raw.Replace("_", " ").Replace("-", " ").Trim();
            if (normalized.Length == 0)
            {
                return "Unknown";
            }
            return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
        }

        private static void BuildFromFallback()
        {
            _cachedDefinitions = new List<ChampionDefinition>(FallbackChampions);
            _cachedAssets = FallbackChampions
                .Select(c => new ChampionDefinitionAsset(
                    c.Id,
                    c.DisplayName,
                    c.DisplayName,
                    c.DisplayName,
                    "Fallback champion description.",
                    c.Role,
                    3,
                    ElementType.Fire,
                    ChampionClassType.Vanguard,
                    25,
                    "Fallback champion entry.",
                    100,
                    10))
                .ToList();
        }
    }
}
