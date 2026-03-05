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

        private ChampionSortMode _sortMode = ChampionSortMode.CatalogOrder;
        private ChampionFilter _filter;
        private string _selectedChampionId = string.Empty;
        private string _feedbackMessage = string.Empty;

        private Text _coinsText;
        private Text _detailText;
        private Text _filterSortPlaceholderText;
        private Button _sortButton;
        private Text _sortButtonText;
        private Button _tierFilterButton;
        private Text _tierFilterButtonText;
        private Button _elementFilterButton;
        private Text _elementFilterButtonText;
        private Button _classFilterButton;
        private Text _classFilterButtonText;
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
            _sortButtonText.text = "Sort: " + ToSortLabel(_sortMode);
            _tierFilterButtonText.text = "Tier: " + _filter.minTier + "-" + _filter.maxTier + "★";
            _elementFilterButtonText.text = "Element: " + ToElementLabel(_filter.element);
            _classFilterButtonText.text = "Type: " + ToClassLabel(_filter.championClass);

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
                selected.Role + " | " + selected.ChampionClass + " | " + selected.Element + "\n\n" +
                "Lore: " + selected.ShortLore + "\n\n" +
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

                item.label.text = champion.DisplayName + "  " + champion.TierStars + "★\n" + champion.Element + " • " + champion.ChampionClass;

                if (isSelected)
                {
                    item.background.color = _selectedColor;
                    item.status.text = "Selected";
                }
                else if (unlocked)
                {
                    item.background.color = _unlockedColor;
                    item.status.text = "Unlocked";
                }
                else if (affordable)
                {
                    item.background.color = _lockedAffordableColor;
                    item.status.text = "Locked - " + champion.UnlockCost;
                }
                else
                {
                    item.background.color = _lockedColor;
                    item.status.text = "Locked - " + champion.UnlockCost;
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
            splashRoot.rectTransform.anchorMin = new Vector2(0f, 0.27f);
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
            bottomPanel.rectTransform.anchorMax = new Vector2(1f, 0.27f);
            bottomPanel.rectTransform.offsetMin = Vector2.zero;
            bottomPanel.rectTransform.offsetMax = Vector2.zero;

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

            var sortButtonImage = CreatePanel("SortButton", modal.transform, new Color(0.22f, 0.22f, 0.26f, 0.95f));
            sortButtonImage.rectTransform.anchorMin = new Vector2(0.04f, 0.74f);
            sortButtonImage.rectTransform.anchorMax = new Vector2(0.96f, 0.82f);
            _sortButton = sortButtonImage.gameObject.AddComponent<Button>();
            _sortButton.targetGraphic = sortButtonImage;
            _sortButton.onClick.AddListener(CycleSortMode);
            _sortButtonText = CreateText("SortButtonText", sortButtonImage.transform, 13, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_sortButtonText.rectTransform);

            var tierFilterButtonImage = CreatePanel("TierFilterButton", modal.transform, new Color(0.2f, 0.2f, 0.24f, 0.95f));
            tierFilterButtonImage.rectTransform.anchorMin = new Vector2(0.04f, 0.66f);
            tierFilterButtonImage.rectTransform.anchorMax = new Vector2(0.96f, 0.74f);
            _tierFilterButton = tierFilterButtonImage.gameObject.AddComponent<Button>();
            _tierFilterButton.targetGraphic = tierFilterButtonImage;
            _tierFilterButton.onClick.AddListener(CycleTierFilter);
            _tierFilterButtonText = CreateText("TierFilterButtonText", tierFilterButtonImage.transform, 13, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_tierFilterButtonText.rectTransform);

            var elementFilterButtonImage = CreatePanel("ElementFilterButton", modal.transform, new Color(0.2f, 0.2f, 0.24f, 0.95f));
            elementFilterButtonImage.rectTransform.anchorMin = new Vector2(0.04f, 0.58f);
            elementFilterButtonImage.rectTransform.anchorMax = new Vector2(0.96f, 0.66f);
            _elementFilterButton = elementFilterButtonImage.gameObject.AddComponent<Button>();
            _elementFilterButton.targetGraphic = elementFilterButtonImage;
            _elementFilterButton.onClick.AddListener(CycleElementFilter);
            _elementFilterButtonText = CreateText("ElementFilterButtonText", elementFilterButtonImage.transform, 13, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_elementFilterButtonText.rectTransform);

            var classFilterButtonImage = CreatePanel("ClassFilterButton", modal.transform, new Color(0.2f, 0.2f, 0.24f, 0.95f));
            classFilterButtonImage.rectTransform.anchorMin = new Vector2(0.04f, 0.5f);
            classFilterButtonImage.rectTransform.anchorMax = new Vector2(0.96f, 0.58f);
            _classFilterButton = classFilterButtonImage.gameObject.AddComponent<Button>();
            _classFilterButton.targetGraphic = classFilterButtonImage;
            _classFilterButton.onClick.AddListener(CycleClassFilter);
            _classFilterButtonText = CreateText("ClassFilterButtonText", classFilterButtonImage.transform, 13, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_classFilterButtonText.rectTransform);

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

        private void CycleSortMode()
        {
            _sortMode = (ChampionSortMode)(((int)_sortMode + 1) % 4);
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
                case ChampionSortMode.TierAsc: return "Tier Asc";
                case ChampionSortMode.TierDesc: return "Tier Desc";
                case ChampionSortMode.PackByElement: return "Pack Element";
                default: return "Catalog";
            }
        }

        private static string ToElementLabel(ElementType? element)
        {
            return element.HasValue ? element.Value.ToString() : "All";
        }

        private static string ToClassLabel(ChampionClassType? championClass)
        {
            return championClass.HasValue ? championClass.Value.ToString() : "All";
        }

        private string BuildFilterSummaryLabel()
        {
            var tierPart = _filter.minTier + "-" + _filter.maxTier + "★";
            return "Tier " + tierPart + " | Elem " + ToElementLabel(_filter.element) + " | Type " + ToClassLabel(_filter.championClass);
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
