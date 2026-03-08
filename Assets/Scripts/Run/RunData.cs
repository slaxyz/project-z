using System.Collections.Generic;
using System.Linq;

namespace ProjectZ.Run
{
    [System.Serializable]
    public class ChampionSpellLoadout
    {
        public string championId;
        public List<string> spellIds = new List<string>();
    }

    [System.Serializable]
    public class RunData
    {
        public List<string> selectedChampionIds = new List<string>();
        public List<string> deckCardIds = new List<string>();
        public List<ChampionSpellLoadout> championSpellLoadouts = new List<ChampionSpellLoadout>();
        public int boardNodeIndex;
        public int zoneIndex;
        public int tileIndex;
        public int branchChoice; // -1 none, 0 left, 1 right
        public int coinsGained;
        public int wins;
        public int losses;
        public bool isActive;

        public bool HasValidTeam()
        {
            return selectedChampionIds.Count == 3 && selectedChampionIds.Distinct().Count() == 3;
        }

        public void Reset()
        {
            selectedChampionIds.Clear();
            deckCardIds.Clear();
            championSpellLoadouts.Clear();
            boardNodeIndex = 0;
            zoneIndex = 0;
            tileIndex = 0;
            branchChoice = -1;
            coinsGained = 0;
            wins = 0;
            losses = 0;
            isActive = false;
        }

        public void SetTeam(IEnumerable<string> championIds)
        {
            selectedChampionIds.Clear();
            selectedChampionIds.AddRange(championIds.Distinct().Take(3));
        }

        public List<string> GetChampionSpells(string championId)
        {
            if (string.IsNullOrWhiteSpace(championId))
            {
                return new List<string>();
            }

            var existing = championSpellLoadouts.FirstOrDefault(x => x != null && x.championId == championId);
            if (existing == null)
            {
                existing = new ChampionSpellLoadout { championId = championId };
                championSpellLoadouts.Add(existing);
            }

            if (existing.spellIds == null)
            {
                existing.spellIds = new List<string>();
            }

            return existing.spellIds;
        }
    }
}
