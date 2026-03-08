using UnityEngine;
using ProjectZ.Combat;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectZ.Run
{
    public enum ChampionClassType
    {
        Vanguard = 0,
        Striker = 1,
        Controller = 2,
        Support = 3
    }

    [System.Serializable]
    public class ChampionDefinitionAsset
    {
        [System.NonSerialized] private Sprite _runtimeAvatarCache;
        [System.NonSerialized] private Sprite _runtimeSplashCache;
        [SerializeField] private int sourceNumericId;
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string pseudo;
        [SerializeField] private string fullName;
        [SerializeField, TextArea(2, 6)] private string description;
        [SerializeField] private HeroRarityDefinitionAsset rarityDefinition;
        [SerializeField] private HeroTypeDefinitionAsset typeDefinition;
        [SerializeField] private HeroRoleDefinitionAsset roleDefinition;
        [SerializeField] private HeroClassDefinitionAsset classDefinition;
        [SerializeField] private HeroPassiveDefinitionAsset passiveDefinition;
        [SerializeField] private string role;
        [SerializeField] private int tierStars = 3;
        [SerializeField] private ElementType element = ElementType.Fire;
        [SerializeField] private ChampionClassType championClass = ChampionClassType.Vanguard;
        [SerializeField] private int unlockCost;
        [SerializeField] private Sprite avatarSprite;
        [SerializeField] private Sprite splashSprite;
        [SerializeField] private string shortLore;
        [SerializeField] private int baseHp = 100;
        [SerializeField] private int baseAttack = 10;
        [SerializeField] private int baseDefense = 10;
        [SerializeField] private int baseSpecial = 10;

        public ChampionDefinitionAsset()
        {
        }

        public ChampionDefinitionAsset(
            string id,
            string displayName,
            string pseudo,
            string fullName,
            string description,
            string role,
            int tierStars,
            ElementType element,
            ChampionClassType championClass,
            int unlockCost,
            string shortLore,
            int baseHp,
            int baseAttack,
            Sprite splashSprite = null)
        {
            this.id = id;
            this.displayName = displayName;
            this.pseudo = pseudo;
            this.fullName = fullName;
            this.description = description;
            this.role = role;
            this.tierStars = Mathf.Clamp(tierStars, 3, 6);
            this.element = element;
            this.championClass = championClass;
            this.unlockCost = unlockCost;
            this.shortLore = shortLore;
            this.baseHp = baseHp;
            this.baseAttack = baseAttack;
            this.splashSprite = splashSprite;
            baseDefense = 10;
            baseSpecial = 10;
        }

        public ChampionDefinitionAsset(
            int sourceNumericId,
            string id,
            string displayName,
            string pseudo,
            string fullName,
            string description,
            HeroRarityDefinitionAsset rarityDefinition,
            HeroTypeDefinitionAsset typeDefinition,
            HeroRoleDefinitionAsset roleDefinition,
            HeroClassDefinitionAsset classDefinition,
            HeroPassiveDefinitionAsset passiveDefinition,
            string role,
            int tierStars,
            ElementType element,
            ChampionClassType championClass,
            int unlockCost,
            string shortLore,
            int baseHp,
            int baseAttack,
            int baseDefense,
            int baseSpecial,
            Sprite avatarSprite,
            Sprite splashSprite = null)
            : this(
                id,
                displayName,
                pseudo,
                fullName,
                description,
                role,
                tierStars,
                element,
                championClass,
                unlockCost,
                shortLore,
                baseHp,
                baseAttack,
                splashSprite)
        {
            this.sourceNumericId = sourceNumericId;
            this.rarityDefinition = rarityDefinition;
            this.typeDefinition = typeDefinition;
            this.roleDefinition = roleDefinition;
            this.classDefinition = classDefinition;
            this.passiveDefinition = passiveDefinition;
            this.avatarSprite = avatarSprite;
            this.baseDefense = Mathf.Max(0, baseDefense);
            this.baseSpecial = Mathf.Max(0, baseSpecial);
        }

        public string Id
        {
            get { return id; }
        }

        public int SourceNumericId
        {
            get { return sourceNumericId; }
        }

        public string DisplayName
        {
            get { return displayName; }
        }

        public string Pseudo
        {
            get { return string.IsNullOrWhiteSpace(pseudo) ? displayName : pseudo; }
        }

        public string FullName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    return fullName;
                }

                return Pseudo;
            }
        }

        public string Description
        {
            get { return string.IsNullOrWhiteSpace(description) ? shortLore : description; }
        }

        public string Role
        {
            get { return role; }
        }

        public HeroRarityDefinitionAsset RarityDefinition
        {
            get { return rarityDefinition; }
        }

        public HeroTypeDefinitionAsset TypeDefinition
        {
            get { return typeDefinition; }
        }

        public HeroRoleDefinitionAsset RoleDefinition
        {
            get { return roleDefinition; }
        }

        public HeroClassDefinitionAsset ClassDefinition
        {
            get { return classDefinition; }
        }

        public HeroPassiveDefinitionAsset PassiveDefinition
        {
            get { return passiveDefinition; }
        }

        public int UnlockCost
        {
            get { return unlockCost; }
        }

        public int TierStars
        {
            get { return Mathf.Clamp(tierStars, 3, 6); }
        }

        public ElementType Element
        {
            get { return element; }
        }

        public ChampionClassType ChampionClass
        {
            get { return championClass; }
        }

        public Sprite SplashSprite
        {
            get
            {
                if (splashSprite != null)
                {
                    return splashSprite;
                }

                if (_runtimeSplashCache == null)
                {
                    _runtimeSplashCache = TryLoadRuntimeSprite("splash");
                }

                return _runtimeSplashCache;
            }
        }

        public Sprite AvatarSprite
        {
            get
            {
                if (avatarSprite != null)
                {
                    return avatarSprite;
                }

                if (_runtimeAvatarCache == null)
                {
                    _runtimeAvatarCache = TryLoadRuntimeSprite("avatar");
                }

                return _runtimeAvatarCache;
            }
        }

        public string ShortLore
        {
            get { return shortLore; }
        }

        public int BaseHp
        {
            get { return baseHp; }
        }

        public int BaseAttack
        {
            get { return baseAttack; }
        }

        public int BaseDefense
        {
            get { return baseDefense; }
        }

        public int BaseSpecial
        {
            get { return baseSpecial; }
        }

        private Sprite TryLoadRuntimeSprite(string kind)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var normalizedId = id.Trim().ToLowerInvariant();
            var resourcePath = "Art/Characters/" + normalizedId + "/" + normalizedId + "_" + kind;
            var fromResources = Resources.Load<Sprite>(resourcePath);
            if (fromResources != null)
            {
                return fromResources;
            }

#if UNITY_EDITOR
            var editorPath = "Assets/Resources/Art/Characters/" + normalizedId + "/" + normalizedId + "_" + kind + ".png";
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(editorPath);
            if (direct != null)
            {
                return direct;
            }

            var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(editorPath);
            foreach (var sub in subs)
            {
                var sprite = sub as Sprite;
                if (sprite != null)
                {
                    return sprite;
                }
            }
#else
            return null;
#endif
            return null;
        }
    }
}
