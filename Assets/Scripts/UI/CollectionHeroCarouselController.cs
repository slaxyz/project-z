using System.Collections.Generic;
using System.Linq;
using ProjectZ.Core;
using ProjectZ.Run;
using UnityEngine;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public class CollectionHeroCarouselController : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] private Transform contentRoot;
        [SerializeField] private HeroSelectorView heroSelectorPrefab;

        [Header("Behavior")]
        [SerializeField] private bool populateOnStart = true;
        [SerializeField] private string selectedChampionId;

        private readonly List<HeroSelectorView> _views = new List<HeroSelectorView>();
        private GameFlowManager _manager;

        public ChampionDefinitionAsset SelectedChampion { get; private set; }

        public event System.Action<ChampionDefinitionAsset> SelectionChanged;

        private void Start()
        {
            if (!populateOnStart)
            {
                return;
            }

            Rebuild();
        }

        private void Reset()
        {
            AutoAssignIfNeeded();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
        }

        public void Rebuild()
        {
            AutoAssignIfNeeded();

            _manager = GameFlowManager.Instance;
            if (_manager == null)
            {
                Debug.LogWarning("CollectionHeroCarouselController: GameFlowManager not found.");
                return;
            }

            if (contentRoot == null)
            {
                Debug.LogWarning("CollectionHeroCarouselController: missing Content Root.");
                return;
            }

            if (heroSelectorPrefab == null)
            {
                Debug.LogWarning("CollectionHeroCarouselController: missing Hero Selector Prefab.");
                return;
            }

            ClearContent();

            var champions = _manager.GetChampionCatalog()
                .Where(champion => champion != null && !string.IsNullOrWhiteSpace(champion.Id))
                .ToList();

            if (champions.Count == 0)
            {
                SelectedChampion = null;
                return;
            }

            var fallbackSelectedId = ResolveInitialSelectedId(champions);

            foreach (var champion in champions)
            {
                var view = Instantiate(heroSelectorPrefab, contentRoot);
                view.name = "Hero_" + champion.Id;
                view.Clicked += OnHeroClicked;
                view.Bind(
                    champion,
                    _manager.IsChampionUnlocked(champion.Id),
                    champion.Id == fallbackSelectedId);

                _views.Add(view);
            }

            SelectChampion(fallbackSelectedId, true);
        }

        public void RefreshOwnership()
        {
            if (_manager == null)
            {
                _manager = GameFlowManager.Instance;
            }

            if (_manager == null)
            {
                return;
            }

            for (var i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view == null || string.IsNullOrWhiteSpace(view.ChampionId))
                {
                    continue;
                }

                view.SetOwned(_manager.IsChampionUnlocked(view.ChampionId));
            }
        }

        public void SelectChampion(string championId)
        {
            SelectChampion(championId, true);
        }

        private void SelectChampion(string championId, bool notify)
        {
            if (_views.Count == 0)
            {
                SelectedChampion = null;
                selectedChampionId = string.Empty;
                return;
            }

            var fallbackId = _views[0].ChampionId;
            selectedChampionId = _views.Any(view => view != null && view.ChampionId == championId)
                ? championId
                : fallbackId;

            for (var i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view == null)
                {
                    continue;
                }

                view.SetSelected(view.ChampionId == selectedChampionId);
                if (view.ChampionId == selectedChampionId)
                {
                    SelectedChampion = view.Champion;
                }
            }

            if (notify)
            {
                SelectionChanged?.Invoke(SelectedChampion);
            }
        }

        private void OnHeroClicked(HeroSelectorView view)
        {
            if (view == null)
            {
                return;
            }

            SelectChampion(view.ChampionId, true);
        }

        private void AutoAssignIfNeeded()
        {
            if (contentRoot == null)
            {
                contentRoot = transform.Find("CarouselViewport/CarouselContent");
                if (contentRoot == null)
                {
                    contentRoot = transform.Find("CarouselContent");
                }
            }
#if UNITY_EDITOR
            if (heroSelectorPrefab == null)
            {
                var prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/HeroSelector.prefab");
                if (prefabAsset != null)
                {
                    heroSelectorPrefab = prefabAsset.GetComponent<HeroSelectorView>();
                }
            }
#endif
        }

        private string ResolveInitialSelectedId(IReadOnlyList<ChampionDefinitionAsset> champions)
        {
            if (!string.IsNullOrWhiteSpace(selectedChampionId) &&
                champions.Any(champion => champion != null && champion.Id == selectedChampionId))
            {
                return selectedChampionId;
            }

            var suggestedId = _manager.GetDefaultSelectedChampionIdForCollection();
            if (!string.IsNullOrWhiteSpace(suggestedId) &&
                champions.Any(champion => champion != null && champion.Id == suggestedId))
            {
                return suggestedId;
            }

            return champions[0].Id;
        }

        private void ClearContent()
        {
            for (var i = _views.Count - 1; i >= 0; i--)
            {
                var view = _views[i];
                if (view != null)
                {
                    view.Clicked -= OnHeroClicked;
                }
            }

            _views.Clear();

            for (var i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var child = contentRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }
    }
}
