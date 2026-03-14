using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "RunLoopConfig", menuName = "Project Z/Run/Run Loop Config")]
    public class RunLoopConfigAsset : ScriptableObject
    {
        [SerializeField] private List<int> zoneTileCounts = new List<int> { 8, 8, 8 };
        [SerializeField] private List<int> zoneIdsInRun = new List<int> { 1, 2, 3 };
        [SerializeField] private int victoryCoinReward = 15;

        public int VictoryCoinReward
        {
            get { return Mathf.Max(0, victoryCoinReward); }
        }

        public int GetZoneTileCount(int zoneIndex, int fallback = 4)
        {
            if (zoneTileCounts == null || zoneTileCounts.Count == 0)
            {
                return Mathf.Max(1, fallback);
            }

            var index = Mathf.Clamp(zoneIndex, 0, zoneTileCounts.Count - 1);
            return Mathf.Max(1, zoneTileCounts[index]);
        }

        public int GetZoneCount(int fallback = 2)
        {
            if (zoneTileCounts == null || zoneTileCounts.Count == 0)
            {
                return Mathf.Max(1, fallback);
            }

            return zoneTileCounts.Count;
        }

        public int GetZoneIdForRunIndex(int runZoneIndex, int fallbackZoneId = 1)
        {
            if (zoneIdsInRun == null || zoneIdsInRun.Count == 0)
            {
                return Mathf.Max(1, fallbackZoneId);
            }

            var index = Mathf.Clamp(runZoneIndex, 0, zoneIdsInRun.Count - 1);
            return Mathf.Max(1, zoneIdsInRun[index]);
        }

        public void EnsureZoneOrderMatchesZoneCount()
        {
            if (zoneTileCounts == null)
            {
                zoneTileCounts = new List<int>();
            }

            if (zoneIdsInRun == null)
            {
                zoneIdsInRun = new List<int>();
            }

            while (zoneIdsInRun.Count < zoneTileCounts.Count)
            {
                zoneIdsInRun.Add(zoneIdsInRun.Count + 1);
            }

            if (zoneIdsInRun.Count > zoneTileCounts.Count && zoneTileCounts.Count > 0)
            {
                zoneIdsInRun = zoneIdsInRun.Take(zoneTileCounts.Count).ToList();
            }
        }
    }
}
