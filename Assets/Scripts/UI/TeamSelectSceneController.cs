using System.Collections.Generic;
using System.Linq;
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
            public Image portrait;
            public Text nameText;
            public Button button;
            public bool hasSplash;
        }

        private sealed class SlotView
        {
            public Image background;
            public Image portrait;
            public Text titleText;
            public Text nameText;
            public Button button;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != GameScenes.TeamSelect)
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

        private Image _playButtonImage;
        private Button _playButton;
        private Text _playButtonText;
        private Text _helperText;

        private void Start()
        {
            _manager = GameFlowManager.Instance;
            if (_manager == null)
            {
                Debug.LogWarning("TeamSelectSceneController: GameFlowManager not found.");
                enabled = false;
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                Debug.LogWarning("TeamSelectSceneController: built-in font LegacyRuntime.ttf not found.");
                enabled = false;
                return;
            }

            _catalog.Clear();
            _catalog.AddRange(_manager.GetChampionCatalog().Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id)));

            EnsureEventSystem();
            BuildLayout();
            InitializeSlotsFromRun();
            ApplySafeArea(true);
            RefreshUI();
        }

        private void Update()
        {
            ApplySafeArea(false);
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

            var portrait = CreatePanel("Portrait", root.transform, new Color(0.12f, 0.12f, 0.12f, 1f));
            portrait.rectTransform.anchorMin = new Vector2(0.04f, 0.16f);
            portrait.rectTransform.anchorMax = new Vector2(0.96f, 0.96f);
            portrait.rectTransform.offsetMin = Vector2.zero;
            portrait.rectTransform.offsetMax = Vector2.zero;
            portrait.preserveAspect = true;
            portrait.sprite = champion.SplashSprite;
            portrait.color = champion.SplashSprite != null
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
                portrait = portrait,
                nameText = nameText,
                button = button,
                hasSplash = champion.SplashSprite != null
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

            var portrait = CreatePanel("Portrait", root.transform, new Color(0.1f, 0.1f, 0.1f, 1f));
            portrait.rectTransform.anchorMin = new Vector2(0.08f, 0.22f);
            portrait.rectTransform.anchorMax = new Vector2(0.92f, 0.8f);
            portrait.rectTransform.offsetMin = Vector2.zero;
            portrait.rectTransform.offsetMax = Vector2.zero;
            portrait.preserveAspect = true;

            var nameText = CreateText("Name", root.transform, 20, TextAnchor.MiddleCenter, Color.white);
            nameText.rectTransform.anchorMin = new Vector2(0.08f, 0.04f);
            nameText.rectTransform.anchorMax = new Vector2(0.92f, 0.2f);
            nameText.rectTransform.offsetMin = Vector2.zero;
            nameText.rectTransform.offsetMax = Vector2.zero;

            return new SlotView
            {
                background = bg,
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
                slot.portrait.sprite = hasChampion ? champion.SplashSprite : null;
                slot.portrait.color = hasChampion ? Color.white : new Color(0.22f, 0.22f, 0.22f, 1f);
            }
        }

        private void RefreshGridTiles()
        {
            for (var i = 0; i < _gridItems.Count; i++)
            {
                var item = _gridItems[i];
                var selected = _slotChampionIds.Contains(item.championId);
                var unlocked = _manager.IsChampionUnlocked(item.championId);

                item.button.interactable = unlocked;
                item.background.color = !unlocked
                    ? _tileLockedColor
                    : (selected ? _tileSelectedColor : _tileIdleColor);
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

        private void ApplySafeArea(bool force)
        {
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
    }
}
