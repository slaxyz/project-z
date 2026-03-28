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
        public int BurnStackCount
        {
            get
            {
                var burnCount = 0;
                for (var i = 0; i < _statusEffects.Count; i++)
                {
                    if (_statusEffects[i] != null && _statusEffects[i].Kind == EnemyStatusEffectKind.Burn)
                    {
                        burnCount++;
                    }
                }

                return burnCount;
            }
        }
        public IReadOnlyList<EnemyStatusEffectState> StatusEffects => _statusEffects;
        public bool IsAlive => CurrentHp > 0;

        private const int MaxBurnStacks = 10;
        private readonly List<EnemyStatusEffectState> _statusEffects = new List<EnemyStatusEffectState>();

        public void BeginTurn()
        {
            for (var i = _statusEffects.Count - 1; i >= 0; i--)
            {
                var status = _statusEffects[i];
                if (status == null)
                {
                    _statusEffects.RemoveAt(i);
                    continue;
                }

                if (!status.Tick())
                {
                    continue;
                }

                _statusEffects.RemoveAt(i);
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

        public int AddBurn(int stackCount, int duration, string iconResource = "4_abnormal Burned")
        {
            if (stackCount <= 0 || !IsAlive)
            {
                return 0;
            }

            var applied = 0;
            var turns = Mathf.Max(2, duration);
            for (var i = 0; i < stackCount && BurnStackCount < MaxBurnStacks; i++)
            {
                _statusEffects.Add(new EnemyStatusEffectState(
                    EnemyStatusEffectKind.Burn,
                    turns,
                    string.IsNullOrWhiteSpace(iconResource) ? "4_abnormal Burned" : iconResource,
                    ElementType.Fire));
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
    }
}
