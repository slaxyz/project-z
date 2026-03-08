using ProjectZ.Run;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [ExecuteAlways]
    public class TypeBadgeView : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] private Image backgroundLayer;
        [SerializeField] private Image iconLayer;

        [Header("Preview")]
        [SerializeField] private HeroTypeDefinitionAsset previewType;
        [SerializeField] private bool previewAsGem = true;
        [SerializeField] private bool applyOnStart = true;

        private void Start()
        {
            if (!applyOnStart)
            {
                return;
            }

            SetType(previewType, previewAsGem);
        }

        private void OnEnable()
        {
            if (!applyOnStart)
            {
                return;
            }

            SetType(previewType, previewAsGem);
        }

        private void OnValidate()
        {
            if (!applyOnStart)
            {
                return;
            }

            SetType(previewType, previewAsGem);
        }

        public void SetType(HeroTypeDefinitionAsset typeDefinition, bool showSpiral)
        {
            if (typeDefinition == null)
            {
                ClearVisual();
                return;
            }

            if (backgroundLayer != null)
            {
                var backgroundSprite = showSpiral
                    ? (typeDefinition.GemBadgeSprite != null ? typeDefinition.GemBadgeSprite : typeDefinition.DefaultBadgeSprite)
                    : typeDefinition.DefaultBadgeSprite;

                backgroundLayer.sprite = backgroundSprite;
                backgroundLayer.color = backgroundLayer.sprite != null ? Color.white : Color.clear;
                backgroundLayer.preserveAspect = true;
            }

            if (iconLayer != null)
            {
                iconLayer.sprite = typeDefinition.Icon;
                iconLayer.color = typeDefinition.Icon != null ? Color.white : Color.clear;
                iconLayer.preserveAspect = true;
            }
        }

        private void ClearVisual()
        {
            if (backgroundLayer != null)
            {
                backgroundLayer.sprite = null;
                backgroundLayer.color = Color.clear;
            }

            if (iconLayer != null)
            {
                iconLayer.sprite = null;
                iconLayer.color = Color.clear;
            }
        }
    }
}
