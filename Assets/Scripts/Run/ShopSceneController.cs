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
    public class ShopSceneController : MonoBehaviour
    {
        private const int RequiredShopSlotCount = 6;
        private const string RuntimeSlotPrefix = "Runtime_SpellShop_";
        private const string RuntimeEmptySlotPrefix = "Runtime_SpellShopEmpty_";
        private readonly List<SpellShopView> _spellShopViews = new List<SpellShopView>(3);
        private readonly List<GameObject> _runtimeEmptySlots = new List<GameObject>(6);
        private readonly List<(Button button, UnityAction action)> _boundButtons = new List<(Button, UnityAction)>();
        private string _feedback;
        private Vector2 _scroll;
        private int _lastOfferCount = -1;
        private Transform _shopSlotsRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnShopScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.Shop)
            {
                return;
            }

            if (FindFirstObjectByType<ShopSceneController>() != null)
            {
                return;
            }

            var go = new GameObject("ShopSceneController");
            go.AddComponent<ShopSceneController>();
        }

        private void OnEnable()
        {
            BindScene();
            RefreshView();
        }

        private void OnDisable()
        {
            ReleaseButtons();
            ClearRuntimeEmptySlots();
            _lastOfferCount = -1;
        }

        private void Update()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null || manager.CurrentState != GameFlowState.Shop)
            {
                return;
            }

            var offerCount = manager.GetCurrentShopOffers().Count;
            if (offerCount != _lastOfferCount)
            {
                RefreshView();
            }
        }

        private void BindScene()
        {
            ReleaseButtons();
            BindShopSlots();
            BindExitButton();
        }

        private void RefreshView()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (_spellShopViews.Count < RequiredShopSlotCount)
            {
                BindShopSlots();
            }

            var offers = manager.GetCurrentShopOffers();
            var foregroundStartIndex = GetForegroundStartIndex();
            _lastOfferCount = offers.Count;
            for (var i = 0; i < _spellShopViews.Count; i++)
            {
                var view = _spellShopViews[i];
                if (view == null)
                {
                    continue;
                }

                view.SetOfferIndex(i);
                view.transform.SetSiblingIndex(foregroundStartIndex + i);
                if (i < offers.Count)
                {
                    view.BindFromCurrentShop(manager, OnOfferClicked);
                }
                else
                {
                    view.Clear();
                    view.gameObject.SetActive(false);
                }
            }

            RenderEmptySlots(offers.Count, foregroundStartIndex);
            KeepBackgroundBehind();
        }

        private void BindShopSlots()
        {
            _spellShopViews.Clear();
            ClearRuntimeEmptySlots();

            var root = FindDeepChildByName("BonusSelection");
            if (root == null)
            {
                return;
            }

            _shopSlotsRoot = root;
            EnsureShopSlotCount(root);

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null || !IsShopSlotTransform(child))
                {
                    continue;
                }

                var view = child.GetComponent<SpellShopView>();
                if (view == null)
                {
                    view = child.gameObject.AddComponent<SpellShopView>();
                }

                view.SetOfferIndex(_spellShopViews.Count);
                _spellShopViews.Add(view);
            }
        }

        private static void EnsureShopSlotCount(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var template = FindShopSlotTemplate(root);
            if (template == null)
            {
                return;
            }

            while (CountShopSlotChildren(root) < RequiredShopSlotCount)
            {
                var clone = Instantiate(template.gameObject, root, false);
                clone.name = RuntimeSlotPrefix + CountShopSlotChildren(root);
                clone.SetActive(true);
            }
        }

        private static int CountShopSlotChildren(Transform root)
        {
            var count = 0;
            for (var i = 0; i < root.childCount; i++)
            {
                if (IsShopSlotTransform(root.GetChild(i)))
                {
                    count++;
                }
            }

            return count;
        }

        private static Transform FindShopSlotTemplate(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (IsShopSlotTransform(child))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsShopSlotTransform(Transform child)
        {
            if (child == null)
            {
                return false;
            }

            if (child.name.StartsWith("SpellShopEmpty", System.StringComparison.OrdinalIgnoreCase)
                || child.name.StartsWith(RuntimeEmptySlotPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return child.GetComponentInChildren<SpellEffectView>(true) != null;
        }

        private void RenderEmptySlots(int offersCount, int foregroundStartIndex)
        {
            ClearRuntimeEmptySlots();

            if (_shopSlotsRoot == null)
            {
                return;
            }

            var spellCount = Mathf.Clamp(offersCount, 0, RequiredShopSlotCount);
            var emptyCount = RequiredShopSlotCount - spellCount;
            if (emptyCount <= 0)
            {
                return;
            }

            var emptyTemplate = FindEmptySlotTemplate();
            if (emptyTemplate == null)
            {
                return;
            }

            for (var i = 0; i < emptyCount; i++)
            {
                var clone = Instantiate(emptyTemplate, _shopSlotsRoot, false);
                clone.name = RuntimeEmptySlotPrefix + i;
                clone.SetActive(true);
                SetNonInteractable(clone);
                clone.transform.SetSiblingIndex(foregroundStartIndex + spellCount + i);
                _runtimeEmptySlots.Add(clone);
            }
        }

        private int GetForegroundStartIndex()
        {
            return FindShopContentBackground() != null ? 1 : 0;
        }

        private void KeepBackgroundBehind()
        {
            var background = FindShopContentBackground();
            if (background != null)
            {
                background.SetAsFirstSibling();
            }
        }

        private Transform FindShopContentBackground()
        {
            if (_shopSlotsRoot == null)
            {
                return null;
            }

            for (var i = 0; i < _shopSlotsRoot.childCount; i++)
            {
                var child = _shopSlotsRoot.GetChild(i);
                if (child != null && string.Equals(child.name, "ShopContentBG", System.StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private GameObject FindEmptySlotTemplate()
        {
            if (_shopSlotsRoot == null)
            {
                return null;
            }

            for (var i = 0; i < _shopSlotsRoot.childCount; i++)
            {
                var child = _shopSlotsRoot.GetChild(i);
                if (child != null && child.name.StartsWith("SpellShopEmpty", System.StringComparison.OrdinalIgnoreCase))
                {
                    return child.gameObject;
                }
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SpellShopEmpty.prefab");
#else
            return null;
#endif
        }

        private void ClearRuntimeEmptySlots()
        {
            for (var i = 0; i < _runtimeEmptySlots.Count; i++)
            {
                var emptySlot = _runtimeEmptySlots[i];
                if (emptySlot != null)
                {
                    Destroy(emptySlot);
                }
            }

            _runtimeEmptySlots.Clear();
        }

        private static void SetNonInteractable(GameObject slot)
        {
            if (slot == null)
            {
                return;
            }

            var buttons = slot.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i].interactable = false;
                buttons[i].enabled = false;
            }
        }

        private void BindExitButton()
        {
            var exitButton = FindButtonByNameOrLabel("exit") ?? FindButtonByNameOrLabel("Exit") ?? FindButtonByNameOrLabel("Skip");
            if (exitButton == null)
            {
                return;
            }

            UnityAction action = SkipShop;
            exitButton.onClick.AddListener(action);
            _boundButtons.Add((exitButton, action));
        }

        private void OnOfferClicked(string spellId)
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (manager.TrySelectShopSpell(spellId, out _feedback))
            {
                RefreshView();
            }
        }

        private void SkipShop()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            manager.SkipShop();
        }

        private void OnGUI()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null || manager.CurrentState != GameFlowState.Shop)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var scale = DebugGuiScale.GetScale();
            var safeArea = DebugGuiScale.GetSafeArea(scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            try
            {
                var width = 420f;
                var height = 500f;
                var x = safeArea.x + safeArea.width - width - 18f;
                var y = safeArea.y + 18f;

                GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
                _scroll = GUILayout.BeginScrollView(_scroll);
                GUILayout.Label("Shop");
                GUILayout.Label("Run coins: " + manager.CurrentRun.coinsGained);
                GUILayout.Space(8f);

                if (manager.IsWaitingSpellReplacementChoice() && manager.IsPendingReplacementFromShop())
                {
                    DrawReplacementFlow(manager);
                }
                else
                {
                    DrawOfferFlow(manager);
                }

                if (!string.IsNullOrWhiteSpace(_feedback))
                {
                    GUILayout.Space(8f);
                    GUILayout.Label(_feedback);
                }

                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private void DrawOfferFlow(GameFlowManager manager)
        {
            GUILayout.Label("Choose a spell or skip.");
            foreach (var spellId in manager.GetCurrentShopOffers())
            {
                var isBought = manager.WasSpellBoughtFromShop(spellId);
                var label = isBought
                    ? manager.GetSpellDisplayName(spellId) + " - Bought"
                    : manager.GetSpellDisplayName(spellId) + " - " + manager.GetSpellPrice(spellId) + " coins";

                GUI.enabled = !isBought;
                if (GUILayout.Button(label, GUILayout.Height(34f)))
                {
                    OnOfferClicked(spellId);
                }
                GUI.enabled = true;
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("Skip Shop", GUILayout.Height(34f)))
            {
                manager.SkipShop();
            }
        }

        private void DrawReplacementFlow(GameFlowManager manager)
        {
            var incomingSpellId = manager.GetPendingIncomingSpellId();
            GUILayout.Label("Bought: " + manager.GetSpellDisplayName(incomingSpellId));

            if (string.IsNullOrWhiteSpace(manager.GetPendingReplacementChampionId()))
            {
                GUILayout.Label("Choose champion.");
                foreach (var championId in manager.GetSelectedChampionIdsForRun())
                {
                    if (GUILayout.Button(manager.GetChampionDisplayName(championId), GUILayout.Height(32f)))
                    {
                        manager.TrySelectReplacementChampion(championId, out _feedback);
                    }
                }

                return;
            }

            var selectedChampionId = manager.GetPendingReplacementChampionId();
            GUILayout.Label("Replace on " + manager.GetChampionDisplayName(selectedChampionId));
            foreach (var existingSpellId in manager.GetChampionSpellLoadout(selectedChampionId))
            {
                if (GUILayout.Button(manager.GetSpellDisplayName(existingSpellId), GUILayout.Height(32f)))
                {
                    manager.TryReplaceRunSpell(existingSpellId, out _feedback);
                }
            }

            if (GUILayout.Button("Choose Another Champion", GUILayout.Height(32f)))
            {
                manager.CancelPendingReplacementChampion();
            }
        }

        private void ReleaseButtons()
        {
            for (var i = 0; i < _boundButtons.Count; i++)
            {
                var entry = _boundButtons[i];
                if (entry.button != null && entry.action != null)
                {
                    entry.button.onClick.RemoveListener(entry.action);
                }
            }

            _boundButtons.Clear();
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

        private static Button FindButtonByNameOrLabel(string targetName)
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
                if (label != null && string.Equals(label.text, targetName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }
            }

            return null;
        }

    }
}
