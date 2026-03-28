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
        [SerializeField] private bool applyOnStart = true;

        private void Start()
        {
            AutoAssignIfNeeded();

            if (!applyOnStart)
            {
                return;
            }

            SetType(previewType);
        }

        private void OnEnable()
        {
            AutoAssignIfNeeded();

            if (!applyOnStart)
            {
                return;
            }

            SetType(previewType);
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();

            if (!applyOnStart)
            {
                return;
            }

            SetType(previewType);
        }

        private void Reset()
        {
            AutoAssignIfNeeded();
        }

        public void SetType(HeroTypeDefinitionAsset typeDefinition)
        {
            AutoAssignIfNeeded();

            if (typeDefinition == null)
            {
                ClearVisual();
                return;
            }

            if (backgroundLayer != null)
            {
                backgroundLayer.sprite = typeDefinition.DefaultBadgeSprite;
                backgroundLayer.color = backgroundLayer.sprite != null ? Color.white : Color.clear;
                backgroundLayer.preserveAspect = true;
            }

            if (iconLayer != null)
            {
                var iconSprite = typeDefinition.RuntimeIcon;
                iconLayer.sprite = iconSprite;
                iconLayer.color = iconSprite != null ? Color.white : Color.clear;
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

        private void AutoAssignIfNeeded()
        {
            if (backgroundLayer == null)
            {
                backgroundLayer = transform.Find("BG")?.GetComponent<Image>();
                if (backgroundLayer == null)
                {
                    backgroundLayer = transform.Find("Fill")?.GetComponent<Image>();
                }
                if (backgroundLayer == null)
                {
                    backgroundLayer = GetComponent<Image>();
                }
            }

            if (iconLayer == null)
            {
                iconLayer = transform.Find("Icon")?.GetComponent<Image>();
            }
        }
    }
}
