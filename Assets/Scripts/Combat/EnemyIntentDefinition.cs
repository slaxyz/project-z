namespace ProjectZ.Combat
{
    public class EnemyIntentDefinition
    {
        public EnemyIntentDefinition(string name, CardCost cost, CardEffect effect, string spellId = null)
        {
            Name = name;
            Cost = cost;
            Effect = effect;
            SpellId = spellId;
        }

        public string Name { get; }
        public CardCost Cost { get; }
        public CardEffect Effect { get; }
        public string SpellId { get; }
    }
}
