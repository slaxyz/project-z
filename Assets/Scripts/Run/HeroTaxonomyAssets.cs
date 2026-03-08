using UnityEngine;

namespace ProjectZ.Run
{
    public abstract class HeroKeyedDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string id = "id";
        [SerializeField] private string displayName = "Name";
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public Sprite Icon => icon;
    }
}
