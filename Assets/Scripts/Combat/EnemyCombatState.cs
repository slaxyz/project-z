using System.Collections.Generic;
using UnityEngine;

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
        public int BurnStackCount => _burnStacks.Count;
        public bool IsAlive => CurrentHp > 0;

        private readonly List<BurnStack> _burnStacks = new List<BurnStack>();

        public void BeginTurn()
        {
            for (var i = _burnStacks.Count - 1; i >= 0; i--)
            {
                _burnStacks[i].TurnsRemaining--;
                if (_burnStacks[i].TurnsRemaining > 0)
                {
                    continue;
                }

                _burnStacks.RemoveAt(i);
            }
        }

        public int TakeDamage(int rawDamage, ElementType? sourceElement = null)
        {
            if (rawDamage <= 0 || !IsAlive)
            {
                return 0;
            }

            var damageBeforeBlock = rawDamage;
            if (sourceElement.HasValue && sourceElement.Value == ElementType.Fire && BurnStackCount > 0)
            {
                damageBeforeBlock += BurnStackCount;
            }

            var damageAfterBlock = damageBeforeBlock;
            if (Block > 0)
            {
                var absorbed = damageBeforeBlock < Block ? damageBeforeBlock : Block;
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

        public int AddBurn(int stackCount, int duration)
        {
            if (stackCount <= 0 || !IsAlive)
            {
                return 0;
            }

            var applied = 0;
            var turns = Mathf.Max(2, duration);
            for (var i = 0; i < stackCount && _burnStacks.Count < 10; i++)
            {
                _burnStacks.Add(new BurnStack(turns));
                applied++;
            }

            return applied;
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

        private sealed class BurnStack
        {
            public BurnStack(int turnsRemaining)
            {
                TurnsRemaining = turnsRemaining;
            }

            public int TurnsRemaining { get; set; }
        }
    }
}
