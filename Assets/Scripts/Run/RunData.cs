using System.Collections.Generic;
using System.Linq;

namespace ProjectZ.Run
{
    [System.Serializable]
    public class RunData
    {
        public List<string> selectedChampionIds = new List<string>();
        public List<string> deckCardIds = new List<string>();
        public int boardNodeIndex;
        public int zoneIndex;
        public int tileIndex;
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
            boardNodeIndex = 0;
            zoneIndex = 0;
            tileIndex = 0;
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
    }
}
