using System.Collections.Generic;
using UnityEngine;

namespace ProjectZ.Combat
{
    [CreateAssetMenu(fileName = "SpellLibrary", menuName = "Project Z/Combat/Spell Library")]
    public class CombatSpellLibraryAsset : ScriptableObject
    {
        [SerializeField] private List<CombatSpellAsset> spells = new List<CombatSpellAsset>();

        public void ValidateUniqueIds()
        {
            var seen = new HashSet<string>();
            foreach (var spell in spells)
            {
                if (spell == null || !spell.IsValidForEnemy())
                {
                    continue;
                }

                if (seen.Contains(spell.SpellId))
                {
                    Debug.LogWarning("Duplicate spellId detected: " + spell.SpellId);
                    continue;
                }

                seen.Add(spell.SpellId);
            }
        }

        public Dictionary<string, CombatSpellAsset> BuildIndexById()
        {
            ValidateUniqueIds();

            var index = new Dictionary<string, CombatSpellAsset>();
            foreach (var spell in spells)
            {
                if (spell == null || !spell.IsValidForEnemy())
                {
                    continue;
                }

                if (index.ContainsKey(spell.SpellId))
                {
                    continue;
                }

                index[spell.SpellId] = spell;
            }

            return index;
        }
    }
}
