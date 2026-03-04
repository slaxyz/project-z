namespace ProjectZ.Combat
{
    public class CardDefinition
    {
        public CardDefinition(string name, CardCost cost, CardEffect effect)
        {
            Name = name;
            Cost = cost;
            Effect = effect;
        }

        public string Name { get; }
        public CardCost Cost { get; }
        public CardEffect Effect { get; }
    }
}
