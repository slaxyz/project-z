using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "HeroPassive", menuName = "Project Z/Heroes/Passive")]
    public class HeroPassiveDefinitionAsset : HeroKeyedDefinitionAsset
    {
        [SerializeField, TextArea(2, 6)] private string description;

        public string Description => description;
    }
}
