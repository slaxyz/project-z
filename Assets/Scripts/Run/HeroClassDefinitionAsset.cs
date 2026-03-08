using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "HeroClass", menuName = "Project Z/Heroes/Class")]
    public class HeroClassDefinitionAsset : HeroKeyedDefinitionAsset
    {
        [System.NonSerialized] private Sprite _runtimeBadgeCache;
        [System.NonSerialized] private Sprite _runtimeIconCache;
        [SerializeField, TextArea(2, 6)] private string description;

        public string Description => description;

        public Sprite RuntimeIcon
        {
            get
            {
                if (Icon != null)
                {
                    return Icon;
                }

                if (_runtimeIconCache != null)
                {
                    return _runtimeIconCache;
                }

                var index = ExtractBadgeIndex(Id);
                if (index <= 0)
                {
                    return null;
                }

                _runtimeIconCache = Resources.Load<Sprite>("Art/UI/ClassIcons/" + index);
                return _runtimeIconCache;
            }
        }

        public Sprite BadgeSprite
        {
            get
            {
                if (_runtimeBadgeCache != null)
                {
                    return _runtimeBadgeCache;
                }

                var index = ExtractBadgeIndex(Id);
                if (index <= 0)
                {
                    return null;
                }

                _runtimeBadgeCache = Resources.Load<Sprite>("Art/UI/ClassBackgrounds/" + index);
                return _runtimeBadgeCache;
            }
        }

        private static int ExtractBadgeIndex(string classId)
        {
            if (string.IsNullOrWhiteSpace(classId))
            {
                return -1;
            }

            var normalized = classId.Trim().ToLowerInvariant();
            return normalized switch
            {
                "class_warrior" => 1,
                "class_tank" => 2,
                "class_rogue" => 3,
                "class_healer" => 4,
                "class_specialist" => 5,
                "class_gunner" => 6,
                _ => -1
            };
        }
    }
}
