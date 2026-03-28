using ProjectZ.Run;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public class TeamCardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("States")]
        [SerializeField] private GameObject idleRoot;
        [SerializeField] private GameObject selectedRoot;

        [Header("Hero Selected")]
        [SerializeField] private Image splashArtImage;
        [SerializeField] private ClassBadgeView classBadgeView;
        [SerializeField] private TypeBadgeView typeBadgeView;
        [SerializeField] private TMP_Text heroNameLabel;
        [SerializeField] private TMP_Text heroNameShadowLabel;
        [SerializeField] private Image passiveIconBackgroundImage;
        [SerializeField] private Image passiveIconImage;
        [SerializeField] private TMP_Text passiveLabel;
        [SerializeField] private Transform starsRoot;
        [SerializeField] private Vector2 starSize = new Vector2(32f, 32f);

        [Header("Interaction")]
        [SerializeField] private Button button;
        [SerializeField] private Image clickSurface;

        public Button Button => button;
        public Vector3 DefaultScale => _defaultScale;
        public int SlotIndex { get; set; } = -1;
        public bool HasChampion => _hasChampion;
        public event System.Action<TeamCardView, PointerEventData> DragStarted;
        public event System.Action<TeamCardView, PointerEventData> DragMoved;
        public event System.Action<TeamCardView, PointerEventData> DragEnded;

        private readonly List<Image> _stars = new List<Image>();
        private Sprite _starSprite;
        private bool _hasChampion;
        private Vector3 _defaultScale = Vector3.one;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            AutoAssignIfNeeded();
            EnsureButton();
            _defaultScale = transform.localScale;
            EnsureCanvasGroup();
        }

        private void Reset()
        {
            AutoAssignIfNeeded();
            EnsureButton();
            _defaultScale = transform.localScale;
            EnsureCanvasGroup();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
        }

        public void ShowIdle()
        {
            AutoAssignIfNeeded();
            _hasChampion = false;

            if (idleRoot != null)
            {
                idleRoot.SetActive(true);
            }

            if (selectedRoot != null)
            {
                selectedRoot.SetActive(false);
            }
        }

        public void Bind(ChampionDefinitionAsset champion)
        {
            AutoAssignIfNeeded();

            if (champion == null)
            {
                ShowIdle();
                return;
            }

            _hasChampion = true;
            if (idleRoot != null)
            {
                idleRoot.SetActive(false);
            }

            if (selectedRoot != null)
            {
                selectedRoot.SetActive(true);
            }

            if (splashArtImage != null)
            {
                var art = champion.SplashSprite != null ? champion.SplashSprite : champion.AvatarSprite;
                splashArtImage.sprite = art;
                splashArtImage.color = art != null ? Color.white : Color.clear;
                splashArtImage.preserveAspect = true;
            }

            if (classBadgeView != null)
            {
                classBadgeView.SetClass(champion.ClassDefinition);
            }

            if (typeBadgeView != null)
            {
                typeBadgeView.SetType(champion.TypeDefinition);
            }

            if (heroNameLabel != null)
            {
                heroNameLabel.text = champion.DisplayName;
            }

            if (heroNameShadowLabel != null)
            {
                heroNameShadowLabel.text = champion.DisplayName;
            }

            if (passiveIconImage != null)
            {
                var passiveIcon = champion.PassiveDefinition != null ? champion.PassiveDefinition.Icon : null;
                passiveIconImage.sprite = passiveIcon;
                passiveIconImage.color = passiveIcon != null ? Color.white : Color.clear;
                passiveIconImage.preserveAspect = true;
            }

            if (passiveIconBackgroundImage != null)
            {
                var typeBackground = ResolveTypeBackgroundSprite(champion);
                passiveIconBackgroundImage.sprite = typeBackground;
                passiveIconBackgroundImage.color = typeBackground != null ? Color.white : Color.clear;
                passiveIconBackgroundImage.preserveAspect = true;
            }

            if (passiveLabel != null)
            {
                var passiveText = champion.PassiveDefinition != null && !string.IsNullOrWhiteSpace(champion.PassiveDefinition.Description)
                    ? champion.PassiveDefinition.Description
                    : champion.Description;
                passiveLabel.text = passiveText ?? string.Empty;
            }

            RefreshStars(champion.TierStars);
        }

        private void RefreshStars(int activeStars)
        {
            if (starsRoot == null)
            {
                return;
            }

            EnsureStarSprite();
            activeStars = Mathf.Clamp(activeStars, 0, 6);

            while (_stars.Count < activeStars)
            {
                var starGo = new GameObject("Star_" + (_stars.Count + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                starGo.transform.SetParent(starsRoot, false);

                var starRect = starGo.GetComponent<RectTransform>();
                starRect.sizeDelta = starSize;

                var starImage = starGo.GetComponent<Image>();
                starImage.sprite = _starSprite;
                starImage.color = _starSprite != null ? Color.white : Color.clear;
                starImage.preserveAspect = true;

                _stars.Add(starImage);
            }

            if (_stars.Count == 0)
            {
                for (var i = 0; i < starsRoot.childCount; i++)
                {
                    var existingStar = starsRoot.GetChild(i)?.GetComponent<Image>();
                    if (existingStar != null)
                    {
                        _stars.Add(existingStar);
                    }
                }
            }

            for (var i = 0; i < starsRoot.childCount; i++)
            {
                var star = starsRoot.GetChild(i);
                if (star == null)
                {
                    continue;
                }

                star.gameObject.SetActive(i < activeStars);
            }

            for (var i = 0; i < _stars.Count; i++)
            {
                var starImage = _stars[i];
                if (starImage == null)
                {
                    continue;
                }

                var active = i < activeStars;
                starImage.gameObject.SetActive(active);
                if (active)
                {
                    starImage.sprite = _starSprite;
                    starImage.color = _starSprite != null ? Color.white : Color.clear;
                    starImage.preserveAspect = true;
                }
            }
        }

        private void AutoAssignIfNeeded()
        {
            idleRoot ??= FindDeepChild("Idle")?.gameObject;
            selectedRoot ??= FindDeepChild("Hero_Selected")?.gameObject;

            if (splashArtImage == null)
            {
                splashArtImage = FindDeepChild("SplashArt")?.GetComponent<Image>();
            }

            if (classBadgeView == null)
            {
                classBadgeView = GetComponentInChildren<ClassBadgeView>(true);
            }

            if (typeBadgeView == null)
            {
                typeBadgeView = GetComponentInChildren<TypeBadgeView>(true);
            }

            if (heroNameLabel == null)
            {
                heroNameLabel = FindDeepChild("Label_Main")?.GetComponent<TMP_Text>();
            }

            if (heroNameShadowLabel == null)
            {
                heroNameShadowLabel = FindDeepChild("Label_Shadow")?.GetComponent<TMP_Text>();
            }

            if (passiveIconImage == null)
            {
                passiveIconImage = FindDeepChild("Icon_Passive")?.GetComponent<Image>();
            }

            if (passiveIconBackgroundImage == null)
            {
                passiveIconBackgroundImage = FindDeepChild("Icon_BG")?.GetComponent<Image>();
            }

            if (passiveLabel == null)
            {
                passiveLabel = FindDeepChild("Text_Passive")?.GetComponent<TMP_Text>();
            }

            if (starsRoot == null)
            {
                starsRoot = FindDeepChild("Stars");
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (clickSurface == null)
            {
                clickSurface = GetComponent<Image>();
            }
        }

        private void EnsureButton()
        {
            if (clickSurface == null)
            {
                clickSurface = GetComponent<Image>();
            }

            if (clickSurface == null)
            {
                clickSurface = gameObject.AddComponent<Image>();
                clickSurface.color = new Color(1f, 1f, 1f, 0f);
            }

            clickSurface.raycastTarget = true;

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }

            if (button.targetGraphic == null)
            {
                button.targetGraphic = clickSurface;
            }
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_hasChampion)
            {
                return;
            }

            EnsureCanvasGroup();
            _canvasGroup.blocksRaycasts = false;
            transform.localScale = _defaultScale * 1.1f;
            DragStarted?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_hasChampion)
            {
                return;
            }

            DragMoved?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_hasChampion)
            {
                return;
            }

            transform.localScale = _defaultScale;
            DragEnded?.Invoke(this, eventData);
            EnsureCanvasGroup();
            _canvasGroup.blocksRaycasts = true;
        }

        private void EnsureStarSprite()
        {
            if (_starSprite == null)
            {
                _starSprite = Resources.Load<Sprite>("Art/UI/Stars/champion_star");
            }
        }

        private Sprite ResolveTypeBackgroundSprite(ChampionDefinitionAsset champion)
        {
            if (champion == null || champion.TypeDefinition == null)
            {
                return null;
            }

            return champion.TypeDefinition.DefaultBadgeSprite;
        }

        private Transform FindDeepChild(string expectedName)
        {
            return FindDeepChild(transform, expectedName);
        }

        private static Transform FindDeepChild(Transform root, string expectedName)
        {
            if (root == null || string.IsNullOrWhiteSpace(expectedName))
            {
                return null;
            }

            var trimmedExpected = expectedName.Trim();
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.name.Trim() == trimmedExpected)
                {
                    return child;
                }

                var nested = FindDeepChild(child, trimmedExpected);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
