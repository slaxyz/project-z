using System.Collections.Generic;
using System.Linq;
using ProjectZ.Combat;
using ProjectZ.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class CenterBoardSpellRackView : MonoBehaviour
    {
        private const int MaxDisplayedSpells = 6;

        private static Dictionary<string, CombatSpellAsset> _spellIndexCache;
        private readonly List<GameObject> _runtimeInstances = new List<GameObject>();
        private RectTransform _templateRoot;
        private HeroSwitchPanelView _heroSwitchPanel;
        private string _lastChampionId = string.Empty;
        private string _lastSpellSignature = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.Fight)
            {
                return;
            }

            var centerBoard = GameObject.Find("CenterBoard");
            if (centerBoard == null || centerBoard.GetComponent<CenterBoardSpellRackView>() != null)
            {
                return;
            }

            centerBoard.AddComponent<CenterBoardSpellRackView>();
        }

        private void Awake()
        {
            CacheTemplate();
            CacheSpellIndex();
            CacheHeroSwitchPanel();
            RefreshIfNeeded(force: true);
        }

        private void OnEnable()
        {
            CacheTemplate();
            CacheSpellIndex();
            CacheHeroSwitchPanel();
            RefreshIfNeeded(force: true);
        }

        private void Update()
        {
            RefreshIfNeeded(force: false);
        }

        private void OnDisable()
        {
            UnhookHeroSwitchPanel();
            SetAllRuntimeInstancesVisible(false);
        }

        public void RefreshIfNeeded(bool force)
        {
            var championId = ResolveActiveChampionId();
            var spellIds = ResolveActiveChampionSpellIds(championId);
            var signature = string.Join("|", spellIds);

            if (!force && championId == _lastChampionId && signature == _lastSpellSignature)
            {
                return;
            }

            _lastChampionId = championId;
            _lastSpellSignature = signature;
            Rebuild(spellIds);
        }

        private void CacheTemplate()
        {
            if (_templateRoot != null)
            {
                return;
            }

            var template = FindTemplateRoot();
            if (template == null)
            {
                Debug.LogWarning("CenterBoardSpellRackView could not find a Spell template under CenterBoard.");
                return;
            }

            _templateRoot = template;
            SetUiVisibility(_templateRoot.gameObject, false);
        }

        private RectTransform FindTemplateRoot()
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.GetComponentInChildren<SpellEffectView>(true) != null)
                {
                    return child as RectTransform;
                }
            }

            return transform.childCount > 0 ? transform.GetChild(0) as RectTransform : null;
        }

        private string ResolveActiveChampionId()
        {
            if (_heroSwitchPanel == null)
            {
                CacheHeroSwitchPanel();
            }

            if (_heroSwitchPanel != null && !string.IsNullOrWhiteSpace(_heroSwitchPanel.CurrentSelectedChampionId))
            {
                return _heroSwitchPanel.CurrentSelectedChampionId;
            }

            var fight = FindFirstObjectByType<FightMockController>();
            if (fight != null && !string.IsNullOrWhiteSpace(fight.ActiveChampionId))
            {
                return fight.ActiveChampionId;
            }

            var manager = GameFlowManager.Instance;
            if (manager != null)
            {
                var selected = manager.GetSelectedChampionIdsForRun();
                if (selected != null && selected.Count > 0)
                {
                    return selected[0];
                }
            }

            return string.Empty;
        }

        private List<string> ResolveActiveChampionSpellIds(string championId)
        {
            if (string.IsNullOrWhiteSpace(championId))
            {
                return new List<string>();
            }

            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return new List<string>();
            }

            return manager.GetChampionSpellLoadout(championId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Take(MaxDisplayedSpells)
                .ToList();
        }

        private void Rebuild(IReadOnlyList<string> spellIds)
        {
            SetAllRuntimeInstancesVisible(false);

            if (_templateRoot == null || spellIds == null || spellIds.Count == 0)
            {
                return;
            }

            var spellIndex = CacheSpellIndex();

            for (var i = 0; i < spellIds.Count; i++)
            {
                var spellId = spellIds[i];
                if (!spellIndex.TryGetValue(spellId, out var spellAsset) || spellAsset == null)
                {
                    continue;
                }

                var instance = GetOrCreateInstance(i);
                instance.name = "Runtime_Spell_" + i;
                instance.transform.SetSiblingIndex(i);

                var effectView = instance.GetComponentInChildren<SpellEffectView>(true);
                if (effectView != null)
                {
                    effectView.SetSpell(spellAsset);
                }

                SetUiVisibility(instance, true);
            }
        }

        private void CacheHeroSwitchPanel()
        {
            if (_heroSwitchPanel != null)
            {
                return;
            }

            _heroSwitchPanel = FindFirstObjectByType<HeroSwitchPanelView>();
            if (_heroSwitchPanel == null)
            {
                return;
            }

            _heroSwitchPanel.SelectionChanged -= HandleHeroSelectionChanged;
            _heroSwitchPanel.SelectionChanged += HandleHeroSelectionChanged;
        }

        private void UnhookHeroSwitchPanel()
        {
            if (_heroSwitchPanel == null)
            {
                return;
            }

            _heroSwitchPanel.SelectionChanged -= HandleHeroSelectionChanged;
        }

        private void HandleHeroSelectionChanged(HeroSwitchSlotView slot)
        {
            RefreshIfNeeded(force: true);
        }

        private static Dictionary<string, CombatSpellAsset> CacheSpellIndex()
        {
            if (_spellIndexCache != null)
            {
                return _spellIndexCache;
            }

            var registry = RuntimeAssetRegistryAsset.Load();
            var spellLibrary = registry != null ? registry.SpellLibrary : null;
            _spellIndexCache = spellLibrary != null
                ? spellLibrary.BuildIndexById()
                : new Dictionary<string, CombatSpellAsset>();
            return _spellIndexCache;
        }

        private GameObject GetOrCreateInstance(int index)
        {
            while (_runtimeInstances.Count <= index)
            {
                var instance = Instantiate(_templateRoot.gameObject, transform, false);
                SetUiVisibility(instance, false);
                _runtimeInstances.Add(instance);
            }

            return _runtimeInstances[index];
        }

        private void SetAllRuntimeInstancesVisible(bool isVisible)
        {
            for (var i = 0; i < _runtimeInstances.Count; i++)
            {
                var instance = _runtimeInstances[i];
                if (instance != null)
                {
                    SetUiVisibility(instance, isVisible);
                }
            }
        }

        private static void SetUiVisibility(GameObject target, bool isVisible)
        {
            if (target == null)
            {
                return;
            }

            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;

            var layoutElement = target.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = target.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = !isVisible;
        }
    }
}
