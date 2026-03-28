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
        public ElementType PrimaryElement => ResolvePrimaryElement();

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

        private ElementType ResolvePrimaryElement()
        {
            if (Intents == null || Intents.Count == 0)
            {
                return ElementType.Fire;
            }

            foreach (var intent in Intents)
            {
                if (intent == null || intent.Cost == null)
                {
                    continue;
                }

                if (!intent.Cost.TryGetPrimaryElement(out var element))
                {
                    continue;
                }

                return element;
            }

            return ElementType.Fire;
        }
    }
}
