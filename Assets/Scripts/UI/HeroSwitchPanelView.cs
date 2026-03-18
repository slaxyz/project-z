using System;
using System.Collections.Generic;
using System.Linq;
using ProjectZ.Core;
using UnityEngine;

namespace ProjectZ.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HeroSwitchPanelView : MonoBehaviour
    {
        [SerializeField] private HeroSwitchSlotView[] slots = Array.Empty<HeroSwitchSlotView>();
        [SerializeField] private bool useDebugSlotOverrides;
        [SerializeField] private string[] debugChampionIds = new string[3];
        [SerializeField] private int selectedSlotIndex;

        private readonly List<HeroSwitchSlotView> _runtimeSlots = new List<HeroSwitchSlotView>();

        public HeroSwitchSlotView CurrentSelectedSlot { get; private set; }
        public string CurrentSelectedChampionId => CurrentSelectedSlot != null ? CurrentSelectedSlot.ChampionId : string.Empty;
        public int SelectedSlotIndex => selectedSlotIndex;

        public event Action<HeroSwitchSlotView> SelectionChanged;

        private void Awake()
        {
            AutoAssignIfNeeded();
            HookSlots();
            RefreshSlots();
        }

        private void OnEnable()
        {
            AutoAssignIfNeeded();
            HookSlots();
            RefreshSlots();
        }

        private void Start()
        {
            RefreshSlots();
        }

        private void OnDisable()
        {
            UnhookSlots();
        }

        private void Reset()
        {
            AutoAssignIfNeeded();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
            EnsureDebugArraySize();
            if (!Application.isPlaying)
            {
                CacheSlots();
                ApplyBindings();
                return;
            }

            HookSlots();
            RefreshSlots();
        }

        public void RefreshSlots()
        {
            AutoAssignIfNeeded();
            EnsureDebugArraySize();
            CacheSlots();
            ApplyBindings();
            ApplySelection();
        }

        public void SelectSlot(int index)
        {
            CacheSlots();
            if (index < 0 || index >= _runtimeSlots.Count)
            {
                return;
            }

            selectedSlotIndex = index;
            ApplySelection();
        }

        public IReadOnlyList<HeroSwitchSlotView> Slots()
        {
            CacheSlots();
            return _runtimeSlots;
        }

        private void ApplyBindings()
        {
            var ids = ResolveChampionIdsForSlots();
            for (var i = 0; i < _runtimeSlots.Count; i++)
            {
                var slot = _runtimeSlots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.SetSlotIndex(i);
                if (i < ids.Count && !string.IsNullOrWhiteSpace(ids[i]))
                {
                    slot.BindChampionId(ids[i]);
                }
                else
                {
                    slot.ClearBinding();
                }
            }
        }

        private void ApplySelection()
        {
            CacheSlots();
            if (_runtimeSlots.Count == 0)
            {
                CurrentSelectedSlot = null;
                return;
            }

            var filledSlots = _runtimeSlots
                .Where(slot => slot != null && !string.IsNullOrWhiteSpace(slot.ChampionId))
                .ToList();

            if (filledSlots.Count == 0)
            {
                selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, _runtimeSlots.Count - 1);
            }
            else
            {
                var hasValidSelected = selectedSlotIndex >= 0
                    && selectedSlotIndex < _runtimeSlots.Count
                    && !string.IsNullOrWhiteSpace(_runtimeSlots[selectedSlotIndex].ChampionId);

                if (!hasValidSelected)
                {
                    selectedSlotIndex = _runtimeSlots.IndexOf(filledSlots[0]);
                }
            }

            for (var i = 0; i < _runtimeSlots.Count; i++)
            {
                var slot = _runtimeSlots[i];
                if (slot == null)
                {
                    continue;
                }

                var shouldSelect = i == selectedSlotIndex && !string.IsNullOrWhiteSpace(slot.ChampionId);
                slot.SetSelected(shouldSelect);
                if (shouldSelect)
                {
                    CurrentSelectedSlot = slot;
                }
            }

            if (CurrentSelectedSlot == null && _runtimeSlots.Count > 0)
            {
                CurrentSelectedSlot = _runtimeSlots[Mathf.Clamp(selectedSlotIndex, 0, _runtimeSlots.Count - 1)];
            }
        }

        private IReadOnlyList<string> ResolveChampionIdsForSlots()
        {
            if (useDebugSlotOverrides)
            {
                return debugChampionIds;
            }

            var manager = GameFlowManager.Instance;
            return manager != null ? manager.SelectedChampionIds() : Array.Empty<string>();
        }

        private void AutoAssignIfNeeded()
        {
            if (slots != null && slots.Length > 0 && slots.All(slot => slot != null))
            {
                return;
            }

            slots = GetComponentsInChildren<HeroSwitchSlotView>(true)
                .OrderBy(slot => slot.transform.GetSiblingIndex())
                .ToArray();
        }

        private void CacheSlots()
        {
            _runtimeSlots.Clear();
            if (slots == null)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    _runtimeSlots.Add(slots[i]);
                }
            }
        }

        private void HookSlots()
        {
            CacheSlots();
            for (var i = 0; i < _runtimeSlots.Count; i++)
            {
                _runtimeSlots[i].Selected -= HandleSlotSelected;
                _runtimeSlots[i].Selected += HandleSlotSelected;
            }
        }

        private void UnhookSlots()
        {
            CacheSlots();
            for (var i = 0; i < _runtimeSlots.Count; i++)
            {
                _runtimeSlots[i].Selected -= HandleSlotSelected;
            }
        }

        private void HandleSlotSelected(HeroSwitchSlotView slot)
        {
            if (slot == null)
            {
                return;
            }

            CacheSlots();
            selectedSlotIndex = _runtimeSlots.IndexOf(slot);
            CurrentSelectedSlot = slot;
            SelectionChanged?.Invoke(slot);
        }

        private void EnsureDebugArraySize()
        {
            if (debugChampionIds != null && debugChampionIds.Length == 3)
            {
                return;
            }

            var previous = debugChampionIds ?? Array.Empty<string>();
            debugChampionIds = new string[3];
            for (var i = 0; i < debugChampionIds.Length && i < previous.Length; i++)
            {
                debugChampionIds[i] = previous[i];
            }
        }
    }
}
