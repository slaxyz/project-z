using ProjectZ.Combat;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class StatusEffectView : MonoBehaviour
    {
        private static readonly Dictionary<int, Sprite> BackgroundCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text timerMainText;
        [SerializeField] private TMP_Text timerShadowText;

        private void Awake()
        {
            AutoAssign();
        }

        private void OnEnable()
        {
            AutoAssign();
        }

        private void OnValidate()
        {
            AutoAssign();
        }

        public void SetStatus(EnemyStatusEffectState status)
        {
            AutoAssign();

            if (status == null)
            {
                Clear();
                return;
            }

            ApplyBackground(status.BackgroundElement);
            ApplyIcon(status.IconResource);
            ApplyTimer(status.TurnsRemaining);
            gameObject.SetActive(true);
        }

        public void Clear()
        {
            if (backgroundImage != null)
            {
                backgroundImage.sprite = null;
                backgroundImage.color = Color.clear;
            }

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = Color.clear;
            }

            ApplyTimer(-1);
        }

        private void ApplyBackground(ElementType backgroundElement)
        {
            if (backgroundImage == null)
            {
                return;
            }

            var sprite = LoadBackgroundSprite(backgroundElement);
            backgroundImage.sprite = sprite;
            backgroundImage.color = sprite != null ? Color.white : Color.clear;
            backgroundImage.preserveAspect = true;
        }

        private void ApplyIcon(string iconResource)
        {
            if (iconImage == null)
            {
                return;
            }

            var sprite = LoadIconSprite(iconResource);
            iconImage.sprite = sprite;
            iconImage.color = sprite != null ? Color.white : Color.clear;
            iconImage.preserveAspect = true;
        }

        private void ApplyTimer(int turnsRemaining)
        {
            var value = turnsRemaining < 0 ? string.Empty : Mathf.Max(0, turnsRemaining).ToString();
            if (timerMainText != null)
            {
                timerMainText.text = value;
            }

            if (timerShadowText != null)
            {
                timerShadowText.text = value;
            }
        }

        private static Sprite LoadBackgroundSprite(ElementType element)
        {
            var cacheKey = (int)element;
            if (BackgroundCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var resourceIndex = Mathf.Clamp(cacheKey + 1, 1, 6);
            var sprite = Resources.Load<Sprite>("Art/UI/TypeBackgrounds/" + resourceIndex);
            BackgroundCache[cacheKey] = sprite;
            return sprite;
        }

        private static Sprite LoadIconSprite(string iconResource)
        {
            var key = string.IsNullOrWhiteSpace(iconResource) ? string.Empty : iconResource.Trim();
            if (IconCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                IconCache[key] = null;
                return null;
            }

            var sprite = Resources.Load<Sprite>(key.Contains("/") ? key : "Art/UI/SystemIcons/Passives/" + key);
            IconCache[key] = sprite;
            return sprite;
        }

        private void AutoAssign()
        {
            if (backgroundImage == null)
            {
                backgroundImage = transform.Find("BG")?.GetComponent<Image>();
            }

            if (iconImage == null)
            {
                iconImage = transform.Find("Icon")?.GetComponent<Image>();
            }

            if (timerMainText == null || timerShadowText == null)
            {
                var timer = transform.Find("Timer");
                if (timer != null)
                {
                    if (timerMainText == null)
                    {
                        timerMainText = FindText(timer, "Label_Main");
                    }

                    if (timerShadowText == null)
                    {
                        timerShadowText = FindText(timer, "Label_Shadow");
                    }
                }
            }
        }

        private static TMP_Text FindText(Transform root, string targetName)
        {
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == targetName)
                {
                    return texts[i];
                }
            }

            return null;
        }
    }
}
