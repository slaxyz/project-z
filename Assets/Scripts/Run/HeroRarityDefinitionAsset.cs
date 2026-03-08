using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "HeroRarity", menuName = "Project Z/Heroes/Rarity")]
    public class HeroRarityDefinitionAsset : HeroKeyedDefinitionAsset
    {
        [SerializeField] private Color backgroundColor = new Color(0.24f, 0.28f, 0.34f, 1f);
        [SerializeField] private Sprite backgroundSprite;

        public Color BackgroundColor => backgroundColor;

        public Sprite BackgroundSprite
        {
            get
            {
                if (backgroundSprite != null)
                {
                    return backgroundSprite;
                }

                var key = (Id ?? string.Empty).Trim().ToLowerInvariant();
                var fromResources = Resources.Load<Sprite>("Art/UI/Rarity/" + key);
                if (fromResources != null)
                {
                    return fromResources;
                }

#if UNITY_EDITOR
                return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Rarity/" + key + ".png");
#else
                return null;
#endif
            }
        }
    }
}
