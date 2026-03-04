using System.Collections.Generic;
using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "ChampionCatalog", menuName = "ProjectZ/Run/Champion Catalog")]
    public class ChampionCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<ChampionDefinitionAsset> champions = new List<ChampionDefinitionAsset>();

        public IReadOnlyList<ChampionDefinitionAsset> Champions
        {
            get { return champions; }
        }
    }
}
