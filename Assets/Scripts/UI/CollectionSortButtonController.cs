using ProjectZ.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public class CollectionSortButtonController : MonoBehaviour
    {
        [Header("Links")]
        [SerializeField] private CollectionHeroCarouselController carouselController;
        [SerializeField] private GameObject sortPopover;

        [Header("Actions")]
        [SerializeField] private Button sortToggleButton;
        [SerializeField] private Button optionLevelButton;
        [SerializeField] private Button optionRarityButton;
        [SerializeField] private Button closeButton;

        [Header("Safe Zone")]
        [SerializeField] private float safeZonePadding = 20f;
        [SerializeField] private bool previewOpenInEditor;

        private RectTransform _rootRect;
        private RectTransform _optionLevelRect;
        private RectTransform _optionRarityRect;
        private RectTransform _closeRect;
        private bool _isOpen;

        private void Awake()
        {
            AutoAssignIfNeeded();
            EnsureButtons();
            ApplyInitialVisibility();
        }

        private void OnEnable()
        {
            AutoAssignIfNeeded();
            EnsureButtons();
            HookButtons();
        }

        private void OnDisable()
        {
            UnhookButtons();
        }

        private void Reset()
        {
            AutoAssignIfNeeded();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
            ApplyEditorPreviewVisibility();
        }

        private void Update()
        {
            if (!_isOpen)
            {
                return;
            }

            if (!TryGetPointerDownPosition(out var screenPosition))
            {
                return;
            }

            if (IsInsideSafeZone(screenPosition))
            {
                return;
            }

            Close();
        }

        private void OnToggleClicked()
        {
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void OnSortByLevelClicked()
        {
            if (carouselController != null)
            {
                carouselController.SetSortMode(CollectionHeroSortMode.ByLevel);
            }

            Close();
        }

        private void OnSortByRarityClicked()
        {
            if (carouselController != null)
            {
                carouselController.SetSortMode(CollectionHeroSortMode.ByRarity);
            }

            Close();
        }

        public void Open()
        {
            _isOpen = true;
            if (sortPopover != null)
            {
                sortPopover.SetActive(true);
            }
        }

        public void Close()
        {
            _isOpen = false;
            if (sortPopover != null)
            {
                sortPopover.SetActive(false);
            }
        }

        private void CloseImmediate()
        {
            _isOpen = false;
            if (sortPopover != null)
            {
                sortPopover.SetActive(false);
            }
        }

        private void ApplyInitialVisibility()
        {
            if (Application.isPlaying)
            {
                CloseImmediate();
                return;
            }

            ApplyEditorPreviewVisibility();
        }

        private void ApplyEditorPreviewVisibility()
        {
            if (Application.isPlaying || sortPopover == null)
            {
                return;
            }

            sortPopover.SetActive(previewOpenInEditor);
        }

        private void AutoAssignIfNeeded()
        {
            if (carouselController == null)
            {
                carouselController = FindFirstObjectByType<CollectionHeroCarouselController>();
            }

            if (sortPopover == null)
            {
                sortPopover = transform.Find("SortPopover")?.gameObject;
            }

            if (sortToggleButton == null)
            {
                sortToggleButton = GetComponent<Button>();
            }

            if (optionLevelButton == null)
            {
                optionLevelButton = transform.Find("SortPopover/Option_Level")?.GetComponent<Button>();
            }

            if (optionRarityButton == null)
            {
                optionRarityButton = transform.Find("SortPopover/Option_Rarity")?.GetComponent<Button>();
            }

            if (closeButton == null)
            {
                closeButton = transform.Find("SortPopover/CloseButton")?.GetComponent<Button>();
            }

            _rootRect = transform as RectTransform;
            _optionLevelRect = optionLevelButton != null ? optionLevelButton.transform as RectTransform : null;
            _optionRarityRect = optionRarityButton != null ? optionRarityButton.transform as RectTransform : null;
            _closeRect = closeButton != null ? closeButton.transform as RectTransform : null;
        }

        private void EnsureButtons()
        {
            sortToggleButton = EnsureButtonOnObject(gameObject, "Face");

            var optionLevelObject = transform.Find("SortPopover/Option_Level")?.gameObject;
            if (optionLevelObject != null)
            {
                optionLevelButton = EnsureButtonOnObject(optionLevelObject, "OptionBG");
            }

            var optionRarityObject = transform.Find("SortPopover/Option_Rarity")?.gameObject;
            if (optionRarityObject != null)
            {
                optionRarityButton = EnsureButtonOnObject(optionRarityObject, "OptionBG");
            }

            var closeObject = transform.Find("SortPopover/CloseButton")?.gameObject;
            if (closeObject != null)
            {
                closeButton = EnsureButtonOnObject(closeObject, "Face");
            }
        }

        private void HookButtons()
        {
            if (sortToggleButton != null)
            {
                sortToggleButton.onClick.RemoveListener(OnToggleClicked);
                sortToggleButton.onClick.AddListener(OnToggleClicked);
            }

            if (optionLevelButton != null)
            {
                optionLevelButton.onClick.RemoveListener(OnSortByLevelClicked);
                optionLevelButton.onClick.AddListener(OnSortByLevelClicked);
            }

            if (optionRarityButton != null)
            {
                optionRarityButton.onClick.RemoveListener(OnSortByRarityClicked);
                optionRarityButton.onClick.AddListener(OnSortByRarityClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
        }

        private void UnhookButtons()
        {
            if (sortToggleButton != null)
            {
                sortToggleButton.onClick.RemoveListener(OnToggleClicked);
            }

            if (optionLevelButton != null)
            {
                optionLevelButton.onClick.RemoveListener(OnSortByLevelClicked);
            }

            if (optionRarityButton != null)
            {
                optionRarityButton.onClick.RemoveListener(OnSortByRarityClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }
        }

        private bool IsInsideSafeZone(Vector2 screenPosition)
        {
            return ContainsScreenPoint(_rootRect, screenPosition)
                || ContainsScreenPoint(_optionLevelRect, screenPosition)
                || ContainsScreenPoint(_optionRarityRect, screenPosition)
                || ContainsScreenPoint(_closeRect, screenPosition);
        }

        private bool ContainsScreenPoint(RectTransform rectTransform, Vector2 screenPosition)
        {
            if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
            {
                return false;
            }

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

            var rect = Rect.MinMaxRect(
                min.x - safeZonePadding,
                min.y - safeZonePadding,
                max.x + safeZonePadding,
                max.y + safeZonePadding);

            return rect.Contains(screenPosition);
        }

        private static bool TryGetPointerDownPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
#else
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    screenPosition = touch.position;
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif

            screenPosition = default;
            return false;
        }

        private static Button EnsureButtonOnObject(GameObject targetObject, string targetGraphicChildName)
        {
            if (targetObject == null)
            {
                return null;
            }

            var button = targetObject.GetComponent<Button>();
            if (button == null)
            {
                button = targetObject.AddComponent<Button>();
            }

            if (button.targetGraphic == null)
            {
                var targetGraphic = targetObject.transform.Find(targetGraphicChildName)?.GetComponent<Graphic>();
                if (targetGraphic == null)
                {
                    targetGraphic = targetObject.GetComponent<Graphic>();
                }

                button.targetGraphic = targetGraphic;
            }

            return button;
        }
    }
}
