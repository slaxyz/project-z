using System.Collections.Generic;
using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "RunLoopConfig", menuName = "Project Z/Run/Run Loop Config")]
    public class RunLoopConfigAsset : ScriptableObject
    {
        [SerializeField] private List<int> zoneTileCounts = new List<int> { 4, 4 };
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
    }
}
