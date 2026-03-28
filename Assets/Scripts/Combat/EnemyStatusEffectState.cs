namespace ProjectZ.Combat
{
    public enum EnemyStatusEffectKind
    {
        Burn = 0
    }

    public sealed class EnemyStatusEffectState
    {
        public EnemyStatusEffectState(
            EnemyStatusEffectKind kind,
            int turnsRemaining,
            string iconResource,
            ElementType backgroundElement)
        {
            Kind = kind;
            TurnsRemaining = turnsRemaining < 0 ? 0 : turnsRemaining;
            IconResource = iconResource;
            BackgroundElement = backgroundElement;
        }

        public EnemyStatusEffectKind Kind { get; }
        public int TurnsRemaining { get; private set; }
        public string IconResource { get; }
        public ElementType BackgroundElement { get; }

        public bool Tick()
        {
            if (TurnsRemaining > 0)
            {
                TurnsRemaining--;
            }

            return TurnsRemaining <= 0;
        }
    }
}
