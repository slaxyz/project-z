namespace ProjectZ.Combat
{
    public class EnemyCombatState
    {
        public EnemyCombatState(EnemyDefinition definition)
        {
            Definition = definition;
            MaxHp = definition.MaxHp;
            CurrentHp = definition.MaxHp;
        }

        public EnemyDefinition Definition { get; }
        public int MaxHp { get; }
        public int CurrentHp { get; private set; }
        public int Block { get; private set; }
        public bool IsAlive => CurrentHp > 0;

        public int TakeDamage(int rawDamage)
        {
            if (rawDamage <= 0 || !IsAlive)
            {
                return 0;
            }

            var damageAfterBlock = rawDamage;
            if (Block > 0)
            {
                var absorbed = rawDamage < Block ? rawDamage : Block;
                Block -= absorbed;
                damageAfterBlock -= absorbed;
            }

            if (damageAfterBlock <= 0)
            {
                return 0;
            }

            var previousHp = CurrentHp;
            CurrentHp -= damageAfterBlock;
            if (CurrentHp < 0)
            {
                CurrentHp = 0;
            }

            return previousHp - CurrentHp;
        }

        public int Heal(int amount)
        {
            if (amount <= 0 || !IsAlive)
            {
                return 0;
            }

            var previousHp = CurrentHp;
            CurrentHp += amount;
            if (CurrentHp > MaxHp)
            {
                CurrentHp = MaxHp;
            }

            return CurrentHp - previousHp;
        }

        public void AddBlock(int amount)
        {
            if (amount <= 0 || !IsAlive)
            {
                return;
            }

            Block += amount;
        }

        public void ResetBlock()
        {
            Block = 0;
        }
    }
}
