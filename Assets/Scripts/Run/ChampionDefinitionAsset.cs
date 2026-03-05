using UnityEngine;
using ProjectZ.Combat;

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
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string role;
        [SerializeField] private int tierStars = 3;
        [SerializeField] private ElementType element = ElementType.Fire;
        [SerializeField] private ChampionClassType championClass = ChampionClassType.Vanguard;
        [SerializeField] private int unlockCost;
        [SerializeField] private Sprite splashSprite;
        [SerializeField] private string shortLore;
        [SerializeField] private int baseHp = 100;
        [SerializeField] private int baseAttack = 10;

        public ChampionDefinitionAsset()
        {
        }

        public ChampionDefinitionAsset(
            string id,
            string displayName,
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
            this.role = role;
            this.tierStars = Mathf.Clamp(tierStars, 3, 6);
            this.element = element;
            this.championClass = championClass;
            this.unlockCost = unlockCost;
            this.shortLore = shortLore;
            this.baseHp = baseHp;
            this.baseAttack = baseAttack;
            this.splashSprite = splashSprite;
        }

        public string Id
        {
            get { return id; }
        }

        public string DisplayName
        {
            get { return displayName; }
        }

        public string Role
        {
            get { return role; }
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
            get { return splashSprite; }
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
    }
}
