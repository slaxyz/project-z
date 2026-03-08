using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "HeroType", menuName = "Project Z/Heroes/Type")]
    public class HeroTypeDefinitionAsset : HeroKeyedDefinitionAsset
    {
        [System.NonSerialized] private Sprite _runtimeDefaultBadgeCache;
        [System.NonSerialized] private Sprite _runtimeGemBadgeCache;

        public Sprite DefaultBadgeSprite
        {
            get
            {
                if (_runtimeDefaultBadgeCache != null)
                {
                    return _runtimeDefaultBadgeCache;
                }

                var numericId = ExtractNumericTypeId(Id);
                if (numericId <= 0)
                {
                    return null;
                }

                _runtimeDefaultBadgeCache = Resources.Load<Sprite>("Art/UI/TypeBackgrounds/" + numericId);
                return _runtimeDefaultBadgeCache;
            }
        }

        public Sprite GemBadgeSprite
        {
            get
            {
                if (_runtimeGemBadgeCache != null)
                {
                    return _runtimeGemBadgeCache;
                }

                var numericId = ExtractNumericTypeId(Id);
                if (numericId <= 0)
                {
                    return null;
                }

                _runtimeGemBadgeCache = Resources.Load<Sprite>("Art/UI/TypeSpirals/" + numericId);
                return _runtimeGemBadgeCache;
            }
        }

        private static int ExtractNumericTypeId(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                return -1;
            }

            var parts = typeId.Split('_');
            if (parts.Length == 0)
            {
                return -1;
            }

            return int.TryParse(parts[parts.Length - 1], out var parsed) ? parsed : -1;
        }
    }
}
