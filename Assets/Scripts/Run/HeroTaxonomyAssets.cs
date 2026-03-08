using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectZ.Run
{
    public abstract class HeroKeyedDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string id = "id";
        [SerializeField] private string displayName = "Name";
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public Sprite Icon => icon;
    }

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
                return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Rarity/" + key + ".png");
#else
                return null;
#endif
            }
        }
    }

    [CreateAssetMenu(fileName = "HeroType", menuName = "Project Z/Heroes/Type")]
    public class HeroTypeDefinitionAsset : HeroKeyedDefinitionAsset
    {
    }

    [CreateAssetMenu(fileName = "HeroRole", menuName = "Project Z/Heroes/Role")]
    public class HeroRoleDefinitionAsset : HeroKeyedDefinitionAsset
    {
    }

    [CreateAssetMenu(fileName = "HeroClass", menuName = "Project Z/Heroes/Class")]
    public class HeroClassDefinitionAsset : HeroKeyedDefinitionAsset
    {
        [SerializeField, TextArea(2, 6)] private string description;
        public string Description => description;
    }

    [CreateAssetMenu(fileName = "HeroPassive", menuName = "Project Z/Heroes/Passive")]
    public class HeroPassiveDefinitionAsset : HeroKeyedDefinitionAsset
    {
        [SerializeField, TextArea(2, 6)] private string description;
        public string Description => description;
    }
}
