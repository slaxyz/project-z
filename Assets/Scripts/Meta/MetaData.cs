using System.Collections.Generic;

namespace ProjectZ.Meta
{
    [System.Serializable]
    public class MetaData
    {
        public int progressionPoints;
        public List<string> unlockedSpellIds = new List<string>();

        public void UnlockSpell(string spellId)
        {
            if (!unlockedSpellIds.Contains(spellId))
            {
                unlockedSpellIds.Add(spellId);
            }
        }
    }
}
