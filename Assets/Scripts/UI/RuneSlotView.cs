using System;
using System.Collections.Generic;
using ProjectZ.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class RuneSlotView : MonoBehaviour
    {
        private static readonly Dictionary<ElementType, Sprite> IconCache = new Dictionary<ElementType, Sprite>();
        private static readonly Dictionary<ElementType, Sprite> BackgroundCache = new Dictionary<ElementType, Sprite>();

        [SerializeField] private GameObject enableRoot;
        [SerializeField] private GameObject lockRoot;
        [SerializeField] private Image enableBackground;
        [SerializeField] private Image lockBackground;
        [SerializeField] private Image enableIcon;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Image hitAreaGraphic;
        [SerializeField] private Button button;

        private int _slotIndex = -1;
        private ElementType _element;
        private bool _isLocked;
        private bool _isAvailable;

        public int SlotIndex => _slotIndex;
        public event Action<RuneSlotView> Clicked;

        private void Awake()
        {
            AutoAssign();
            EnsureButton();
        }

        private void OnEnable()
        {
            EnsureButton();
            HookButton();
            ApplyVisuals();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        private void OnValidate()
        {
            AutoAssign();

            if (Application.isPlaying)
            {
                return;
            }

            ApplyVisuals();
        }

        public void BindSlotIndex(int slotIndex)
        {
            _slotIndex = slotIndex;
        }

        public void ApplyState(ElementType element, bool isLocked, bool isAvailable)
        {
            _element = element;
            _isLocked = isLocked;
            _isAvailable = isAvailable;

            ApplyVisuals();
        }

        private void HandleClicked()
        {
            Clicked?.Invoke(this);
        }

        private void HookButton()
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }

        private void EnsureButton()
        {
            if (button == null)
            {
                TryGetComponent(out button);
            }

            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }

            if (button.targetGraphic == null && hitAreaGraphic != null)
            {
                button.targetGraphic = hitAreaGraphic;
            }
        }

        private void AutoAssign()
        {
            if (enableRoot == null)
            {
                var found = transform.Find("Enable");
                if (found != null)
                {
                    enableRoot = found.gameObject;
                }
            }

            if (lockRoot == null)
            {
                var found = transform.Find("Lock");
                if (found != null)
                {
                    lockRoot = found.gameObject;
                }
            }

            if (enableIcon == null && enableRoot != null)
            {
                var icon = enableRoot.transform.Find("Icon");
                if (icon != null)
                {
                    enableIcon = icon.GetComponent<Image>();
                }
            }

            if (enableBackground == null && enableRoot != null)
            {
                var background = enableRoot.transform.Find("BG");
                if (background != null)
                {
                    enableBackground = background.GetComponent<Image>();
                }
            }

            if (lockIcon == null && lockRoot != null)
            {
                var icon = lockRoot.transform.Find("Icon");
                if (icon != null)
                {
                    lockIcon = icon.GetComponent<Image>();
                }
            }

            if (lockBackground == null && lockRoot != null)
            {
                var background = lockRoot.transform.Find("BG");
                if (background != null)
                {
                    lockBackground = background.GetComponent<Image>();
                }
            }

            if (hitAreaGraphic == null)
            {
                var overlay = transform.Find("Overlay");
                if (overlay != null)
                {
                    hitAreaGraphic = overlay.GetComponent<Image>();
                }
            }
        }

        private void ApplyVisuals()
        {
            if (enableRoot != null)
            {
                enableRoot.SetActive(!_isLocked);
            }

            if (lockRoot != null)
            {
                lockRoot.SetActive(_isLocked);
            }

            var backgroundSprite = LoadBackgroundSprite(_element);
            if (enableBackground != null)
            {
                enableBackground.sprite = backgroundSprite;
            }

            if (lockBackground != null)
            {
                lockBackground.sprite = backgroundSprite;
            }

            var iconSprite = LoadElementIcon(_element);
            if (enableIcon != null)
            {
                enableIcon.sprite = iconSprite;
            }

            if (lockIcon != null)
            {
                lockIcon.sprite = iconSprite;
            }

            if (button != null)
            {
                button.interactable = _isLocked || _isAvailable;
            }
        }

        private static Sprite LoadElementIcon(ElementType element)
        {
            if (IconCache.TryGetValue(element, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = Resources.Load<Sprite>("Art/UI/TypeIcons/" + GetVisualIndex(element));
            IconCache[element] = sprite;
            return sprite;
        }

        private static Sprite LoadBackgroundSprite(ElementType element)
        {
            if (BackgroundCache.TryGetValue(element, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = Resources.Load<Sprite>("Art/UI/TypeSpirals/" + GetVisualIndex(element));
            BackgroundCache[element] = sprite;
            return sprite;
        }

        private static int GetVisualIndex(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:
                    return 1;
                case ElementType.Nature:
                    return 2;
                case ElementType.Water:
                    return 3;
                case ElementType.Poison:
                    return 4;
                case ElementType.Ground:
                    return 5;
                case ElementType.Mystic:
                    return 6;
                default:
                    return 1;
            }
        }
    }
}
