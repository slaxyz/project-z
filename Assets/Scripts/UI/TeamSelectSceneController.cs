using System.Collections.Generic;
using System.Linq;
using System;
using ProjectZ.Core;
using ProjectZ.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace ProjectZ.UI
{
    public class TeamSelectSceneController : MonoBehaviour
    {
        private sealed class ChampionGridItemView
        {
            public string championId;
            public Image background;
            public Image portraitBackground;
            public Image portrait;
            public Text nameText;
            public Button button;
            public bool hasSplash;
        }

        private sealed class SlotView
        {
            public Image background;
            public Image portraitBackground;
            public Image portrait;
            public Text titleText;
            public Text nameText;
            public Button button;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!IsSupportedScene(activeScene.name))
            {
                return;
            }

            if (FindFirstObjectByType<TeamSelectSceneController>() != null)
            {
                return;
            }

            var go = new GameObject("TeamSelectSceneController");
            go.AddComponent<TeamSelectSceneController>();
        }

        private readonly List<ChampionGridItemView> _gridItems = new List<ChampionGridItemView>();
        private readonly SlotView[] _slotViews = new SlotView[3];
        private readonly string[] _slotChampionIds = new string[3];
        private readonly List<HeroSelectorView> _manualHeroViews = new List<HeroSelectorView>();
        private readonly TeamCardView[] _manualTeamCardViews = new TeamCardView[3];
        private readonly Color _slotIdleColor = new Color(0.14f, 0.16f, 0.2f, 0.96f);
        private readonly Color _slotFilledColor = new Color(0.18f, 0.22f, 0.3f, 0.98f);
        private readonly Color _slotActiveColor = new Color(0.27f, 0.37f, 0.57f, 1f);
        private readonly Color _tileIdleColor = new Color(0.18f, 0.2f, 0.24f, 0.98f);
        private readonly Color _tileSelectedColor = new Color(0.34f, 0.45f, 0.66f, 1f);
        private readonly Color _tileLockedColor = new Color(0.24f, 0.24f, 0.24f, 0.92f);
        private readonly Color _playDisabledColor = new Color(0.35f, 0.35f, 0.35f, 0.9f);
        private readonly Color _playEnabledColor = new Color(0.64f, 0.89f, 0.22f, 1f);

        private readonly List<ChampionDefinitionAsset> _catalog = new List<ChampionDefinitionAsset>();

        private GameFlowManager _manager;
        private Font _font;
        private RectTransform _safeAreaRoot;
        private Rect _lastSafeArea;
        private int _activeSlotIndex;
        private bool _useManualUi;
        private Transform _manualGridContent;
        private Transform _manualTeamCardsRow;
        private CollectionHeroCarouselController _manualCarouselSource;

        private Image _playButtonImage;
        private Button _playButton;
        private Text _playButtonText;
        private Text _helperText;

        private void Awake()
        {
            if (!IsSupportedScene(SceneManager.GetActiveScene().name))
            {
                return;
            }

            var carousel = FindFirstObjectByType<CollectionHeroCarouselController>();
            if (carousel != null && FindDeepChildByName("TeamCardsRow") != null)
            {
                carousel.enabled = false;
                _manualCarouselSource = carousel;
            }
        }

        private void Start()
        {
            _manager = GameFlowManager.Instance;
            if (_manager == null)
            {
                Debug.LogWarning("TeamSelectSceneController: GameFlowManager not found.");
                enabled = false;
                return;
            }

            _catalog.Clear();
            _catalog.AddRange(_manager.GetChampionCatalog().Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id)));

            EnsureEventSystem();

            if (TryBindManualUi())
            {
                InitializeSlotsFromRun();
                RefreshUI();
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                Debug.LogWarning("TeamSelectSceneController: built-in font LegacyRuntime.ttf not found.");
                enabled = false;
                return;
            }

            BuildLayout();
            InitializeSlotsFromRun();
            ApplySafeArea(true);
            RefreshUI();
        }

        private void Update()
        {
            if (_useManualUi)
            {
                return;
            }

            ApplySafeArea(false);
        }

        private static bool IsSupportedScene(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName) &&
                   sceneName.StartsWith(GameScenes.TeamSelect, StringComparison.OrdinalIgnoreCase);
        }

        private void InitializeSlotsFromRun()
        {
            for (var i = 0; i < _slotChampionIds.Length; i++)
            {
                _slotChampionIds[i] = string.Empty;
            }

            var selected = _manager.SelectedChampionIds();
            for (var i = 0; i < selected.Count && i < _slotChampionIds.Length; i++)
            {
                _slotChampionIds[i] = selected[i];
            }

            var count = CountFilledSlots();
            _activeSlotIndex = count >= 3 ? 2 : Mathf.Clamp(count, 0, 2);
            SyncSlotsToRunData();
        }

        private bool TryBindManualUi()
        {
            _manualCarouselSource = FindFirstObjectByType<CollectionHeroCarouselController>();
            _manualGridContent = _manualCarouselSource != null ? _manualCarouselSource.ContentRoot : FindDeepChildByName("Content_Grid");
            var heroSelectorPrefab = _manualCarouselSource != null ? _manualCarouselSource.HeroSelectorPrefab : null;
            _manualTeamCardsRow = FindDeepChildByName("TeamCardsRow");

            if (_manualGridContent == null || heroSelectorPrefab == null || _manualTeamCardsRow == null)
            {
                return false;
            }

            _useManualUi = true;

            if (_manualCarouselSource != null)
            {
                _manualCarouselSource.ClearRuntimeItems();
                _manualCarouselSource.enabled = false;
            }

            BuildManualChampionGrid(heroSelectorPrefab);
            PrepareManualTeamCards();
            return true;
        }

        private void BuildManualChampionGrid(HeroSelectorView heroSelectorPrefab)
        {
            ClearManualHeroGrid();

            foreach (var champion in _catalog)
            {
                var view = Instantiate(heroSelectorPrefab, _manualGridContent);
                view.name = "Hero_" + champion.Id;
                view.SetShowSelectedRootWhenSelected(true);
                view.Clicked += OnManualHeroClicked;
                view.Bind(champion, _manager.IsChampionUnlocked(champion.Id), false);
                _manualHeroViews.Add(view);
            }
        }

        private void ClearManualHeroGrid()
        {
            for (var i = _manualHeroViews.Count - 1; i >= 0; i--)
            {
                var view = _manualHeroViews[i];
                if (view != null)
                {
                    view.Clicked -= OnManualHeroClicked;
                }
            }

            _manualHeroViews.Clear();

            if (_manualGridContent == null)
            {
                return;
            }

            for (var i = _manualGridContent.childCount - 1; i >= 0; i--)
            {
                var child = _manualGridContent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        private void PrepareManualTeamCards()
        {
            var cards = new List<TeamCardView>();
            for (var i = 0; i < _manualTeamCardsRow.childCount; i++)
            {
                var child = _manualTeamCardsRow.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var card = child.GetComponent<TeamCardView>();
                if (card == null)
                {
                    card = child.gameObject.AddComponent<TeamCardView>();
                }

                cards.Add(card);
            }

            if (cards.Count > 0)
            {
                while (cards.Count < _manualTeamCardViews.Length)
                {
                    var clone = Instantiate(cards[0], _manualTeamCardsRow);
                    clone.name = "TeamCard_" + cards.Count;
                    cards.Add(clone);
                }
            }

            for (var i = 0; i < _manualTeamCardViews.Length; i++)
            {
                _manualTeamCardViews[i] = i < cards.Count ? cards[i] : null;
                if (_manualTeamCardViews[i] == null)
                {
                    continue;
                }

                var capturedIndex = i;
                _manualTeamCardViews[i].Button.onClick.RemoveAllListeners();
                _manualTeamCardViews[i].Button.onClick.AddListener(() => OnSlotClicked(capturedIndex));
            }
        }

        private void OnManualHeroClicked(HeroSelectorView view)
        {
            if (view == null)
            {
                return;
            }

            OnChampionClicked(view.ChampionId);
        }

        private void BuildLayout()
        {
            var canvasGo = CreateUIObject("TeamSelectCanvas", transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2532f, 1170f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var background = CreatePanel("Background", canvasGo.transform, new Color(0.12f, 0.65f, 0.72f, 1f));
            StretchToParent(background.rectTransform);

            var safeAreaGo = CreateUIObject("SafeAreaRoot", canvasGo.transform);
            _safeAreaRoot = safeAreaGo.GetComponent<RectTransform>();
            StretchToParent(_safeAreaRoot);

            var contentRoot = CreateUIObject("ContentRoot", safeAreaGo.transform);
            var contentRootRect = contentRoot.GetComponent<RectTransform>();
            StretchToParent(contentRootRect);
            contentRootRect.offsetMin = new Vector2(24f, 24f);
            contentRootRect.offsetMax = new Vector2(-24f, -24f);

            var backButtonImage = CreatePanel("BackButton", contentRoot.transform, new Color(0.08f, 0.1f, 0.15f, 0.97f));
            backButtonImage.rectTransform.anchorMin = new Vector2(0f, 1f);
            backButtonImage.rectTransform.anchorMax = new Vector2(0f, 1f);
            backButtonImage.rectTransform.pivot = new Vector2(0f, 1f);
            backButtonImage.rectTransform.anchoredPosition = Vector2.zero;
            backButtonImage.rectTransform.sizeDelta = new Vector2(76f, 76f);
            var backButton = backButtonImage.gameObject.AddComponent<Button>();
            backButton.targetGraphic = backButtonImage;
            backButton.onClick.AddListener(() => _manager.GoToHome());
            var backText = CreateText("BackText", backButtonImage.transform, 34, TextAnchor.MiddleCenter, Color.white);
            backText.text = "←";
            StretchToParent(backText.rectTransform);

            var mainRow = CreateUIObject("MainRow", contentRoot.transform);
            var mainRowRect = mainRow.GetComponent<RectTransform>();
            mainRowRect.anchorMin = new Vector2(0f, 0f);
            mainRowRect.anchorMax = new Vector2(1f, 1f);
            mainRowRect.offsetMin = new Vector2(0f, 0f);
            mainRowRect.offsetMax = new Vector2(0f, -112f);
            var mainLayout = mainRow.AddComponent<HorizontalLayoutGroup>();
            mainLayout.spacing = 24f;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = true;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childForceExpandHeight = true;
            mainLayout.padding = new RectOffset(0, 0, 24, 0);

            var leftPanel = CreatePanel("LeftPanel", mainRow.transform, new Color(0f, 0f, 0f, 0.12f));
            var leftPanelLayout = leftPanel.gameObject.AddComponent<LayoutElement>();
            leftPanelLayout.minWidth = 320f;
            leftPanelLayout.flexibleWidth = 1f;
            leftPanelLayout.flexibleHeight = 1f;

            BuildChampionGrid(leftPanel.transform);

            var rightPanel = CreateUIObject("RightPanel", mainRow.transform);
            var rightLayoutElement = rightPanel.AddComponent<LayoutElement>();
            rightLayoutElement.flexibleWidth = 3f;
            rightLayoutElement.flexibleHeight = 1f;
            var rightLayout = rightPanel.AddComponent<VerticalLayoutGroup>();
            rightLayout.spacing = 16f;
            rightLayout.padding = new RectOffset(0, 0, 0, 0);
            rightLayout.childControlWidth = true;
            rightLayout.childControlHeight = false;
            rightLayout.childForceExpandWidth = true;
            rightLayout.childForceExpandHeight = false;

            var slotRow = CreateUIObject("SlotRow", rightPanel.transform);
            var slotRowLayoutElement = slotRow.AddComponent<LayoutElement>();
            slotRowLayoutElement.flexibleHeight = 1f;
            slotRowLayoutElement.minHeight = 420f;
            var slotRowLayout = slotRow.AddComponent<HorizontalLayoutGroup>();
            slotRowLayout.spacing = 16f;
            slotRowLayout.padding = new RectOffset(0, 0, 0, 0);
            slotRowLayout.childAlignment = TextAnchor.MiddleCenter;
            slotRowLayout.childControlWidth = true;
            slotRowLayout.childControlHeight = true;
            slotRowLayout.childForceExpandWidth = true;
            slotRowLayout.childForceExpandHeight = true;

            for (var i = 0; i < _slotViews.Length; i++)
            {
                _slotViews[i] = CreateSlot(slotRow.transform, i);
            }

            _helperText = CreateText("HelperText", rightPanel.transform, 18, TextAnchor.MiddleLeft, Color.white);
            _helperText.text = "Choose 3 champions to start.";
            var helperLayout = _helperText.gameObject.AddComponent<LayoutElement>();
            helperLayout.preferredHeight = 36f;

            _playButtonImage = CreatePanel("PlayButton", contentRoot.transform, _playDisabledColor);
            _playButtonImage.rectTransform.anchorMin = new Vector2(1f, 0f);
            _playButtonImage.rectTransform.anchorMax = new Vector2(1f, 0f);
            _playButtonImage.rectTransform.pivot = new Vector2(1f, 0f);
            _playButtonImage.rectTransform.anchoredPosition = Vector2.zero;
            _playButtonImage.rectTransform.sizeDelta = new Vector2(380f, 84f);
            _playButton = _playButtonImage.gameObject.AddComponent<Button>();
            _playButton.targetGraphic = _playButtonImage;
            _playButton.onClick.AddListener(() => _manager.StartRun());
            _playButtonText = CreateText("PlayText", _playButtonImage.transform, 34, TextAnchor.MiddleCenter, Color.black);
            _playButtonText.text = "PLAY";
            StretchToParent(_playButtonText.rectTransform);
        }

        private void BuildChampionGrid(Transform parent)
        {
            var scrollRoot = CreateUIObject("ChampionScroll", parent);
            var scrollRect = scrollRoot.AddComponent<ScrollRect>();
            StretchToParent(scrollRoot.GetComponent<RectTransform>());

            var viewport = CreatePanel("Viewport", scrollRoot.transform, new Color(0f, 0f, 0f, 0.08f));
            StretchToParent(viewport.rectTransform);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = CreateUIObject("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(216f, 216f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.rectTransform;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            foreach (var champion in _catalog)
            {
                _gridItems.Add(CreateChampionGridItem(content.transform, champion));
            }
        }

        private ChampionGridItemView CreateChampionGridItem(Transform parent, ChampionDefinitionAsset champion)
        {
            var root = CreateUIObject("Champion_" + champion.Id, parent);
            var bg = root.AddComponent<Image>();
            bg.color = _tileIdleColor;
            var button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => OnChampionClicked(champion.Id));

            var portraitBg = CreatePanel("PortraitBg", root.transform, new Color(0.12f, 0.12f, 0.12f, 1f));
            portraitBg.rectTransform.anchorMin = new Vector2(0.04f, 0.16f);
            portraitBg.rectTransform.anchorMax = new Vector2(0.96f, 0.96f);
            portraitBg.rectTransform.offsetMin = Vector2.zero;
            portraitBg.rectTransform.offsetMax = Vector2.zero;

            var portrait = CreatePanel("Portrait", portraitBg.transform, new Color(1f, 1f, 1f, 0f));
            StretchToParent(portrait.rectTransform);
            portrait.preserveAspect = true;
            var portraitSprite = champion.AvatarSprite != null ? champion.AvatarSprite : champion.SplashSprite;
            portrait.sprite = portraitSprite;
            portrait.color = portraitSprite != null
                ? Color.white
                : new Color(0.14f, 0.14f, 0.14f, 1f);

            var nameText = CreateText("Name", root.transform, 18, TextAnchor.MiddleCenter, Color.white);
            nameText.text = champion.DisplayName;
            nameText.rectTransform.anchorMin = new Vector2(0.04f, 0.02f);
            nameText.rectTransform.anchorMax = new Vector2(0.96f, 0.16f);
            nameText.rectTransform.offsetMin = Vector2.zero;
            nameText.rectTransform.offsetMax = Vector2.zero;

            return new ChampionGridItemView
            {
                championId = champion.Id,
                background = bg,
                portraitBackground = portraitBg,
                portrait = portrait,
                nameText = nameText,
                button = button,
                hasSplash = portraitSprite != null
            };
        }

        private SlotView CreateSlot(Transform parent, int slotIndex)
        {
            var root = CreateUIObject("Slot_" + (slotIndex + 1), parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.minWidth = 220f;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;

            var bg = root.AddComponent<Image>();
            bg.color = _slotIdleColor;
            var button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            var capturedIndex = slotIndex;
            button.onClick.AddListener(() => OnSlotClicked(capturedIndex));

            var title = CreateText("Title", root.transform, 18, TextAnchor.UpperLeft, Color.white);
            title.rectTransform.anchorMin = new Vector2(0.08f, 0.84f);
            title.rectTransform.anchorMax = new Vector2(0.92f, 0.96f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;

            var portraitBg = CreatePanel("PortraitBg", root.transform, new Color(0.1f, 0.1f, 0.1f, 1f));
            portraitBg.rectTransform.anchorMin = new Vector2(0.08f, 0.22f);
            portraitBg.rectTransform.anchorMax = new Vector2(0.92f, 0.8f);
            portraitBg.rectTransform.offsetMin = Vector2.zero;
            portraitBg.rectTransform.offsetMax = Vector2.zero;

            var portrait = CreatePanel("Portrait", portraitBg.transform, new Color(1f, 1f, 1f, 0f));
            StretchToParent(portrait.rectTransform);
            portrait.preserveAspect = true;

            var nameText = CreateText("Name", root.transform, 20, TextAnchor.MiddleCenter, Color.white);
            nameText.rectTransform.anchorMin = new Vector2(0.08f, 0.04f);
            nameText.rectTransform.anchorMax = new Vector2(0.92f, 0.2f);
            nameText.rectTransform.offsetMin = Vector2.zero;
            nameText.rectTransform.offsetMax = Vector2.zero;

            return new SlotView
            {
                background = bg,
                portraitBackground = portraitBg,
                portrait = portrait,
                titleText = title,
                nameText = nameText,
                button = button
            };
        }

        private void OnSlotClicked(int slotIndex)
        {
            _activeSlotIndex = Mathf.Clamp(slotIndex, 0, 2);
            RefreshUI();
        }

        private void OnChampionClicked(string championId)
        {
            if (string.IsNullOrWhiteSpace(championId))
            {
                return;
            }

            if (!_manager.IsChampionUnlocked(championId))
            {
                return;
            }

            for (var i = 0; i < _slotChampionIds.Length; i++)
            {
                if (i != _activeSlotIndex && _slotChampionIds[i] == championId)
                {
                    _slotChampionIds[i] = string.Empty;
                }
            }

            _slotChampionIds[_activeSlotIndex] = championId;
            AdvanceActiveSlot();
            SyncSlotsToRunData();
            RefreshUI();
        }

        private void AdvanceActiveSlot()
        {
            if (CountFilledSlots() >= 3)
            {
                _activeSlotIndex = 2;
                return;
            }

            for (var i = _activeSlotIndex + 1; i < _slotChampionIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(_slotChampionIds[i]))
                {
                    _activeSlotIndex = i;
                    return;
                }
            }

            for (var i = 0; i < _slotChampionIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(_slotChampionIds[i]))
                {
                    _activeSlotIndex = i;
                    return;
                }
            }
        }

        private void SyncSlotsToRunData()
        {
            _manager.CurrentRun.SetTeam(_slotChampionIds.Where(id => !string.IsNullOrWhiteSpace(id)));
        }

        private void RefreshUI()
        {
            RefreshSlots();
            RefreshGridTiles();
            RefreshPlayState();
        }

        private void RefreshSlots()
        {
            if (_useManualUi)
            {
                RefreshManualTeamCards();
                return;
            }

            for (var i = 0; i < _slotViews.Length; i++)
            {
                var slot = _slotViews[i];
                var champion = ChampionCatalog.FindById(_slotChampionIds[i]);
                var hasChampion = champion != null;
                slot.background.color = i == _activeSlotIndex
                    ? _slotActiveColor
                    : (hasChampion ? _slotFilledColor : _slotIdleColor);
                slot.titleText.text = "Slot " + (i + 1) + (i == _activeSlotIndex ? "  •  Active" : string.Empty);
                slot.nameText.text = hasChampion ? champion.DisplayName : "Empty";
                slot.portrait.sprite = hasChampion
                    ? (champion.AvatarSprite != null ? champion.AvatarSprite : champion.SplashSprite)
                    : null;
                slot.portrait.color = hasChampion ? Color.white : new Color(0.22f, 0.22f, 0.22f, 1f);
                var rarityBg = ResolveRarityBackgroundSprite(champion);
                slot.portraitBackground.sprite = rarityBg;
                slot.portraitBackground.color = rarityBg != null
                    ? Color.white
                    : (hasChampion && champion.RarityDefinition != null ? champion.RarityDefinition.BackgroundColor : new Color(0.1f, 0.1f, 0.1f, 1f));
            }
        }

        private void RefreshGridTiles()
        {
            if (_useManualUi)
            {
                RefreshManualGridTiles();
                return;
            }

            for (var i = 0; i < _gridItems.Count; i++)
            {
                var item = _gridItems[i];
                var selected = _slotChampionIds.Contains(item.championId);
                var unlocked = _manager.IsChampionUnlocked(item.championId);
                var champion = ChampionCatalog.FindById(item.championId);
                var rarityColor = champion != null && champion.RarityDefinition != null
                    ? champion.RarityDefinition.BackgroundColor
                    : _tileIdleColor;

                item.button.interactable = unlocked;
                item.background.color = !unlocked
                    ? _tileLockedColor
                    : (selected ? _tileSelectedColor : rarityColor);
                var rarityBg = ResolveRarityBackgroundSprite(champion);
                item.portraitBackground.sprite = rarityBg;
                item.portraitBackground.color = rarityBg != null ? Color.white : rarityColor;
                if (!item.hasSplash)
                {
                    item.portrait.color = unlocked
                        ? new Color(0.14f, 0.14f, 0.14f, 1f)
                        : new Color(0.2f, 0.2f, 0.2f, 1f);
                }
                else
                {
                    item.portrait.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
                }
                item.nameText.color = unlocked ? Color.white : new Color(0.82f, 0.82f, 0.82f, 1f);
            }
        }

        private void RefreshPlayState()
        {
            if (_useManualUi)
            {
                return;
            }

            var canPlay = _manager.CurrentRun.HasValidTeam();
            _playButton.interactable = canPlay;
            _playButtonImage.color = canPlay ? _playEnabledColor : _playDisabledColor;
            _playButtonText.color = canPlay ? Color.black : new Color(0.9f, 0.9f, 0.9f, 1f);
            _playButtonText.text = canPlay ? "PLAY" : "SELECT 3";
            _helperText.text = canPlay
                ? "Team ready."
                : "Choose 3 different champions to start.";
        }

        private int CountFilledSlots()
        {
            return _slotChampionIds.Count(id => !string.IsNullOrWhiteSpace(id));
        }

        private void RefreshManualTeamCards()
        {
            for (var i = 0; i < _manualTeamCardViews.Length; i++)
            {
                var card = _manualTeamCardViews[i];
                if (card == null)
                {
                    continue;
                }

                var champion = ChampionCatalog.FindById(_slotChampionIds[i]);
                if (champion == null)
                {
                    card.ShowIdle();
                    continue;
                }

                card.Bind(champion);
            }
        }

        private void RefreshManualGridTiles()
        {
            for (var i = 0; i < _manualHeroViews.Count; i++)
            {
                var view = _manualHeroViews[i];
                if (view == null || view.Champion == null)
                {
                    continue;
                }

                var championId = view.ChampionId;
                view.Bind(
                    view.Champion,
                    _manager.IsChampionUnlocked(championId),
                    _slotChampionIds.Contains(championId));
            }
        }

        private void ApplySafeArea(bool force)
        {
            if (_safeAreaRoot == null)
            {
                return;
            }

            var safe = Screen.safeArea;
            if (!force && safe == _lastSafeArea)
            {
                return;
            }

            _lastSafeArea = safe;
            var min = safe.position;
            var max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _safeAreaRoot.anchorMin = min;
            _safeAreaRoot.anchorMax = max;
            _safeAreaRoot.offsetMin = Vector2.zero;
            _safeAreaRoot.offsetMax = Vector2.zero;
        }

        private void EnsureEventSystem()
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

        private Text CreateText(string name, Transform parent, int fontSize, TextAnchor anchor, Color color)
        {
            var go = CreateUIObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static Sprite ResolveRarityBackgroundSprite(ChampionDefinitionAsset champion)
        {
            if (champion == null)
            {
                return null;
            }

            if (champion.RarityDefinition != null && champion.RarityDefinition.BackgroundSprite != null)
            {
                return champion.RarityDefinition.BackgroundSprite;
            }

            string key;
            switch (champion.TierStars)
            {
                case 6:
                    key = "lr";
                    break;
                case 5:
                    key = "ssr";
                    break;
                case 4:
                    key = "sr";
                    break;
                case 3:
                    key = "r";
                    break;
                default:
                    key = "default";
                    break;
            }

            return Resources.Load<Sprite>("Art/UI/Rarity/" + key);
        }

        private static Transform FindDeepChildByName(string expectedName)
        {
            if (string.IsNullOrWhiteSpace(expectedName))
            {
                return null;
            }

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                {
                    continue;
                }

                var match = FindDeepChildByName(root.transform, expectedName.Trim());
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform FindDeepChildByName(Transform root, string expectedName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name.Trim() == expectedName)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var match = FindDeepChildByName(child, expectedName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
