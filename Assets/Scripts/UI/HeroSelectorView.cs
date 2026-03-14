using ProjectZ.Run;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public class HeroSelectorView : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] private GameObject selectedRoot;
        [SerializeField] private Image selectedFrameImage;
        [SerializeField] private Image rarityImage;
        [SerializeField] private Image cardFrameImage;
        [SerializeField] private HeroOwnershipVisual ownershipVisual;
        [SerializeField] private LayoutElement layoutElement;
        [SerializeField] private Button button;

        [Header("Sizes")]
        [SerializeField] private Vector2 defaultSize = new Vector2(120f, 120f);
        [SerializeField] private Vector2 selectedSize = new Vector2(120f, 120f);

        [Header("Optional Scene Rules")]
        [SerializeField] private bool showSelectedRootWhenSelected;

        private ChampionDefinitionAsset _champion;
        private bool _isOwned = true;
        private bool _isSelected;
        private bool _clickHooked;

        public ChampionDefinitionAsset Champion => _champion;
        public string ChampionId => _champion != null ? _champion.Id : string.Empty;

        public event System.Action<HeroSelectorView> Clicked;

        private void Awake()
        {
            AutoAssignIfNeeded();
            EnsureOwnershipVisual();
            EnsureButton();
            RefreshVisuals();
        }

        private void Reset()
        {
            AutoAssignIfNeeded();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
            RefreshVisuals();
        }

        public void Bind(ChampionDefinitionAsset champion, bool isOwned, bool isSelected)
        {
            _champion = champion;
            _isOwned = isOwned;
            _isSelected = isSelected;
            RefreshVisuals();
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            RefreshVisuals();
        }

        public void SetOwned(bool isOwned)
        {
            _isOwned = isOwned;
            RefreshVisuals();
        }

        public void SetShowSelectedRootWhenSelected(bool enabled)
        {
            showSelectedRootWhenSelected = enabled;
            RefreshVisuals();
        }

        private void AutoAssignIfNeeded()
        {
            if (selectedRoot == null)
            {
                selectedRoot = transform.Find("Selected")?.gameObject;
            }

            if (selectedFrameImage == null)
            {
                selectedFrameImage = transform.Find("SelectedFrame")?.GetComponent<Image>();
            }

            if (rarityImage == null)
            {
                rarityImage = transform.Find("Rarity")?.GetComponent<Image>();
            }

            if (cardFrameImage == null)
            {
                cardFrameImage = transform.Find("CardFrame")?.GetComponent<Image>();
            }

            if (ownershipVisual == null)
            {
                ownershipVisual = GetComponent<HeroOwnershipVisual>();
            }

            if (layoutElement == null)
            {
                layoutElement = GetComponent<LayoutElement>();
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void EnsureOwnershipVisual()
        {
            if (ownershipVisual != null)
            {
                return;
            }

            ownershipVisual = GetComponent<HeroOwnershipVisual>();
            if (ownershipVisual == null && Application.isPlaying)
            {
                ownershipVisual = gameObject.AddComponent<HeroOwnershipVisual>();
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

            if (!_clickHooked)
            {
                button.onClick.AddListener(NotifyClicked);
                _clickHooked = true;
            }

            if (button.targetGraphic == null)
            {
                button.targetGraphic = cardFrameImage != null ? cardFrameImage : rarityImage;
            }
        }

        private void RefreshVisuals()
        {
            AutoAssignIfNeeded();
            EnsureOwnershipVisual();

            if (selectedFrameImage != null)
            {
                selectedFrameImage.gameObject.SetActive(_isSelected);
            }

            if (selectedRoot != null)
            {
                selectedRoot.SetActive(showSelectedRootWhenSelected && _isSelected);
            }

            if (ownershipVisual != null)
            {
                ownershipVisual.Bind(_champion, _isOwned, _isSelected);
            }

            if (rarityImage != null)
            {
                var raritySprite = ResolveRaritySprite(_champion);
                rarityImage.sprite = raritySprite;
                rarityImage.color = raritySprite != null
                    ? Color.white
                    : ResolveRarityColor(_champion);
                rarityImage.preserveAspect = true;
            }

            ApplySize();
        }

        private void ApplySize()
        {
            var targetSize = _isSelected ? selectedSize : defaultSize;
            var rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = targetSize;
            }

            if (layoutElement != null)
            {
                layoutElement.preferredWidth = targetSize.x;
                layoutElement.preferredHeight = targetSize.y;
            }
        }

        private void NotifyClicked()
        {
            Clicked?.Invoke(this);
        }

        private static Color ResolveRarityColor(ChampionDefinitionAsset champion)
        {
            if (champion != null && champion.RarityDefinition != null)
            {
                return champion.RarityDefinition.BackgroundColor;
            }

            return Color.white;
        }

        private static Sprite ResolveRaritySprite(ChampionDefinitionAsset champion)
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

            var sprite = Resources.Load<Sprite>("Art/UI/Rarity/" + key);
            if (sprite != null)
            {
                return sprite;
            }

            return Resources.Load<Sprite>("Art/UI/Rarity/" + key.ToUpperInvariant());
        }
    }
}
