using System.Collections.Generic;
using System.Linq;
using ProjectZ.Combat;
using ProjectZ.Core;
using ProjectZ.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace ProjectZ.UI
{
    public enum ChampionSortMode
    {
        CatalogOrder = 0,
        TierAsc = 1,
        TierDesc = 2,
        PackByElement = 3
    }

    public struct ChampionFilter
    {
        public int minTier;
        public int maxTier;
        public ElementType? element;
        public ChampionClassType? championClass;
    }

    public class CollectionSceneController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != GameScenes.Collection)
            {
                return;
            }

            if (FindFirstObjectByType<CollectionSceneController>() != null)
            {
                return;
            }

            var go = new GameObject("CollectionSceneController");
            go.AddComponent<CollectionSceneController>();
        }

        private sealed class CarouselItemView
        {
            public string championId;
            public Image background;
            public Text label;
            public Text status;
            public Button button;
        }

        private readonly List<CarouselItemView> _itemViews = new List<CarouselItemView>();
        private readonly List<ChampionDefinitionAsset> _visibleChampions = new List<ChampionDefinitionAsset>();

        private GameFlowManager _manager;
        private Font _font;

        private ChampionSortMode _sortMode = ChampionSortMode.TierAsc;
        private ChampionFilter _filter;
        private string _selectedChampionId = string.Empty;
        private string _feedbackMessage = string.Empty;

        private Text _coinsText;
        private Text _detailText;
        private Text _filterSortPlaceholderText;
        private Button _sortButton;
        private Text _sortButtonText;
        private Image _sortButtonImage;
        private Button _filterButton;
        private Text _filterButtonText;
        private Image _filterButtonImage;
        private GameObject _sortPopup;
        private GameObject _filterPopup;
        private Text _tierFilterStatusText;
        private Text _elementFilterStatusText;
        private Text _classFilterStatusText;
        private Image _splashImage;
        private Text _splashFallbackText;
        private Text _feedbackText;
        private Button _unlockButton;
        private Text _unlockButtonText;
        private RectTransform _safeAreaRoot;
        private Rect _lastSafeArea;

        private readonly Color _selectedColor = new Color(1f, 0.85f, 0.45f, 1f);
        private readonly Color _unlockedColor = new Color(0.45f, 0.9f, 0.55f, 1f);
        private readonly Color _lockedAffordableColor = new Color(1f, 0.65f, 0.3f, 1f);
        private readonly Color _lockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        private readonly Color _controlButtonIdleColor = new Color(0.22f, 0.22f, 0.26f, 0.95f);
        private readonly Color _controlButtonActiveColor = new Color(0.34f, 0.42f, 0.56f, 0.98f);

        private void Start()
        {
            _manager = GameFlowManager.Instance;
            if (_manager == null)
            {
                Debug.LogWarning("CollectionSceneController: GameFlowManager not found.");
                enabled = false;
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                Debug.LogWarning("CollectionSceneController: built-in font LegacyRuntime.ttf not found.");
                enabled = false;
                return;
            }

            _filter.minTier = 3;
            _filter.maxTier = 6;
            _filter.element = null;
            _filter.championClass = null;

            EnsureEventSystem();
            BuildLayout();
            ApplySafeArea(true);
            RefreshAndRender();
        }

        private void Update()
        {
            ApplySafeArea(false);
            HandlePopupOutsideClick();
        }

        private void RefreshAndRender()
        {
            _visibleChampions.Clear();
            _visibleChampions.AddRange(ApplyFilterAndSort(_manager.GetChampionCatalog()));

            if (_visibleChampions.Count == 0)
            {
                _selectedChampionId = string.Empty;
                _feedbackMessage = "No champions available in catalog.";
            }
            else if (string.IsNullOrWhiteSpace(_selectedChampionId) || !ContainsChampion(_selectedChampionId))
            {
                _selectedChampionId = _manager.GetDefaultSelectedChampionIdForCollection();
                if (string.IsNullOrWhiteSpace(_selectedChampionId) || !ContainsChampion(_selectedChampionId))
                {
                    _selectedChampionId = _visibleChampions[0].Id;
                }
            }

            RebuildCarousel();
            RefreshTopPanel();
        }

        private void RebuildCarousel()
        {
            foreach (var itemView in _itemViews)
            {
                if (itemView != null && itemView.button != null)
                {
                    Destroy(itemView.button.gameObject);
                }
            }

            _itemViews.Clear();

            var content = GameObject.Find("CollectionCarouselContent");
            if (content == null)
            {
                return;
            }

            foreach (var champion in _visibleChampions)
            {
                var view = CreateCarouselItem(content.transform, champion);
                _itemViews.Add(view);
            }
        }

        private CarouselItemView CreateCarouselItem(Transform parent, ChampionDefinitionAsset champion)
        {
            var root = CreateUIObject("ChampionItem_" + champion.Id, parent);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(236f, 108f);

            var image = root.AddComponent<Image>();
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText("Label", root.transform, 14, TextAnchor.UpperLeft);
            label.rectTransform.anchorMin = new Vector2(0.05f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0.95f, 0.95f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            var status = CreateText("Status", root.transform, 11, TextAnchor.LowerLeft);
            status.rectTransform.anchorMin = new Vector2(0.05f, 0.05f);
            status.rectTransform.anchorMax = new Vector2(0.95f, 0.45f);
            status.rectTransform.offsetMin = Vector2.zero;
            status.rectTransform.offsetMax = Vector2.zero;

            button.onClick.AddListener(() =>
            {
                _selectedChampionId = champion.Id;
                _feedbackMessage = string.Empty;
                RefreshTopPanel();
            });

            return new CarouselItemView
            {
                championId = champion.Id,
                background = image,
                button = button,
                label = label,
                status = status
            };
        }

        private void RefreshTopPanel()
        {
            _coinsText.text = "Coins: " + _manager.GetPlayerCoins();
            _filterSortPlaceholderText.text = BuildFilterSummaryLabel();
            _sortButtonText.text = "Tri";
            _filterButtonText.text = "Filtre" + (CountActiveFilters() > 0 ? " (" + CountActiveFilters() + ")" : string.Empty);
            _tierFilterStatusText.text = "Tier: " + _filter.minTier + "-" + _filter.maxTier + "★";
            _elementFilterStatusText.text = "Element: " + ToElementLabel(_filter.element);
            _classFilterStatusText.text = "Type: " + ToClassLabel(_filter.championClass);
            UpdateControlVisualState();

            var selected = GetSelectedChampion();
            if (selected == null)
            {
                _detailText.text = "No champion selected.";
                _unlockButton.gameObject.SetActive(false);
                _feedbackText.text = _feedbackMessage;
                _splashImage.sprite = null;
                _splashImage.color = Color.black;
                _splashFallbackText.text = "No splash";
                RefreshCarouselVisuals();
                return;
            }

            var unlocked = _manager.IsChampionUnlocked(selected.Id);
            var coins = _manager.GetPlayerCoins();
            var affordable = coins >= selected.UnlockCost;

            _detailText.text =
                selected.DisplayName + "\n" +
                "Pseudo: " + selected.Pseudo + "\n" +
                "Nom: " + selected.FullName + "\n\n" +
                "Description: " + selected.Description + "\n\n" +
                selected.Role + " | " + selected.ChampionClass + " | " + selected.Element + "\n" +
                "Tier: " + selected.TierStars + "★\n" +
                "HP: " + selected.BaseHp + "\n" +
                "ATK: " + selected.BaseAttack + "\n" +
                "Cost: " + selected.UnlockCost + "\n" +
                "State: " + (unlocked ? "Unlocked" : "Locked");

            _splashImage.sprite = selected.SplashSprite;
            _splashImage.color = selected.SplashSprite != null ? Color.white : new Color(0.12f, 0.12f, 0.12f, 1f);
            _splashFallbackText.text = selected.SplashSprite == null ? "No splash art" : string.Empty;

            _unlockButton.gameObject.SetActive(!unlocked);
            if (!unlocked)
            {
                _unlockButton.interactable = affordable;
                _unlockButtonText.text = "Unlock (" + selected.UnlockCost + ")";
            }

            _feedbackText.text = _feedbackMessage;
            RefreshCarouselVisuals();
        }

        private void RefreshCarouselVisuals()
        {
            foreach (var item in _itemViews)
            {
                var champion = _visibleChampions.FirstOrDefault(c => c != null && c.Id == item.championId);
                if (champion == null)
                {
                    continue;
                }

                var isSelected = champion.Id == _selectedChampionId;
                var unlocked = _manager.IsChampionUnlocked(champion.Id);
                var affordable = _manager.GetPlayerCoins() >= champion.UnlockCost;

                item.label.text = champion.DisplayName + "  " + champion.TierStars + "★";
                item.status.color = GetElementTagColor(champion.Element);

                if (isSelected)
                {
                    item.background.color = _selectedColor;
                    item.status.text = BuildChampionTagLine(champion) + "  |  Selected";
                }
                else if (unlocked)
                {
                    item.background.color = _unlockedColor;
                    item.status.text = BuildChampionTagLine(champion) + "  |  Unlocked";
                }
                else if (affordable)
                {
                    item.background.color = _lockedAffordableColor;
                    item.status.text = BuildChampionTagLine(champion) + "  |  Locked " + champion.UnlockCost;
                }
                else
                {
                    item.background.color = _lockedColor;
                    item.status.text = BuildChampionTagLine(champion) + "  |  Locked " + champion.UnlockCost;
                }
            }
        }

        private void OnClickUnlockSelected()
        {
            var selected = GetSelectedChampion();
            if (selected == null)
            {
                return;
            }

            if (_manager.TryUnlockChampion(selected.Id, out var reason))
            {
                _feedbackMessage = reason;
            }
            else
            {
                _feedbackMessage = reason;
            }

            RefreshAndRender();
        }

        private ChampionDefinitionAsset GetSelectedChampion()
        {
            return _visibleChampions.FirstOrDefault(c => c != null && c.Id == _selectedChampionId);
        }

        private bool ContainsChampion(string championId)
        {
            return _visibleChampions.Any(c => c != null && c.Id == championId);
        }

        private IEnumerable<ChampionDefinitionAsset> ApplyFilterAndSort(IReadOnlyList<ChampionDefinitionAsset> source)
        {
            IEnumerable<ChampionDefinitionAsset> query = source.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id));
            query = query.Where(c => c.TierStars >= _filter.minTier && c.TierStars <= _filter.maxTier);
            if (_filter.element.HasValue) query = query.Where(c => c.Element == _filter.element.Value);
            if (_filter.championClass.HasValue) query = query.Where(c => c.ChampionClass == _filter.championClass.Value);

            switch (_sortMode)
            {
                case ChampionSortMode.TierAsc:
                    query = query.OrderBy(c => c.TierStars).ThenBy(c => c.DisplayName);
                    break;
                case ChampionSortMode.TierDesc:
                    query = query.OrderByDescending(c => c.TierStars).ThenBy(c => c.DisplayName);
                    break;
                case ChampionSortMode.PackByElement:
                    query = query.OrderBy(c => c.Element).ThenByDescending(c => c.TierStars).ThenBy(c => c.DisplayName);
                    break;
                default:
                    break;
            }

            return query.ToList();
        }

        private void BuildLayout()
        {
            var canvasGo = CreateUIObject("CollectionCanvas", transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2532f, 1170f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var background = CreatePanel("Background", canvasGo.transform, new Color(0.09f, 0.1f, 0.12f, 1f));
            StretchToParent(background.rectTransform);

            var safeAreaGo = CreateUIObject("SafeAreaRoot", canvasGo.transform);
            _safeAreaRoot = safeAreaGo.GetComponent<RectTransform>();
            StretchToParent(_safeAreaRoot);

            var splashRoot = CreatePanel("SplashRoot", safeAreaGo.transform, new Color(0.07f, 0.08f, 0.1f, 1f));
            splashRoot.rectTransform.anchorMin = new Vector2(0f, 0.29f);
            splashRoot.rectTransform.anchorMax = new Vector2(1f, 1f);
            splashRoot.rectTransform.offsetMin = Vector2.zero;
            splashRoot.rectTransform.offsetMax = Vector2.zero;
            _splashImage = splashRoot;
            _splashImage.preserveAspect = true;
            _splashFallbackText = CreateText("SplashFallbackText", splashRoot.transform, 22, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_splashFallbackText.rectTransform);

            var modal = CreatePanel("InfoModal", splashRoot.transform, new Color(0f, 0f, 0f, 0.58f));
            modal.rectTransform.anchorMin = new Vector2(0.56f, 0.07f);
            modal.rectTransform.anchorMax = new Vector2(0.97f, 0.93f);
            modal.rectTransform.offsetMin = Vector2.zero;
            modal.rectTransform.offsetMax = Vector2.zero;

            var bottomPanel = CreatePanel("BottomPanel", safeAreaGo.transform, new Color(0.12f, 0.13f, 0.16f, 0.98f));
            bottomPanel.rectTransform.anchorMin = new Vector2(0f, 0f);
            bottomPanel.rectTransform.anchorMax = new Vector2(1f, 0.21f);
            bottomPanel.rectTransform.offsetMin = Vector2.zero;
            bottomPanel.rectTransform.offsetMax = Vector2.zero;

            var controlsPanel = CreatePanel("CollectionControls", safeAreaGo.transform, new Color(0.1f, 0.11f, 0.14f, 0.96f));
            controlsPanel.rectTransform.anchorMin = new Vector2(0f, 0.21f);
            controlsPanel.rectTransform.anchorMax = new Vector2(1f, 0.29f);
            controlsPanel.rectTransform.offsetMin = Vector2.zero;
            controlsPanel.rectTransform.offsetMax = Vector2.zero;

            const float controlPaddingX = 16f;
            const float controlSpacing = 8f;
            const float controlButtonWidth = 120f;
            const float controlButtonHeight = 42f;

            _coinsText = CreateText("CoinsText", modal.transform, 17, TextAnchor.UpperLeft, Color.white);
            _coinsText.rectTransform.anchorMin = new Vector2(0.04f, 0.83f);
            _coinsText.rectTransform.anchorMax = new Vector2(0.5f, 0.97f);
            _coinsText.rectTransform.offsetMin = Vector2.zero;
            _coinsText.rectTransform.offsetMax = Vector2.zero;

            _filterSortPlaceholderText = CreateText("FilterSortPlaceholder", modal.transform, 12, TextAnchor.UpperRight, Color.white);
            _filterSortPlaceholderText.rectTransform.anchorMin = new Vector2(0.5f, 0.83f);
            _filterSortPlaceholderText.rectTransform.anchorMax = new Vector2(0.96f, 0.97f);
            _filterSortPlaceholderText.rectTransform.offsetMin = Vector2.zero;
            _filterSortPlaceholderText.rectTransform.offsetMax = Vector2.zero;

            var sortButtonImage = CreatePanel("SortButton", controlsPanel.transform, _controlButtonIdleColor);
            sortButtonImage.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            sortButtonImage.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            sortButtonImage.rectTransform.pivot = new Vector2(0f, 0.5f);
            sortButtonImage.rectTransform.anchoredPosition = new Vector2(controlPaddingX, 0f);
            sortButtonImage.rectTransform.sizeDelta = new Vector2(controlButtonWidth, controlButtonHeight);
            _sortButtonImage = sortButtonImage;
            _sortButton = sortButtonImage.gameObject.AddComponent<Button>();
            _sortButton.targetGraphic = sortButtonImage;
            _sortButton.onClick.AddListener(ToggleSortPopup);
            _sortButtonText = CreateText("SortButtonText", sortButtonImage.transform, 13, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_sortButtonText.rectTransform);

            var filterButtonImage = CreatePanel("FilterButton", controlsPanel.transform, _controlButtonIdleColor);
            filterButtonImage.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            filterButtonImage.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            filterButtonImage.rectTransform.pivot = new Vector2(0f, 0.5f);
            filterButtonImage.rectTransform.anchoredPosition = new Vector2(controlPaddingX + controlButtonWidth + controlSpacing, 0f);
            filterButtonImage.rectTransform.sizeDelta = new Vector2(controlButtonWidth, controlButtonHeight);
            _filterButtonImage = filterButtonImage;
            _filterButton = filterButtonImage.gameObject.AddComponent<Button>();
            _filterButton.targetGraphic = filterButtonImage;
            _filterButton.onClick.AddListener(ToggleFilterPopup);
            _filterButtonText = CreateText("FilterButtonText", filterButtonImage.transform, 13, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_filterButtonText.rectTransform);

            BuildSortPopup(controlsPanel.transform);
            BuildFilterPopup(controlsPanel.transform);

            _detailText = CreateText("DetailText", modal.transform, 15, TextAnchor.UpperLeft, Color.white);
            _detailText.rectTransform.anchorMin = new Vector2(0.05f, 0.18f);
            _detailText.rectTransform.anchorMax = new Vector2(0.95f, 0.48f);
            _detailText.rectTransform.offsetMin = Vector2.zero;
            _detailText.rectTransform.offsetMax = Vector2.zero;
            _detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailText.verticalOverflow = VerticalWrapMode.Overflow;

            var unlockButtonImage = CreatePanel("UnlockButton", modal.transform, new Color(0.27f, 0.53f, 0.29f, 1f));
            unlockButtonImage.rectTransform.anchorMin = new Vector2(0.04f, 0.06f);
            unlockButtonImage.rectTransform.anchorMax = new Vector2(0.45f, 0.18f);
            unlockButtonImage.rectTransform.offsetMin = Vector2.zero;
            unlockButtonImage.rectTransform.offsetMax = Vector2.zero;
            _unlockButton = unlockButtonImage.gameObject.AddComponent<Button>();
            _unlockButton.targetGraphic = unlockButtonImage;
            _unlockButton.onClick.AddListener(OnClickUnlockSelected);
            _unlockButtonText = CreateText("UnlockButtonText", unlockButtonImage.transform, 14, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_unlockButtonText.rectTransform);

            _feedbackText = CreateText("FeedbackText", modal.transform, 13, TextAnchor.MiddleLeft, new Color(1f, 0.95f, 0.75f, 1f));
            _feedbackText.rectTransform.anchorMin = new Vector2(0.5f, 0.06f);
            _feedbackText.rectTransform.anchorMax = new Vector2(0.96f, 0.18f);
            _feedbackText.rectTransform.offsetMin = Vector2.zero;
            _feedbackText.rectTransform.offsetMax = Vector2.zero;

            BuildCarousel(bottomPanel.transform);
        }

        private void BuildCarousel(Transform parent)
        {
            var title = CreateText("CarouselTitle", parent, 17, TextAnchor.MiddleLeft, Color.white);
            title.text = "Champion Gallery";
            title.rectTransform.anchorMin = new Vector2(0.03f, 0.73f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 0.95f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;

            var scrollRoot = CreateUIObject("CarouselScroll", parent);
            var scrollRect = scrollRoot.AddComponent<ScrollRect>();
            var scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.03f, 0.08f);
            scrollRectTransform.anchorMax = new Vector2(0.97f, 0.71f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            var viewport = CreatePanel("Viewport", scrollRoot.transform, new Color(0f, 0f, 0f, 0.15f));
            StretchToParent(viewport.rectTransform);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = CreateUIObject("CollectionCarouselContent", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);

            var layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.padding = new RectOffset(10, 10, 8, 8);

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = viewport.rectTransform;
            scrollRect.content = contentRect;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        private void BuildSortPopup(Transform parent)
        {
            const float controlPaddingX = 16f;
            const float sortPopupWidth = 180f;
            const float sortPopupHeight = 156f;
            var popupImage = CreatePanel("SortPopup", parent, new Color(0.07f, 0.07f, 0.1f, 0.96f));
            var popupRect = popupImage.rectTransform;
            popupRect.anchorMin = new Vector2(0f, 0f);
            popupRect.anchorMax = new Vector2(0f, 0f);
            popupRect.pivot = new Vector2(0f, 1f);
            popupRect.anchoredPosition = new Vector2(controlPaddingX, -4f);
            popupRect.sizeDelta = new Vector2(sortPopupWidth, sortPopupHeight);
            _sortPopup = popupImage.gameObject;
            _sortPopup.SetActive(false);

            var sortLayout = _sortPopup.AddComponent<VerticalLayoutGroup>();
            sortLayout.spacing = 8f;
            sortLayout.padding = new RectOffset(16, 16, 8, 8);
            sortLayout.childControlHeight = true;
            sortLayout.childControlWidth = true;
            sortLayout.childForceExpandHeight = false;
            sortLayout.childForceExpandWidth = true;

            CreatePopupOption(_sortPopup.transform, "Tier ↑", () => SelectSortMode(ChampionSortMode.TierAsc));
            CreatePopupOption(_sortPopup.transform, "Tier ↓", () => SelectSortMode(ChampionSortMode.TierDesc));
            CreatePopupOption(_sortPopup.transform, "Element", () => SelectSortMode(ChampionSortMode.PackByElement));
        }

        private void BuildFilterPopup(Transform parent)
        {
            const float controlPaddingX = 16f;
            const float controlSpacing = 8f;
            const float controlButtonWidth = 120f;
            const float sortPopupWidth = 180f;
            const float filterPopupWidth = 220f;
            const float filterPopupHeight = 188f;
            var popupImage = CreatePanel("FilterPopup", parent, new Color(0.07f, 0.07f, 0.1f, 0.96f));
            var popupRect = popupImage.rectTransform;
            popupRect.anchorMin = new Vector2(0f, 0f);
            popupRect.anchorMax = new Vector2(0f, 0f);
            popupRect.pivot = new Vector2(0f, 1f);
            popupRect.anchoredPosition = new Vector2(controlPaddingX + controlButtonWidth + controlSpacing, -4f);
            popupRect.sizeDelta = new Vector2(filterPopupWidth, filterPopupHeight);
            _filterPopup = popupImage.gameObject;
            _filterPopup.SetActive(false);

            var filterLayout = _filterPopup.AddComponent<VerticalLayoutGroup>();
            filterLayout.spacing = 8f;
            filterLayout.padding = new RectOffset(16, 16, 8, 8);
            filterLayout.childControlHeight = true;
            filterLayout.childControlWidth = true;
            filterLayout.childForceExpandHeight = false;
            filterLayout.childForceExpandWidth = true;

            var tierBtn = CreatePopupOption(_filterPopup.transform, string.Empty, CycleTierFilter);
            _tierFilterStatusText = tierBtn.GetComponentInChildren<Text>();
            var elementBtn = CreatePopupOption(_filterPopup.transform, string.Empty, CycleElementFilter);
            _elementFilterStatusText = elementBtn.GetComponentInChildren<Text>();
            var classBtn = CreatePopupOption(_filterPopup.transform, string.Empty, CycleClassFilter);
            _classFilterStatusText = classBtn.GetComponentInChildren<Text>();
            CreatePopupOption(_filterPopup.transform, "Reset", ResetFilters);
        }

        private Button CreatePopupOption(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var optionImage = CreatePanel("Option", parent, new Color(0.2f, 0.2f, 0.24f, 1f));
            var optionLayout = optionImage.gameObject.AddComponent<LayoutElement>();
            optionLayout.minHeight = 30f;
            optionLayout.preferredHeight = 34f;
            var button = optionImage.gameObject.AddComponent<Button>();
            button.targetGraphic = optionImage;
            button.onClick.AddListener(onClick);
            var text = CreateText("OptionText", optionImage.transform, 13, TextAnchor.MiddleCenter, Color.white);
            text.text = label;
            StretchToParent(text.rectTransform);
            return button;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreatePanel(string name, Transform parent, Color color)
        {
            var go = CreateUIObject(name, parent);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment, Color? color = null)
        {
            var go = CreateUIObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = color ?? Color.white;
            return text;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemGo.AddComponent<StandaloneInputModule>();
#endif
        }

        private void ToggleSortPopup()
        {
            var shouldShow = _sortPopup != null && !_sortPopup.activeSelf;
            if (_sortPopup != null) _sortPopup.SetActive(shouldShow);
            if (_filterPopup != null) _filterPopup.SetActive(false);
            UpdateControlVisualState();
        }

        private void ToggleFilterPopup()
        {
            var shouldShow = _filterPopup != null && !_filterPopup.activeSelf;
            if (_filterPopup != null) _filterPopup.SetActive(shouldShow);
            if (_sortPopup != null) _sortPopup.SetActive(false);
            UpdateControlVisualState();
        }

        private void SelectSortMode(ChampionSortMode mode)
        {
            _sortMode = mode;
            if (_sortPopup != null) _sortPopup.SetActive(false);
            RefreshAndRender();
        }

        private void ResetFilters()
        {
            _filter.minTier = 3;
            _filter.maxTier = 6;
            _filter.element = null;
            _filter.championClass = null;
            RefreshAndRender();
        }

        private void CycleTierFilter()
        {
            if (_filter.minTier == 3 && _filter.maxTier == 6)
            {
                _filter.minTier = 3;
                _filter.maxTier = 3;
            }
            else if (_filter.minTier == 3 && _filter.maxTier == 3)
            {
                _filter.minTier = 4;
                _filter.maxTier = 4;
            }
            else if (_filter.minTier == 4 && _filter.maxTier == 4)
            {
                _filter.minTier = 5;
                _filter.maxTier = 5;
            }
            else if (_filter.minTier == 5 && _filter.maxTier == 5)
            {
                _filter.minTier = 6;
                _filter.maxTier = 6;
            }
            else
            {
                _filter.minTier = 3;
                _filter.maxTier = 6;
            }

            RefreshAndRender();
        }

        private void CycleElementFilter()
        {
            if (!_filter.element.HasValue) _filter.element = ElementType.Fire;
            else if (_filter.element.Value == ElementType.Fire) _filter.element = ElementType.Water;
            else if (_filter.element.Value == ElementType.Water) _filter.element = ElementType.Earth;
            else if (_filter.element.Value == ElementType.Earth) _filter.element = ElementType.Air;
            else _filter.element = null;

            RefreshAndRender();
        }

        private void CycleClassFilter()
        {
            if (!_filter.championClass.HasValue) _filter.championClass = ChampionClassType.Vanguard;
            else if (_filter.championClass.Value == ChampionClassType.Vanguard) _filter.championClass = ChampionClassType.Striker;
            else if (_filter.championClass.Value == ChampionClassType.Striker) _filter.championClass = ChampionClassType.Controller;
            else if (_filter.championClass.Value == ChampionClassType.Controller) _filter.championClass = ChampionClassType.Support;
            else _filter.championClass = null;

            RefreshAndRender();
        }

        private static string ToSortLabel(ChampionSortMode mode)
        {
            switch (mode)
            {
                case ChampionSortMode.TierAsc: return "Tier ↑";
                case ChampionSortMode.TierDesc: return "Tier ↓";
                case ChampionSortMode.PackByElement: return "Element";
                default: return "Catalog";
            }
        }

        private static string ToElementLabel(ElementType? element)
        {
            return element.HasValue ? element.Value.ToString() : "Tous";
        }

        private static string ToClassLabel(ChampionClassType? championClass)
        {
            return championClass.HasValue ? championClass.Value.ToString() : "Tous";
        }

        private string BuildFilterSummaryLabel()
        {
            var tierPart = _filter.minTier + "-" + _filter.maxTier + "★";
            return "Tri " + ToSortLabel(_sortMode) + " | T " + tierPart + " | E " + ToElementLabel(_filter.element) + " | Type " + ToClassLabel(_filter.championClass);
        }

        private static string BuildChampionTagLine(ChampionDefinitionAsset champion)
        {
            return champion.Element + " • " + champion.ChampionClass + " • " + champion.TierStars + "★";
        }

        private static Color GetElementTagColor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:
                    return new Color(1f, 0.52f, 0.32f, 1f);
                case ElementType.Water:
                    return new Color(0.43f, 0.74f, 1f, 1f);
                case ElementType.Earth:
                    return new Color(0.58f, 0.86f, 0.45f, 1f);
                case ElementType.Air:
                    return new Color(0.83f, 0.83f, 1f, 1f);
                default:
                    return Color.white;
            }
        }

        private int CountActiveFilters()
        {
            var count = 0;
            if (_filter.minTier != 3 || _filter.maxTier != 6) count++;
            if (_filter.element.HasValue) count++;
            if (_filter.championClass.HasValue) count++;
            return count;
        }

        private void UpdateControlVisualState()
        {
            var isSortPopupOpen = _sortPopup != null && _sortPopup.activeSelf;
            var isFilterPopupOpen = _filterPopup != null && _filterPopup.activeSelf;
            var hasActiveFilter = _filter.minTier != 3 || _filter.maxTier != 6 || _filter.element.HasValue || _filter.championClass.HasValue;

            if (_sortButtonImage != null)
            {
                _sortButtonImage.color = isSortPopupOpen ? _controlButtonActiveColor : _controlButtonIdleColor;
            }

            if (_filterButtonImage != null)
            {
                _filterButtonImage.color = (isFilterPopupOpen || hasActiveFilter) ? _controlButtonActiveColor : _controlButtonIdleColor;
            }
        }

        private void HandlePopupOutsideClick()
        {
            if ((_sortPopup == null || !_sortPopup.activeSelf) && (_filterPopup == null || !_filterPopup.activeSelf))
            {
                return;
            }

            if (!TryGetPointerDownPosition(out var pointerPosition))
            {
                return;
            }

            if (IsPointerInsideInteractiveZone(pointerPosition))
            {
                return;
            }

            if (_sortPopup != null) _sortPopup.SetActive(false);
            if (_filterPopup != null) _filterPopup.SetActive(false);
            UpdateControlVisualState();
        }

        private bool IsPointerInsideInteractiveZone(Vector2 pointerPosition)
        {
            return IsPointerInside(_sortPopup, pointerPosition)
                || IsPointerInside(_filterPopup, pointerPosition)
                || IsPointerInside(_sortButton != null ? _sortButton.gameObject : null, pointerPosition)
                || IsPointerInside(_filterButton != null ? _filterButton.gameObject : null, pointerPosition);
        }

        private static bool IsPointerInside(GameObject target, Vector2 pointerPosition)
        {
            if (target == null)
            {
                return false;
            }

            var rect = target.GetComponent<RectTransform>();
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, pointerPosition, null);
        }

        private static bool TryGetPointerDownPosition(out Vector2 pointerPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    pointerPosition = touch.position.ReadValue();
                    return true;
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                pointerPosition = Mouse.current.position.ReadValue();
                return true;
            }
#else
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    pointerPosition = touch.position;
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                pointerPosition = Input.mousePosition;
                return true;
            }
#endif

            pointerPosition = Vector2.zero;
            return false;
        }

        private void ApplySafeArea(bool force)
        {
            if (_safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            if (!force && safeArea == _lastSafeArea)
            {
                return;
            }

            _lastSafeArea = safeArea;
            var min = safeArea.position;
            var max = safeArea.position + safeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _safeAreaRoot.anchorMin = min;
            _safeAreaRoot.anchorMax = max;
            _safeAreaRoot.offsetMin = Vector2.zero;
            _safeAreaRoot.offsetMax = Vector2.zero;
        }
    }
}
