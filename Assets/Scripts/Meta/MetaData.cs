using System.Collections.Generic;
using System.Linq;
using ProjectZ.Run;
using UnityEngine;

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
        public int runBranchChoice = -1;
        public int runBoardNodeIndex;
        public int runCoinsGained;
        public List<string> runSelectedChampionIds = new List<string>();
        public List<string> runDeckSpellIds = new List<string>();
        public List<ChampionSpellLoadout> runChampionSpellLoadouts = new List<ChampionSpellLoadout>();
        public bool runHasLastFightResult;
        public bool runLastFightWasVictory;
        public bool runLastFightWasBoss;
        public int runLastFightCoinsReward;
        public string runLastFightEnemyId;
        public string runLastFightEnemyDisplayName;
        public List<string> runPendingSpellRewardOffers = new List<string>();
        public int runPendingSpellRewardRefreshesRemaining;
        public List<string> runShopOffers = new List<string>();
        public string runPendingReplacementIncomingSpellId;
        public string runPendingReplacementChampionId;
        public bool runPendingReplacementFromShop;
        public int runPendingReplacementShopCost;
        public bool hasLastRunSummary;
        public int lifetimeRunCount;
        public int lastRunCoinsEarned;
        public int lastRunWins;
        public int lastRunLosses;
        public int lastRunZoneReached;
        public int lastRunLastTileIndex;

        public void UnlockSpell(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return;
            }

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

        public void SetRunProgress(
            int zoneIndex,
            int tileIndex,
            int branchChoice,
            int boardNodeIndex,
            int coinsGained,
            IEnumerable<string> selectedChampionIds,
            IEnumerable<string> runDeckSpellIdsInput,
            IEnumerable<ChampionSpellLoadout> championSpellLoadoutsInput,
            bool hasLastFightResult,
            bool lastFightWasVictory,
            bool lastFightWasBoss,
            int lastFightCoinsReward,
            string lastFightEnemyId,
            string lastFightEnemyDisplayName,
            IEnumerable<string> pendingSpellRewardOffers,
            int pendingSpellRewardRefreshesRemaining,
            IEnumerable<string> shopOffers,
            string pendingReplacementIncomingSpellId,
            string pendingReplacementChampionId,
            bool pendingReplacementFromShop,
            int pendingReplacementShopCost)
        {
            hasActiveRunProgress = true;
            runZoneIndex = zoneIndex;
            runTileIndex = tileIndex;
            runBranchChoice = branchChoice;
            runBoardNodeIndex = boardNodeIndex;
            runCoinsGained = coinsGained;
            runSelectedChampionIds = selectedChampionIds != null
                ? selectedChampionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Take(3).ToList()
                : new List<string>();
            runDeckSpellIds = runDeckSpellIdsInput != null
                ? runDeckSpellIdsInput.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList()
                : new List<string>();
            runChampionSpellLoadouts = championSpellLoadoutsInput != null
                ? championSpellLoadoutsInput
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.championId))
                    .Select(x => new ChampionSpellLoadout
                    {
                        championId = x.championId,
                        spellIds = x.spellIds != null
                            ? x.spellIds.Where(id => !string.IsNullOrWhiteSpace(id)).Take(4).ToList()
                            : new List<string>()
                    })
                    .ToList()
                : new List<ChampionSpellLoadout>();
            runHasLastFightResult = hasLastFightResult;
            runLastFightWasVictory = lastFightWasVictory;
            runLastFightWasBoss = lastFightWasBoss;
            runLastFightCoinsReward = lastFightCoinsReward;
            runLastFightEnemyId = string.IsNullOrWhiteSpace(lastFightEnemyId)
                ? string.Empty
                : lastFightEnemyId;
            runLastFightEnemyDisplayName = string.IsNullOrWhiteSpace(lastFightEnemyDisplayName)
                ? string.Empty
                : lastFightEnemyDisplayName;
            runPendingSpellRewardOffers = pendingSpellRewardOffers != null
                ? pendingSpellRewardOffers.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList()
                : new List<string>();
            runPendingSpellRewardRefreshesRemaining = Mathf.Max(0, pendingSpellRewardRefreshesRemaining);
            runShopOffers = shopOffers != null
                ? shopOffers.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList()
                : new List<string>();
            runPendingReplacementIncomingSpellId = pendingReplacementIncomingSpellId;
            runPendingReplacementChampionId = pendingReplacementChampionId;
            runPendingReplacementFromShop = pendingReplacementFromShop;
            runPendingReplacementShopCost = pendingReplacementShopCost;
        }

        public void ClearRunProgress()
        {
            hasActiveRunProgress = false;
            runZoneIndex = 0;
            runTileIndex = 0;
            runBranchChoice = -1;
            runBoardNodeIndex = 0;
            runCoinsGained = 0;
            runSelectedChampionIds = new List<string>();
            runDeckSpellIds = new List<string>();
            runChampionSpellLoadouts = new List<ChampionSpellLoadout>();
            runHasLastFightResult = false;
            runLastFightWasVictory = false;
            runLastFightWasBoss = false;
            runLastFightCoinsReward = 0;
            runLastFightEnemyId = null;
            runLastFightEnemyDisplayName = null;
            runPendingSpellRewardOffers = new List<string>();
            runPendingSpellRewardRefreshesRemaining = 0;
            runShopOffers = new List<string>();
            runPendingReplacementIncomingSpellId = null;
            runPendingReplacementChampionId = null;
            runPendingReplacementFromShop = false;
            runPendingReplacementShopCost = 0;
        }

        public void SetLastRunSummary(int coinsEarned, int wins, int losses, int zoneReached, int lastTileIndex)
        {
            hasLastRunSummary = true;
            lastRunCoinsEarned = coinsEarned;
            lastRunWins = wins;
            lastRunLosses = losses;
            lastRunZoneReached = zoneReached;
            lastRunLastTileIndex = lastTileIndex;
        }
    }
}
