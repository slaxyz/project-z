using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "ZoneDatabase", menuName = "Project Z/Run/Zone Database")]
    public class ZoneDatabaseAsset : ScriptableObject
    {
        [SerializeField] private List<ZoneDataAsset> zones = new List<ZoneDataAsset>();

        public IReadOnlyList<ZoneDataAsset> Zones => zones;

        public ZoneDataAsset GetZoneById(int zoneId)
        {
            return zones.FirstOrDefault(zone => zone != null && zone.ZoneId == zoneId);
        }

        public ZoneDataAsset GetZoneByIndex(int zoneIndex)
        {
            if (zones == null || zones.Count == 0)
            {
                return null;
            }

            var clampedIndex = Mathf.Clamp(zoneIndex, 0, zones.Count - 1);
            return zones[clampedIndex];
        }
    }
}
