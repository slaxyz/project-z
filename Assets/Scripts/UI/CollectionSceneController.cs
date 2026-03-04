using System.Collections.Generic;
using System.Linq;
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
        NameAsc = 1,
        CostAsc = 2
    }

    public struct ChampionFilter
    {
        public string role;
        public bool unlockedOnly;
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
        private Image _splashImage;
        private Text _splashFallbackText;
        private Text _feedbackText;
        private Button _unlockButton;
        private Text _unlockButtonText;

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
            EnsureEventSystem();
            BuildLayout();
            RefreshAndRender();
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
            rect.sizeDelta = new Vector2(210f, 110f);

            var image = root.AddComponent<Image>();
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText("Label", root.transform, 17, TextAnchor.UpperLeft);
            label.rectTransform.anchorMin = new Vector2(0.05f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0.95f, 0.95f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            var status = CreateText("Status", root.transform, 14, TextAnchor.LowerLeft);
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
            _filterSortPlaceholderText.text = "Filters/Sort: coming soon";

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
                selected.Role + "\n\n" +
                "Lore: " + selected.ShortLore + "\n\n" +
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

                item.label.text = champion.DisplayName + "\n" + champion.Role;

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

            if (!string.IsNullOrWhiteSpace(_filter.role))
            {
                query = query.Where(c => c.Role == _filter.role);
            }

            if (_filter.unlockedOnly)
            {
                query = query.Where(c => _manager.IsChampionUnlocked(c.Id));
            }

            switch (_sortMode)
            {
                case ChampionSortMode.NameAsc:
                    query = query.OrderBy(c => c.DisplayName);
                    break;
                case ChampionSortMode.CostAsc:
                    query = query.OrderBy(c => c.UnlockCost);
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
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var background = CreatePanel("Background", canvasGo.transform, new Color(0.09f, 0.1f, 0.12f, 1f));
            StretchToParent(background.rectTransform);

            var topPanel = CreatePanel("TopPanel", canvasGo.transform, new Color(0.15f, 0.17f, 0.2f, 0.96f));
            topPanel.rectTransform.anchorMin = new Vector2(0.03f, 0.34f);
            topPanel.rectTransform.anchorMax = new Vector2(0.97f, 0.97f);
            topPanel.rectTransform.offsetMin = Vector2.zero;
            topPanel.rectTransform.offsetMax = Vector2.zero;

            var bottomPanel = CreatePanel("BottomPanel", canvasGo.transform, new Color(0.12f, 0.13f, 0.16f, 0.98f));
            bottomPanel.rectTransform.anchorMin = new Vector2(0f, 0f);
            bottomPanel.rectTransform.anchorMax = new Vector2(1f, 0.31f);
            bottomPanel.rectTransform.offsetMin = Vector2.zero;
            bottomPanel.rectTransform.offsetMax = Vector2.zero;

            _coinsText = CreateText("CoinsText", topPanel.transform, 24, TextAnchor.UpperLeft, Color.white);
            _coinsText.rectTransform.anchorMin = new Vector2(0.03f, 0.9f);
            _coinsText.rectTransform.anchorMax = new Vector2(0.4f, 0.98f);
            _coinsText.rectTransform.offsetMin = Vector2.zero;
            _coinsText.rectTransform.offsetMax = Vector2.zero;

            _filterSortPlaceholderText = CreateText("FilterSortPlaceholder", topPanel.transform, 18, TextAnchor.UpperRight, Color.white);
            _filterSortPlaceholderText.rectTransform.anchorMin = new Vector2(0.55f, 0.9f);
            _filterSortPlaceholderText.rectTransform.anchorMax = new Vector2(0.97f, 0.98f);
            _filterSortPlaceholderText.rectTransform.offsetMin = Vector2.zero;
            _filterSortPlaceholderText.rectTransform.offsetMax = Vector2.zero;

            var splashRoot = CreatePanel("SplashRoot", topPanel.transform, new Color(0.07f, 0.08f, 0.1f, 1f));
            splashRoot.rectTransform.anchorMin = new Vector2(0.03f, 0.08f);
            splashRoot.rectTransform.anchorMax = new Vector2(0.48f, 0.86f);
            splashRoot.rectTransform.offsetMin = Vector2.zero;
            splashRoot.rectTransform.offsetMax = Vector2.zero;
            _splashImage = splashRoot;
            _splashImage.preserveAspect = true;
            _splashFallbackText = CreateText("SplashFallbackText", splashRoot.transform, 22, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_splashFallbackText.rectTransform);

            _detailText = CreateText("DetailText", topPanel.transform, 20, TextAnchor.UpperLeft, Color.white);
            _detailText.rectTransform.anchorMin = new Vector2(0.52f, 0.24f);
            _detailText.rectTransform.anchorMax = new Vector2(0.97f, 0.86f);
            _detailText.rectTransform.offsetMin = Vector2.zero;
            _detailText.rectTransform.offsetMax = Vector2.zero;

            var unlockButtonImage = CreatePanel("UnlockButton", topPanel.transform, new Color(0.27f, 0.53f, 0.29f, 1f));
            unlockButtonImage.rectTransform.anchorMin = new Vector2(0.52f, 0.08f);
            unlockButtonImage.rectTransform.anchorMax = new Vector2(0.72f, 0.18f);
            unlockButtonImage.rectTransform.offsetMin = Vector2.zero;
            unlockButtonImage.rectTransform.offsetMax = Vector2.zero;
            _unlockButton = unlockButtonImage.gameObject.AddComponent<Button>();
            _unlockButton.targetGraphic = unlockButtonImage;
            _unlockButton.onClick.AddListener(OnClickUnlockSelected);
            _unlockButtonText = CreateText("UnlockButtonText", unlockButtonImage.transform, 18, TextAnchor.MiddleCenter, Color.white);
            StretchToParent(_unlockButtonText.rectTransform);

            _feedbackText = CreateText("FeedbackText", topPanel.transform, 17, TextAnchor.MiddleLeft, new Color(1f, 0.95f, 0.75f, 1f));
            _feedbackText.rectTransform.anchorMin = new Vector2(0.74f, 0.08f);
            _feedbackText.rectTransform.anchorMax = new Vector2(0.97f, 0.18f);
            _feedbackText.rectTransform.offsetMin = Vector2.zero;
            _feedbackText.rectTransform.offsetMax = Vector2.zero;

            BuildCarousel(bottomPanel.transform);
        }

        private void BuildCarousel(Transform parent)
        {
            var title = CreateText("CarouselTitle", parent, 24, TextAnchor.MiddleLeft, Color.white);
            title.text = "Champion Gallery";
            title.rectTransform.anchorMin = new Vector2(0.03f, 0.78f);
            title.rectTransform.anchorMax = new Vector2(0.4f, 0.98f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;

            var scrollRoot = CreateUIObject("CarouselScroll", parent);
            var scrollRect = scrollRoot.AddComponent<ScrollRect>();
            var scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.03f, 0.08f);
            scrollRectTransform.anchorMax = new Vector2(0.97f, 0.74f);
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
            layout.padding = new RectOffset(6, 6, 8, 8);

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
    }
}
