using ProjectZ.Combat;
using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "HeroType", menuName = "Project Z/Heroes/Type")]
    public class HeroTypeDefinitionAsset : HeroKeyedDefinitionAsset
    {
        [System.NonSerialized] private Sprite _runtimeDefaultBadgeCache;

        public ElementType Element
        {
            get
            {
                return ResolveElementFromNumericId(ExtractNumericTypeId(Id));
            }
        }

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

        public Sprite RuntimeIcon
        {
            get
            {
                var numericId = ExtractNumericTypeId(Id);
                if (numericId <= 0)
                {
                    return Icon;
                }

                return Resources.Load<Sprite>("Art/UI/TypeIcons/" + numericId) ?? Icon;
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

        private static ElementType ResolveElementFromNumericId(int numericId)
        {
            switch (numericId)
            {
                case 1:
                    return ElementType.Fire;
                case 2:
                    return ElementType.Nature;
                case 3:
                    return ElementType.Poison;
                case 4:
                    return ElementType.Water;
                case 5:
                    return ElementType.Ground;
                case 6:
                    return ElementType.Mystic;
                default:
                    return ElementType.Fire;
            }
        }
    }
}
