using System;
using System.Collections.Generic;
using System.Linq;
using ProjectZ.Core;
using ProjectZ.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HeroSwitchSlotView : MonoBehaviour
    {
        [Header("State Roots")]
        [SerializeField] private GameObject selectedRoot;
        [SerializeField] private GameObject notSelectedRoot;

        [Header("Content")]
        [SerializeField] private Image[] selectedAvatarTargets;
        [SerializeField] private Image[] notSelectedAvatarTargets;
        [SerializeField] private TMP_Text[] nameTargets;
        [SerializeField] private Button button;
        [SerializeField] private Image hitAreaGraphic;
        [SerializeField, Range(0f, 1f)] private float hitAreaExpandPercent = 0.25f;

        [Header("Binding")]
        [SerializeField] private bool autoBindFromCurrentRun = true;
        [SerializeField] private int slotIndex = -1;
        [SerializeField] private string championId;
        [SerializeField] private bool startSelected;

        [Header("Editor Preview")]
        [SerializeField] private bool useManualPreview;
        [SerializeField] private string previewChampionId;
        [SerializeField] private bool previewSelected;

        private ChampionDefinitionAsset _champion;
        private bool _isSelected;
        private bool _buttonHooked;

        public string ChampionId => _champion != null ? _champion.Id : championId;
        public bool IsSelected => _isSelected;
        public int SlotIndex => ResolveSlotIndex();
        public ChampionDefinitionAsset Champion => _champion;

        public event Action<HeroSwitchSlotView> Clicked;
        public event Action<HeroSwitchSlotView> Selected;

        private void Awake()
        {
            AutoAssignIfNeeded();
            EnsureHitAreaGraphic();
            EnsureButton();
            RefreshAllVisuals();
        }

        private void Start()
        {
            if (ShouldUseManualPreview())
            {
                ApplyManualPreview();
                return;
            }

            TryBindFromCurrentRunIfNeeded();
            RefreshHitAreaPadding();

            if (startSelected && HasChampion())
            {
                Select();
                return;
            }

            EnsureFallbackSelection();
        }

        private void Reset()
        {
            AutoAssignIfNeeded();
            EnsureHitAreaGraphic();
            RefreshHitAreaPadding();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
            EnsureHitAreaGraphic();
            RefreshHitAreaPadding();
            if (ShouldUseManualPreview())
            {
                ApplyManualPreview();
                return;
            }

            ApplyChampionVisuals();
            RefreshVisuals(Application.isPlaying);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshHitAreaPadding();
        }

        public void Bind(ChampionDefinitionAsset champion)
        {
            _champion = champion;
            championId = champion != null ? champion.Id : string.Empty;
            RefreshAllVisuals();
        }

        public void BindChampionId(string nextChampionId)
        {
            championId = nextChampionId ?? string.Empty;
            _champion = string.IsNullOrWhiteSpace(championId) ? null : ChampionCatalog.FindById(championId);
            RefreshAllVisuals();
        }

        public void ClearBinding()
        {
            championId = string.Empty;
            _champion = null;
            RefreshAllVisuals();
        }

        public void SetSlotIndex(int index)
        {
            slotIndex = index;
        }

        public void Select()
        {
            if (!HasChampion())
            {
                return;
            }

            var siblings = CollectSiblingSlots();
            for (var i = 0; i < siblings.Count; i++)
            {
                var sibling = siblings[i];
                if (sibling == null)
                {
                    continue;
                }

                sibling.ApplySelectedState(sibling == this);
            }

            Selected?.Invoke(this);
        }

        public void SetSelected(bool isSelected)
        {
            ApplySelectedState(isSelected);
        }

        private void HandleClicked()
        {
            Clicked?.Invoke(this);

            if (_isSelected || !HasChampion())
            {
                return;
            }

            Select();
        }

        private void ApplySelectedState(bool isSelected)
        {
            _isSelected = isSelected;
            RefreshVisuals(Application.isPlaying);
        }

        private void RefreshAllVisuals()
        {
            ApplyChampionVisuals();
            RefreshVisuals(Application.isPlaying);
        }

        private void RefreshVisuals(bool allowRootToggle)
        {
            if (allowRootToggle && selectedRoot != null)
            {
                selectedRoot.SetActive(_isSelected);
            }

            if (allowRootToggle && notSelectedRoot != null)
            {
                notSelectedRoot.SetActive(!_isSelected);
            }

            RefreshRaycastTargets();
        }

        private void ApplyChampionVisuals()
        {
            var selectedAvatar = ResolveSelectedAvatarSprite();
            for (var i = 0; i < selectedAvatarTargets.Length; i++)
            {
                var target = selectedAvatarTargets[i];
                if (target == null)
                {
                    continue;
                }

                target.sprite = selectedAvatar;
                target.color = selectedAvatar != null ? Color.white : Color.clear;
                target.preserveAspect = true;
            }

            var notSelectedAvatar = ResolveNotSelectedAvatarSprite();
            for (var i = 0; i < notSelectedAvatarTargets.Length; i++)
            {
                var target = notSelectedAvatarTargets[i];
                if (target == null)
                {
                    continue;
                }

                target.sprite = notSelectedAvatar;
                target.color = notSelectedAvatar != null ? Color.white : Color.clear;
                target.preserveAspect = true;
            }

            var label = ResolveDisplayName();
            for (var i = 0; i < nameTargets.Length; i++)
            {
                var target = nameTargets[i];
                if (target == null)
                {
                    continue;
                }

                target.text = label;
            }
        }

        private void AutoAssignIfNeeded()
        {
            if (selectedRoot == null)
            {
                selectedRoot = FindDeepChild("State_Selected")?.gameObject;
            }

            if (notSelectedRoot == null)
            {
                notSelectedRoot = FindDeepChild("State_NotSelected")?.gameObject;
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (hitAreaGraphic == null)
            {
                hitAreaGraphic = GetComponent<Image>();
            }

            if (selectedAvatarTargets == null || selectedAvatarTargets.Length == 0)
            {
                selectedAvatarTargets = FindChildrenUnderRoot<Image>(selectedRoot != null ? selectedRoot.transform : null, "Avatar");
            }

            if (notSelectedAvatarTargets == null || notSelectedAvatarTargets.Length == 0)
            {
                notSelectedAvatarTargets = FindChildrenUnderRoot<Image>(notSelectedRoot != null ? notSelectedRoot.transform : null, "Avatar");
            }

            if (nameTargets == null || nameTargets.Length == 0)
            {
                nameTargets = FindDeepChildren<TMP_Text>("Label_Main", "Label_Shadow");
            }
        }

        private void EnsureButton()
        {
            if (button == null)
            {
                button = gameObject.GetComponent<Button>();
            }

            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }

            if (!_buttonHooked)
            {
                button.onClick.AddListener(HandleClicked);
                _buttonHooked = true;
            }

            if (hitAreaGraphic != null)
            {
                button.targetGraphic = hitAreaGraphic;
            }
        }

        private void EnsureHitAreaGraphic()
        {
            if (hitAreaGraphic == null)
            {
                hitAreaGraphic = GetComponent<Image>();
            }

            if (hitAreaGraphic == null)
            {
                hitAreaGraphic = gameObject.AddComponent<Image>();
            }

            hitAreaGraphic.color = new Color(1f, 1f, 1f, 0f);
            hitAreaGraphic.raycastTarget = true;
            hitAreaGraphic.maskable = false;
        }

        private void RefreshHitAreaPadding()
        {
            if (hitAreaGraphic == null)
            {
                return;
            }

            var rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            var rect = rectTransform.rect;
            var horizontalPadding = rect.width * hitAreaExpandPercent * 0.5f;
            var verticalPadding = rect.height * hitAreaExpandPercent * 0.5f;
            hitAreaGraphic.raycastPadding = new Vector4(
                -horizontalPadding,
                -verticalPadding,
                -horizontalPadding,
                -verticalPadding);
        }

        private void RefreshRaycastTargets()
        {
            SetRaycastTargets(selectedRoot, false);
            SetRaycastTargets(notSelectedRoot, true);
        }

        private void SetRaycastTargets(GameObject root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                graphic.raycastTarget = enabled;
            }
        }

        private void TryBindFromCurrentRunIfNeeded()
        {
            if (!autoBindFromCurrentRun || !string.IsNullOrWhiteSpace(ChampionId))
            {
                RefreshAllVisuals();
                return;
            }

            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                RefreshAllVisuals();
                return;
            }

            var selectedIds = manager.SelectedChampionIds();
            var resolvedSlotIndex = ResolveSlotIndex();
            if (resolvedSlotIndex < 0 || resolvedSlotIndex >= selectedIds.Count)
            {
                RefreshAllVisuals();
                return;
            }

            BindChampionId(selectedIds[resolvedSlotIndex]);
        }

        private void EnsureFallbackSelection()
        {
            var siblings = CollectSiblingSlots();
            var hasSelectedSibling = siblings.Any(slot => slot != null && slot._isSelected);
            if (hasSelectedSibling)
            {
                RefreshVisuals(Application.isPlaying);
                return;
            }

            var firstFilled = siblings.FirstOrDefault(slot => slot != null && slot.HasChampion());
            if (firstFilled == null)
            {
                if (siblings.Count > 0 && siblings[0] == this)
                {
                    ApplySelectedState(true);
                }
                else
                {
                    RefreshVisuals(Application.isPlaying);
                }

                return;
            }

            ApplySelectedState(firstFilled == this);
        }

        private bool HasChampion()
        {
            return !string.IsNullOrWhiteSpace(ChampionId);
        }

        private Sprite ResolveAvatarSprite()
        {
            if (_champion == null && !string.IsNullOrWhiteSpace(championId))
            {
                _champion = ChampionCatalog.FindById(championId);
            }

            if (_champion == null)
            {
                return null;
            }

            return _champion.AvatarSprite != null ? _champion.AvatarSprite : _champion.SplashSprite;
        }

        private Sprite ResolveSelectedAvatarSprite()
        {
            var normalizedId = ResolveChampionResourceId();
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                return null;
            }

            return TryLoadCharacterSprite(normalizedId, BuildPascalCase(normalizedId) + "_clear")
                ?? TryLoadCharacterSprite(normalizedId, normalizedId + "_clear")
                ?? ResolveAvatarSprite();
        }

        private Sprite ResolveNotSelectedAvatarSprite()
        {
            var normalizedId = ResolveChampionResourceId();
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                return null;
            }

            return TryLoadCharacterSprite(normalizedId, normalizedId + "_splashBlack")
                ?? TryLoadCharacterSprite(normalizedId, normalizedId + "_avatarBlack")
                ?? ResolveAvatarSprite();
        }

        private string ResolveDisplayName()
        {
            if (_champion == null && !string.IsNullOrWhiteSpace(championId))
            {
                _champion = ChampionCatalog.FindById(championId);
            }

            if (_champion != null && !string.IsNullOrWhiteSpace(_champion.DisplayName))
            {
                return _champion.DisplayName;
            }

            return championId ?? string.Empty;
        }

        private string ResolveChampionResourceId()
        {
            var rawId = ChampionId;
            if (string.IsNullOrWhiteSpace(rawId))
            {
                return string.Empty;
            }

            return rawId.Trim().ToLowerInvariant();
        }

        private static string BuildPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.Length == 1)
            {
                return value.ToUpperInvariant();
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static Sprite TryLoadCharacterSprite(string championResourceId, string spriteName)
        {
            if (string.IsNullOrWhiteSpace(championResourceId) || string.IsNullOrWhiteSpace(spriteName))
            {
                return null;
            }

            return Resources.Load<Sprite>("Art/Characters/" + championResourceId + "/" + spriteName);
        }

        private int ResolveSlotIndex()
        {
            if (slotIndex >= 0)
            {
                return slotIndex;
            }

            var siblings = CollectSiblingSlots();
            for (var i = 0; i < siblings.Count; i++)
            {
                if (siblings[i] == this)
                {
                    return i;
                }
            }

            return transform.GetSiblingIndex();
        }

        private List<HeroSwitchSlotView> CollectSiblingSlots()
        {
            var root = transform.parent;
            if (root == null)
            {
                return new List<HeroSwitchSlotView> { this };
            }

            return root
                .GetComponentsInChildren<HeroSwitchSlotView>(true)
                .OrderBy(slot => slot.transform.GetSiblingIndex())
                .ToList();
        }

        private Transform FindDeepChild(string childName)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child != transform && child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private T[] FindDeepChildren<T>(params string[] childNames) where T : Component
        {
            var targets = new List<T>();
            var lookup = new HashSet<string>(childNames);

            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform || !lookup.Contains(child.name))
                {
                    continue;
                }

                var component = child.GetComponent<T>();
                if (component != null)
                {
                    targets.Add(component);
                }
            }

            return targets.ToArray();
        }

        private static T[] FindChildrenUnderRoot<T>(Transform root, params string[] childNames) where T : Component
        {
            if (root == null)
            {
                return Array.Empty<T>();
            }

            var targets = new List<T>();
            var lookup = new HashSet<string>(childNames);

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root || !lookup.Contains(child.name))
                {
                    continue;
                }

                var component = child.GetComponent<T>();
                if (component != null)
                {
                    targets.Add(component);
                }
            }

            return targets.ToArray();
        }

        private bool ShouldUseManualPreview()
        {
            return useManualPreview && !Application.isPlaying;
        }

        private void ApplyManualPreview()
        {
            _champion = null;
            championId = previewChampionId ?? string.Empty;
            _isSelected = previewSelected;
            RefreshAllVisuals();
        }
    }
}
