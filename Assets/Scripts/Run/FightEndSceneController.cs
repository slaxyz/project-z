using System.Collections.Generic;
using ProjectZ.Core;
using ProjectZ.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectZ.Run
{
    public class FightEndSceneController : MonoBehaviour
    {
        private readonly List<RewardSlot> _rewardSlots = new List<RewardSlot>(3);
        private readonly List<TMP_Text> _refreshCountTexts = new List<TMP_Text>(1);
        private RectTransform _winScreenRoot;
        private RectTransform _loseScreenRoot;
        private TMP_Text _monsterMainText;
        private TMP_Text _monsterShadowText;
        private TMP_Text _runNumberText;
        private Button _retryButton;
        private Button _exitButton;
        private UnityAction _retryAction;
        private UnityAction _exitAction;
        private Button _refreshButton;
        private UnityAction _refreshAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnFightEndScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.FightEnd)
            {
                return;
            }

            if (FindFirstObjectByType<FightEndSceneController>() != null)
            {
                return;
            }

            var go = new GameObject("FightEndSceneController");
            go.AddComponent<FightEndSceneController>();
        }

        private void Awake()
        {
            BindScene();
        }

        private void OnEnable()
        {
            BindScene();
            RefreshView();
        }

        private void Update()
        {
            SyncRefreshButtonState();
        }

        public void RefreshView()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            UpdateResultScreens(manager);
            ApplyMonsterName(manager.LastFightEnemyDisplayName);
            ApplyRunNumber(manager);
            BindExitButtons(manager);
            BindRefreshButton(manager);
            BindRewardSlots(manager);
            SyncRefreshButtonState(manager);
        }

        public void RefreshRewards()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (manager.RefreshSpellRewardOffers())
            {
                RefreshView();
            }
        }

        private void BindScene()
        {
            CacheResultScreens();
            BindMonsterHeader();
            BindRunNumber();
            BindExitButtons(GameFlowManager.Instance);
            BindRewardSlots(GameFlowManager.Instance);
            BindRefreshButton(GameFlowManager.Instance);
        }

        private void CacheResultScreens()
        {
            _winScreenRoot = FindDeepChildByName("WinScreen") as RectTransform;
            _loseScreenRoot = FindDeepChildByName("LoseScreen") as RectTransform;
        }

        private void UpdateResultScreens(GameFlowManager manager)
        {
            CacheResultScreens();
            if (_winScreenRoot == null || _loseScreenRoot == null || manager == null)
            {
                return;
            }

            if (!manager.HasLastFightResult)
            {
                _winScreenRoot.gameObject.SetActive(false);
                _loseScreenRoot.gameObject.SetActive(false);
                return;
            }

            var isVictory = manager.LastFightWasVictory;
            _winScreenRoot.gameObject.SetActive(isVictory);
            _loseScreenRoot.gameObject.SetActive(!isVictory);
        }

        private void BindExitButtons(GameFlowManager manager)
        {
            BindSceneButton(ref _retryButton, ref _retryAction, "Retry", () =>
            {
                if (manager != null)
                {
                    manager.EndRun(0, GameScenes.TeamSelect);
                }
            });

            BindSceneButton(ref _exitButton, ref _exitAction, "Exit", () =>
            {
                if (manager != null)
                {
                    manager.EndRun(0, GameScenes.Home);
                }
            });
        }

        private void BindMonsterHeader()
        {
            var header = FindDeepChildByName("Header_Monster")
                ?? FindDeepChildByName("Header_Center")
                ?? FindDeepChildByName("Header_Defeated");
            if (header == null)
            {
                return;
            }

            _monsterMainText = FindTextChild(header, "Label_Main") ?? FindTextChild(header, "Monster");
            _monsterShadowText = FindTextChild(header, "Label_Shadow") ?? FindTextChild(header, "Monster");
        }

        private void BindRunNumber()
        {
            var loseRecap = FindDeepChildByName("LoseRecap");
            if (loseRecap == null)
            {
                _runNumberText = null;
                return;
            }

            _runNumberText = FindTextChild(loseRecap, "Run_Number");
        }

        private void ApplyRunNumber(GameFlowManager manager)
        {
            if (_runNumberText == null || manager == null)
            {
                return;
            }

            _runNumberText.text = manager.GetLifetimeRunLabel();
        }

        private void BindRewardSlots(GameFlowManager manager)
        {
            for (var i = 0; i < _rewardSlots.Count; i++)
            {
                _rewardSlots[i].Release();
            }
            _rewardSlots.Clear();

            var bonusSelection = FindDeepChildByName("BonusSelection");
            if (bonusSelection == null)
            {
                return;
            }

            for (var i = 0; i < bonusSelection.childCount; i++)
            {
                var slotRoot = bonusSelection.GetChild(i);
                if (slotRoot == null)
                {
                    continue;
                }

                _rewardSlots.Add(new RewardSlot(slotRoot));
            }

            if (manager == null)
            {
                return;
            }

            var offers = manager.GetPendingSpellRewardOffers();
            for (var i = 0; i < _rewardSlots.Count; i++)
            {
                var slot = _rewardSlots[i];
                var spellId = i < offers.Count ? offers[i] : string.Empty;
                slot.BindSpell(manager, spellId, i, OnRewardSlotClicked);
            }
        }

        private void BindRefreshButton(GameFlowManager manager)
        {
            if (_refreshButton != null && _refreshAction != null)
            {
                _refreshButton.onClick.RemoveListener(_refreshAction);
                _refreshAction = null;
            }

            _refreshButton = FindButtonByLabelOrName("Refresh");
            if (_refreshButton == null || manager == null)
            {
                return;
            }

            _refreshAction = RefreshRewards;
            _refreshButton.onClick.AddListener(_refreshAction);
            CacheRefreshCountTexts(_refreshButton.transform);
            SyncRefreshButtonState(manager);
        }

        private void BindSceneButton(ref Button button, ref UnityAction action, string targetName, System.Action onClick)
        {
            if (button != null && action != null)
            {
                button.onClick.RemoveListener(action);
            }

            button = FindButtonByLabelOrName(targetName);
            if (button == null || onClick == null)
            {
                action = null;
                return;
            }

            action = () => onClick();
            button.onClick.AddListener(action);
        }

        private void ApplyMonsterName(string monsterName)
        {
            var value = string.IsNullOrWhiteSpace(monsterName) ? "Monster" : monsterName;

            if (_monsterMainText != null)
            {
                _monsterMainText.text = value;
            }

            if (_monsterShadowText != null)
            {
                _monsterShadowText.text = value;
            }
        }

        private void OnRewardSlotClicked(string spellId)
        {
            var manager = GameFlowManager.Instance;
            if (manager == null || string.IsNullOrWhiteSpace(spellId))
            {
                return;
            }

            if (manager.TrySelectSpellReward(spellId, out _))
            {
                manager.NextBoardNode();
            }
        }

        private void SyncRefreshButtonState()
        {
            SyncRefreshButtonState(GameFlowManager.Instance);
        }

        private void SyncRefreshButtonState(GameFlowManager manager)
        {
            if (_refreshButton == null || manager == null)
            {
                return;
            }

            var remaining = Mathf.Max(0, manager.SpellRewardRefreshesRemaining);
            _refreshButton.interactable = manager.CanRefreshSpellRewardOffers();

            var countText = manager.HasPendingSpellRewardChoice() ? remaining.ToString() : string.Empty;
            for (var i = 0; i < _refreshCountTexts.Count; i++)
            {
                var text = _refreshCountTexts[i];
                if (text != null)
                {
                    text.text = countText;
                }
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
            if (numberRoot != null)
            {
                _refreshCountTexts.AddRange(numberRoot.GetComponentsInChildren<TMP_Text>(true));
                if (_refreshCountTexts.Count > 0)
                {
                    return;
                }
            }

            var createdText = CreateRuntimeRefreshCountText(refreshRoot);
            if (createdText != null)
            {
                _refreshCountTexts.Add(createdText);
            }
        }

        private static TMP_Text CreateRuntimeRefreshCountText(Transform refreshRoot)
        {
            if (refreshRoot == null)
            {
                return null;
            }

            var numberGo = new GameObject("Number", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            numberGo.transform.SetParent(refreshRoot, false);
            numberGo.transform.SetAsLastSibling();

            var rect = numberGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(10f, 21f);
            rect.sizeDelta = new Vector2(40f, 30f);

            var text = numberGo.GetComponent<TextMeshProUGUI>();
            var sourceText = refreshRoot.GetComponentInChildren<TMP_Text>(true);
            if (sourceText != null)
            {
                text.font = sourceText.font;
                text.fontSharedMaterial = sourceText.fontSharedMaterial;
                text.fontSize = Mathf.Max(16f, sourceText.fontSize);
                text.color = sourceText.color;
            }

            text.text = "1";
            text.fontSize = 24f;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            return text;
        }

        private static Transform FindDeepChildByName(string targetName)
        {
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == targetName)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static TMP_Text FindTextChild(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == targetName)
                {
                    return texts[i];
                }
            }

            return null;
        }

        private static Button FindButtonByLabelOrName(string targetName)
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                if (button.name == targetName)
                {
                    return button;
                }

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null && label.text == targetName)
                {
                    return button;
                }
            }

            return null;
        }

        private sealed class RewardSlot
        {
            private readonly Transform _root;
            private readonly Button _button;
            private readonly SpellEffectView _spellView;
            private readonly TMP_Text _fallbackText;
            private UnityAction _clickAction;

            public RewardSlot(Transform root)
            {
                _root = root;
                _button = root != null ? root.GetComponentInChildren<Button>(true) : null;
                _spellView = root != null ? root.GetComponentInChildren<SpellEffectView>(true) : null;
                _fallbackText = _spellView == null && root != null ? root.GetComponentInChildren<TMP_Text>(true) : null;
            }

            public void Release()
            {
                Clear();
            }

            public void BindSpell(GameFlowManager manager, string spellId, int index, System.Action<string> onClick)
            {
                if (_button != null && _clickAction != null)
                {
                    _button.onClick.RemoveListener(_clickAction);
                }

                if (string.IsNullOrWhiteSpace(spellId))
                {
                    Clear();
                    return;
                }

                if (_spellView != null && manager != null && manager.TryGetSpellAsset(spellId, out var spell))
                {
                    _spellView.SetSpell(spell);
                    _spellView.SetNewSpellVisible(IsNewSpellForThisRun(manager, spellId));
                }
                else if (_fallbackText != null && manager != null)
                {
                    if (_spellView != null)
                    {
                        _spellView.SetSpell(null);
                        _spellView.SetNewSpellVisible(false);
                    }

                    _fallbackText.text = manager.GetSpellDisplayName(spellId);
                }

                if (_button != null && onClick != null)
                {
                    var capturedSpellId = spellId;
                    _clickAction = () => onClick(capturedSpellId);
                    _button.onClick.AddListener(_clickAction);
                }

                if (_root != null)
                {
                    _root.gameObject.SetActive(true);
                }
            }

            private void Clear()
            {
                if (_button != null && _clickAction != null)
                {
                    _button.onClick.RemoveListener(_clickAction);
                }

                _clickAction = null;

                if (_spellView != null)
                {
                    _spellView.SetSpell(null);
                    _spellView.SetNewSpellVisible(false);
                }

                if (_fallbackText != null)
                {
                    _fallbackText.text = string.Empty;
                }

                if (_root != null)
                {
                    _root.gameObject.SetActive(true);
                }
            }

            private static bool IsNewSpellForThisRun(GameFlowManager manager, string spellId)
            {
                if (manager == null || string.IsNullOrWhiteSpace(spellId) || manager.CurrentRun == null)
                {
                    return false;
                }

                return !manager.IsSpellInRunInventory(spellId);
            }
        }
    }
}
