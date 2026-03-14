using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "ZoneData", menuName = "Project Z/Run/Zone Data")]
    public class ZoneDataAsset : ScriptableObject
    {
        [SerializeField] private string zoneName = "Zone";
        [SerializeField] private int difficulty = 1;
        [SerializeField] private int zoneId = 1;
        [SerializeField] private int musicId = 1;

        public string ZoneName => zoneName;
        public int Difficulty => Mathf.Max(0, difficulty);
        public int ZoneId => Mathf.Max(0, zoneId);
        public int MusicId => Mathf.Max(0, musicId);
    }
}
