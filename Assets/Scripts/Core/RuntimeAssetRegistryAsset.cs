using ProjectZ.Combat;
using ProjectZ.Run;
using UnityEngine;

namespace ProjectZ.Core
{
    [CreateAssetMenu(fileName = "RuntimeAssetRegistry", menuName = "Project Z/Core/Runtime Asset Registry")]
    public class RuntimeAssetRegistryAsset : ScriptableObject
    {
        private const string ResourcePath = "Runtime/RuntimeAssetRegistry";
        private static RuntimeAssetRegistryAsset _cached;

        [SerializeField] private ChampionCatalogAsset championCatalog;
        [SerializeField] private RunLoopConfigAsset runLoopConfig;
        [SerializeField] private ZoneDatabaseAsset zoneDatabase;
        [SerializeField] private CombatSpellLibraryAsset spellLibrary;
        [SerializeField] private EnemyCatalogAsset enemyCatalog;
        [SerializeField] private CombatSpawnRulesAsset spawnRules;

        public ChampionCatalogAsset ChampionCatalog => championCatalog;
        public RunLoopConfigAsset RunLoopConfig => runLoopConfig;
        public ZoneDatabaseAsset ZoneDatabase => zoneDatabase;
        public CombatSpellLibraryAsset SpellLibrary => spellLibrary;
        public EnemyCatalogAsset EnemyCatalog => enemyCatalog;
        public CombatSpawnRulesAsset SpawnRules => spawnRules;

        public static RuntimeAssetRegistryAsset Load()
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Resources.Load<RuntimeAssetRegistryAsset>(ResourcePath);
            if (_cached == null)
            {
                Debug.LogWarning("RuntimeAssetRegistry missing at Resources/Runtime/RuntimeAssetRegistry.");
            }

            return _cached;
        }
    }
}
