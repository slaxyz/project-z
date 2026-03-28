using System.Collections.Generic;

namespace ProjectZ.Combat
{
    public class ChampionCombatState
    {
        public ChampionCombatState(string id, string displayName, int maxHp, ElementType element, List<string> availableSpellIds, List<CardDefinition> hand = null)
        {
            Id = id;
            DisplayName = displayName;
            MaxHp = maxHp;
            Element = element;
            CurrentHp = maxHp;
            AvailableSpellIds = availableSpellIds ?? new List<string>();
            Hand = hand ?? new List<CardDefinition>();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxHp { get; }
        public ElementType Element { get; }
        public int CurrentHp { get; private set; }
        public int Block { get; private set; }
        public List<string> AvailableSpellIds { get; }
        public List<CardDefinition> Hand { get; }

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

        public void ReplaceHand(List<CardDefinition> hand)
        {
            Hand.Clear();
            if (hand == null)
            {
                return;
            }

            Hand.AddRange(hand);
        }

    }
}
