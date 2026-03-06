using System.Collections.Generic;
using System.Linq;

namespace ProjectZ.Meta
{
    [System.Serializable]
    public class MetaData
    {
        public int progressionPoints;
        public List<string> unlockedSpellIds = new List<string>();
        public List<string> unlockedChampionIds = new List<string>();
        public bool hasActiveRunProgress;
        public int runZoneIndex;
        public int runTileIndex;
        public int runBoardNodeIndex;
        public int runCoinsGained;
        public List<string> runSelectedChampionIds = new List<string>();

        public void UnlockSpell(string spellId)
        {
            if (!unlockedSpellIds.Contains(spellId))
            {
                unlockedSpellIds.Add(spellId);
            }
        }

        public bool IsChampionUnlocked(string championId)
        {
            if (string.IsNullOrWhiteSpace(championId))
            {
                return false;
            }

            EnsureCollections();
            return unlockedChampionIds.Contains(championId);
        }

        public void UnlockChampion(string championId)
        {
            if (string.IsNullOrWhiteSpace(championId))
            {
                return;
            }

            EnsureCollections();
            if (!unlockedChampionIds.Contains(championId))
            {
                unlockedChampionIds.Add(championId);
            }
        }

        public bool EnsureDefaultUnlockedChampions(IEnumerable<string> defaultChampionIds)
        {
            EnsureCollections();

            if (unlockedChampionIds.Count > 0)
            {
                return false;
            }

            var changed = false;
            foreach (var id in defaultChampionIds ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (unlockedChampionIds.Contains(id))
                {
                    continue;
                }

                unlockedChampionIds.Add(id);
                changed = true;
            }

            return changed;
        }

        public void EnsureCollections()
        {
            if (unlockedSpellIds == null)
            {
                unlockedSpellIds = new List<string>();
            }

            if (unlockedChampionIds == null)
            {
                unlockedChampionIds = new List<string>();
            }
        }

        public void SetRunProgress(int zoneIndex, int tileIndex, int boardNodeIndex, int coinsGained, IEnumerable<string> selectedChampionIds)
        {
            hasActiveRunProgress = true;
            runZoneIndex = zoneIndex;
            runTileIndex = tileIndex;
            runBoardNodeIndex = boardNodeIndex;
            runCoinsGained = coinsGained;
            runSelectedChampionIds = selectedChampionIds != null
                ? selectedChampionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Take(3).ToList()
                : new List<string>();
        }

        public void ClearRunProgress()
        {
            hasActiveRunProgress = false;
            runZoneIndex = 0;
            runTileIndex = 0;
            runBoardNodeIndex = 0;
            runCoinsGained = 0;
            runSelectedChampionIds = new List<string>();
        }
    }
}
