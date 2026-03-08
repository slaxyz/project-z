using ProjectZ.Run;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [ExecuteAlways]
    public class ClassBadgeView : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] private Image backgroundLayer;
        [SerializeField] private Image iconLayer;

        [Header("Preview")]
        [SerializeField] private HeroClassDefinitionAsset previewClass;
        [SerializeField] private bool applyOnStart = true;

        private void Start()
        {
            if (!applyOnStart)
            {
                return;
            }

            SetClass(previewClass);
        }

        private void OnEnable()
        {
            if (!applyOnStart)
            {
                return;
            }

            SetClass(previewClass);
        }

        private void OnValidate()
        {
            if (!applyOnStart)
            {
                return;
            }

            SetClass(previewClass);
        }

        public void SetClass(HeroClassDefinitionAsset classDefinition)
        {
            if (classDefinition == null)
            {
                ClearVisual();
                return;
            }

            if (backgroundLayer != null)
            {
                backgroundLayer.sprite = classDefinition.BadgeSprite;
                backgroundLayer.color = backgroundLayer.sprite != null ? Color.white : Color.clear;
                backgroundLayer.preserveAspect = true;
            }

            if (iconLayer != null)
            {
                iconLayer.sprite = classDefinition.RuntimeIcon;
                iconLayer.color = iconLayer.sprite != null ? Color.white : Color.clear;
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
