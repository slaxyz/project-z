using UnityEngine;

namespace ProjectZ.Run
{
    [CreateAssetMenu(fileName = "HeroClass", menuName = "Project Z/Heroes/Class")]
    public class HeroClassDefinitionAsset : HeroKeyedDefinitionAsset
    {
        [SerializeField, TextArea(2, 6)] private string description;

        public string Description => description;
    }
}
