using System.Collections.Generic;
using System.Linq;
using ProjectZ.Combat;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class BottomBarRunesController : MonoBehaviour
    {
        private readonly List<RuneSlotView> _runeViews = new List<RuneSlotView>();

        private FightMockController _fight;
        private Transform _refreshRoot;
        private Button _refreshButton;
        private readonly List<TMP_Text> _refreshCountTexts = new List<TMP_Text>();

        private void Awake()
        {
            CacheSceneReferences();
        }

        private void OnEnable()
        {
            CacheSceneReferences();
            HookRuneViews();
            HookRefreshButton();
            SyncFromFight();
        }

        private void OnDisable()
        {
            UnhookRuneViews();

            if (_refreshButton != null)
            {
                _refreshButton.onClick.RemoveListener(HandleRefreshClicked);
            }
        }

        private void Update()
        {
            SyncFromFight();
        }

        private void CacheSceneReferences()
        {
            if (_fight == null)
            {
                _fight = Object.FindFirstObjectByType<FightMockController>();
            }

            if (_refreshRoot == null)
            {
                _refreshRoot = FindDeepChild("Refresh");
            }

            if (_refreshButton == null && _refreshRoot != null)
            {
                _refreshButton = _refreshRoot.GetComponent<Button>();
                if (_refreshButton == null)
                {
                    _refreshButton = _refreshRoot.GetComponentInChildren<Button>(true);
                }

                CacheRefreshCountTexts(_refreshRoot);
            }

            _runeViews.Clear();
            _runeViews.AddRange(GetComponentsInChildren<RuneSlotView>(true)
                .OrderBy(view => view.transform.GetSiblingIndex()));

            for (var i = 0; i < _runeViews.Count; i++)
            {
                _runeViews[i].BindSlotIndex(i);
            }
        }

        private void CacheRefreshCountTexts(Transform refreshRoot)
        {
            _refreshCountTexts.Clear();
            if (refreshRoot == null)
            {
                return;
            }

            var numberRoot = refreshRoot.Find("Number");
            if (numberRoot == null)
            {
                return;
            }

            _refreshCountTexts.AddRange(numberRoot.GetComponentsInChildren<TMP_Text>(true));
        }

        private Transform FindDeepChild(string childName)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform || child.name != childName)
                {
                    continue;
                }

                return child;
            }

            return null;
        }

        private void HookRuneViews()
        {
            foreach (var runeView in _runeViews)
            {
                runeView.Clicked -= HandleRuneClicked;
                runeView.Clicked += HandleRuneClicked;
            }
        }

        private void UnhookRuneViews()
        {
            foreach (var runeView in _runeViews)
            {
                runeView.Clicked -= HandleRuneClicked;
            }
        }

        private void HookRefreshButton()
        {
            if (_refreshButton == null)
            {
                return;
            }

            _refreshButton.onClick.RemoveListener(HandleRefreshClicked);
            _refreshButton.onClick.AddListener(HandleRefreshClicked);
        }

        private void HandleRuneClicked(RuneSlotView runeView)
        {
            if (_fight == null || runeView == null)
            {
                return;
            }

            _fight.TryToggleRuneLock(runeView.SlotIndex);
            SyncFromFight();
        }

        private void HandleRefreshClicked()
        {
            if (_fight == null)
            {
                return;
            }

            _fight.RequestRefreshRunes();
            SyncFromFight();
        }

        private void SyncFromFight()
        {
            if (_fight == null)
            {
                _fight = Object.FindFirstObjectByType<FightMockController>();
                if (_fight == null)
                {
                    return;
                }
            }

            if (_refreshRoot == null || _refreshButton == null)
            {
                CacheSceneReferences();
                HookRefreshButton();
            }

            for (var i = 0; i < _runeViews.Count; i++)
            {
                if (!_fight.TryGetRuneState(i, out var element, out var isLocked, out var isAvailable))
                {
                    continue;
                }

                _runeViews[i].ApplyState(element, isLocked, isAvailable);
            }

            if (_refreshButton != null)
            {
                _refreshButton.interactable = _fight.CanRefreshRunes;
            }

            if (_refreshCountTexts.Count > 0)
            {
                var remainingRolls = _fight.RerollsRemaining.ToString();
                foreach (var countText in _refreshCountTexts)
                {
                    if (countText != null)
                    {
                        countText.text = remainingRolls;
                    }
                }
            }
        }
    }
}
