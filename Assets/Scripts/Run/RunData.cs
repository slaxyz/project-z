using System.Collections.Generic;

namespace ProjectZ.Run
{
    [System.Serializable]
    public class RunData
    {
        public List<string> selectedChampionIds = new List<string>();
        public List<string> deckCardIds = new List<string>();
        public int boardNodeIndex;
        public int wins;
        public int losses;
        public bool isActive;

        public void Reset()
        {
            selectedChampionIds.Clear();
            deckCardIds.Clear();
            boardNodeIndex = 0;
            wins = 0;
            losses = 0;
            isActive = false;
        }
    }
}
