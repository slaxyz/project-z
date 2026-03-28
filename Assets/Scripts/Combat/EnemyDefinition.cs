using System.Collections.Generic;
using ProjectZ.Run;
using UnityEngine;

namespace ProjectZ.Combat
{
    public class EnemyDefinition
    {
        private Sprite _runtimeZoneBackgroundCache;
        private Sprite _runtimeSplashCache;

        public EnemyDefinition(
            string id,
            string displayName,
            int maxHp,
            EnemyBiome biome,
            EnemyTier tier,
            List<EnemyIntentDefinition> intents,
            HeroTypeDefinitionAsset typeDefinition = null,
            int artIndex = 1)
        {
            Id = id;
            DisplayName = displayName;
            MaxHp = maxHp;
            Biome = biome;
            Tier = tier;
            Intents = intents;
            TypeDefinition = typeDefinition;
            ArtIndex = artIndex <= 0 ? 1 : artIndex;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxHp { get; }
        public EnemyBiome Biome { get; }
        public EnemyTier Tier { get; }
        public List<EnemyIntentDefinition> Intents { get; }
        public HeroTypeDefinitionAsset TypeDefinition { get; }
        public int ArtIndex { get; }
        public int ZoneNumber => (int)Biome + 1;

        public Sprite ZoneBackgroundSprite
        {
            get
            {
                if (_runtimeZoneBackgroundCache != null)
                {
                    return _runtimeZoneBackgroundCache;
                }

                _runtimeZoneBackgroundCache = Resources.Load<Sprite>("Art/UI/Monster/Zone" + ZoneNumber + "/zone_BG");
                return _runtimeZoneBackgroundCache;
            }
        }

        public Sprite SplashSprite
        {
            get
            {
                if (_runtimeSplashCache != null)
                {
                    return _runtimeSplashCache;
                }

                _runtimeSplashCache = Resources.Load<Sprite>("Art/UI/Monster/Zone" + ZoneNumber + "/" + ArtIndex);
                return _runtimeSplashCache;
            }
        }
    }
}
