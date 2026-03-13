using System.Collections.Generic;
using ProjectZ.Run;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public class CollectionHeroShowcaseView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private CollectionHeroCarouselController carouselController;

        [Header("Showcase refs")]
        [SerializeField] private Image splashImage;
        [SerializeField] private ClassBadgeView classBadgeView;
        [SerializeField] private TypeBadgeView typeBadgeView;
        [SerializeField] private RectTransform starsRoot;

        [Header("Stars")]
        [SerializeField] private Vector2 starSize = new Vector2(32f, 32f);
        [SerializeField] private bool typeBadgeUsesSpiral = true;

        private readonly List<Image> _stars = new List<Image>();
        private Sprite _starSprite;

        private void OnEnable()
        {
            AutoAssignIfNeeded();
            EnsureStarSprite();

            if (carouselController != null)
            {
                carouselController.SelectionChanged += OnSelectionChanged;
                if (carouselController.SelectedChampion != null)
                {
                    SetChampion(carouselController.SelectedChampion);
                }
            }
        }

        private void OnDisable()
        {
            if (carouselController != null)
            {
                carouselController.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void Reset()
        {
            AutoAssignIfNeeded();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
        }

        public void SetChampion(ChampionDefinitionAsset champion)
        {
            if (champion == null)
            {
                ClearVisuals();
                return;
            }

            if (splashImage != null)
            {
                var splash = champion.SplashSprite != null ? champion.SplashSprite : champion.AvatarSprite;
                splashImage.sprite = splash;
                splashImage.color = splash != null ? Color.white : Color.clear;
                splashImage.preserveAspect = true;
            }

            if (classBadgeView != null)
            {
                classBadgeView.SetClass(champion.ClassDefinition);
            }

            if (typeBadgeView != null)
            {
                typeBadgeView.SetType(champion.TypeDefinition, typeBadgeUsesSpiral);
            }

            RefreshStars(champion.TierStars);
        }

        private void OnSelectionChanged(ChampionDefinitionAsset champion)
        {
            SetChampion(champion);
        }

        private void AutoAssignIfNeeded()
        {
            if (carouselController == null)
            {
                carouselController = FindFirstObjectByType<CollectionHeroCarouselController>();
            }

            if (splashImage == null)
            {
                splashImage = transform.Find("ArtMask/SplashArt")?.GetComponent<Image>();
            }

            if (classBadgeView == null)
            {
                classBadgeView = GetComponentInChildren<ClassBadgeView>(true);
            }

            if (typeBadgeView == null)
            {
                typeBadgeView = GetComponentInChildren<TypeBadgeView>(true);
            }

            if (starsRoot == null)
            {
                starsRoot = transform.Find("Stars") as RectTransform;
            }
        }

        private void EnsureStarSprite()
        {
            if (_starSprite == null)
            {
                _starSprite = Resources.Load<Sprite>("Art/UI/Stars/champion_star");
            }
        }

        private void RefreshStars(int starCount)
        {
            if (starsRoot == null)
            {
                return;
            }

            EnsureStarSprite();
            starCount = Mathf.Clamp(starCount, 0, 6);

            while (_stars.Count < starCount)
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

            for (var i = 0; i < _stars.Count; i++)
            {
                var starImage = _stars[i];
                if (starImage == null)
                {
                    continue;
                }

                var active = i < starCount;
                starImage.gameObject.SetActive(active);
                if (active)
                {
                    starImage.sprite = _starSprite;
                    starImage.color = _starSprite != null ? Color.white : Color.clear;
                }
            }
        }

        private void ClearVisuals()
        {
            if (splashImage != null)
            {
                splashImage.sprite = null;
                splashImage.color = Color.clear;
            }

            if (classBadgeView != null)
            {
                classBadgeView.SetClass(null);
            }

            if (typeBadgeView != null)
            {
                typeBadgeView.SetType(null, typeBadgeUsesSpiral);
            }

            RefreshStars(0);
        }
    }
}
