namespace ProjectZ.Combat
{
    public enum CardEffectType
    {
        Damage,
        Shield,
        Heal
    }

    public class CardEffect
    {
        public CardEffect(CardEffectType type, int amount)
        {
            Type = type;
            Amount = amount;
        }

        public CardEffectType Type { get; }
        public int Amount { get; }
    }
}
