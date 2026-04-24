using System;
using ProjectZ.Combat;
using ProjectZ.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class SpellShopView : MonoBehaviour
    {
        [SerializeField] private int offerIndex;
        [SerializeField] private string spellIdOverride;
        [SerializeField] private SpellEffectView spellView;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text fallbackText;
        [SerializeField] private Button button;
        [SerializeField] private GameObject boughtRoot;
        [SerializeField] private GameObject priceTagRoot;
        [SerializeField] private Button priceTagButton;

        private UnityAction _clickAction;
        private string _spellId;

        public string SpellId => _spellId;

        private void Awake()
        {
            CacheReferences();
            SetOverlayDefaults();
        }

        private void OnValidate()
        {
            CacheReferences();
            SetOverlayDefaults();
        }

        public void SetOfferIndex(int index)
        {
            offerIndex = Mathf.Max(0, index);
        }

        public void BindFromCurrentShop(GameFlowManager manager, Action<string> onClick)
        {
            CacheReferences();
            ClearClick();

            var spellId = ResolveSpellId(manager);
            if (string.IsNullOrWhiteSpace(spellId))
            {
                SetVisible(false);
                return;
            }

            var spell = ResolveSpell(manager, spellId);
            if (spell == null)
            {
                SetVisible(false);
                return;
            }

            _spellId = spellId;
            SetVisible(true);

            var isBought = manager != null && manager.WasSpellBoughtFromShop(spellId);

            if (spellView != null)
            {
                spellView.SetSpell(spell);
                spellView.SetNewSpellVisible(manager != null && !isBought && !manager.IsSpellInRunInventory(spellId));
            }

            var price = manager != null ? manager.GetSpellPrice(spellId) : ResolvePreviewPrice(spell);
            if (priceText != null)
            {
                priceText.text = price.ToString();
            }

            if (boughtRoot != null)
            {
                boughtRoot.SetActive(isBought);
            }

            if (priceTagRoot != null)
            {
                priceTagRoot.SetActive(!isBought);
            }

            if (fallbackText != null)
            {
                fallbackText.text = spell.DisplayName;
            }

            var isInteractable = !isBought;
            BindInteractiveButton(button, isInteractable, onClick);
            BindInteractiveButton(priceTagButton, isInteractable, onClick);
        }

        public void Clear()
        {
            CacheReferences();
            ClearClick();
            _spellId = string.Empty;

            if (spellView != null)
            {
                spellView.SetSpell(null);
                spellView.SetNewSpellVisible(false);
            }

            if (priceText != null)
            {
                priceText.text = string.Empty;
            }

            if (boughtRoot != null)
            {
                boughtRoot.SetActive(false);
            }

            if (priceTagRoot != null)
            {
                priceTagRoot.SetActive(true);
            }

            if (fallbackText != null)
            {
                fallbackText.text = string.Empty;
            }

            if (button != null)
            {
                button.interactable = true;
            }

            if (priceTagButton != null)
            {
                priceTagButton.interactable = true;
            }
        }

        private string ResolveSpellId(GameFlowManager manager)
        {
            if (!string.IsNullOrWhiteSpace(spellIdOverride))
            {
                return spellIdOverride;
            }

            if (manager == null)
            {
                return string.Empty;
            }

            var offers = manager.GetCurrentShopOffers();
            return offerIndex >= 0 && offerIndex < offers.Count ? offers[offerIndex] : string.Empty;
        }

        private static CombatSpellAsset ResolveSpell(GameFlowManager manager, string spellId)
        {
            if (manager != null && manager.TryGetSpellAsset(spellId, out var runtimeSpell))
            {
                return runtimeSpell;
            }

            var registry = RuntimeAssetRegistryAsset.Load();
            var library = registry != null ? registry.SpellLibrary : null;
            return library != null && library.TryGetSpellById(spellId, out var librarySpell)
                ? librarySpell
                : null;
        }

        private static int ResolvePreviewPrice(CombatSpellAsset spell)
        {
            return spell == null ? 0 : Mathf.Clamp(10 + spell.Value * 2 + spell.CostEntriesCount * 4, 14, 65);
        }

        private void CacheReferences()
        {
            if (spellView == null)
            {
                spellView = GetComponentInChildren<SpellEffectView>(true);
            }

            if (priceText == null)
            {
                priceText = FindText("PriceAmmount") ?? FindText("PriceAmount") ?? FindText("Price");
            }

            if (fallbackText == null)
            {
                fallbackText = GetComponentInChildren<TMP_Text>(true);
            }

            if (button == null)
            {
                button = GetComponentInChildren<Button>(true);
            }

            if (boughtRoot == null)
            {
                var bought = transform.Find("Bought");
                if (bought != null)
                {
                    boughtRoot = bought.gameObject;
                }
            }

            if (priceTagRoot == null)
            {
                var priceTag = transform.Find("PriceTag");
                if (priceTag != null)
                {
                    priceTagRoot = priceTag.gameObject;
                }
            }

            if (priceTagButton == null && priceTagRoot != null)
            {
                priceTagButton = priceTagRoot.GetComponent<Button>();
                if (priceTagButton == null)
                {
                    priceTagButton = priceTagRoot.AddComponent<Button>();
                }
            }
        }

        private TMP_Text FindText(string targetName)
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == targetName)
                {
                    return texts[i];
                }
            }

            return null;
        }

        private void ClearClick()
        {
            if (button != null && _clickAction != null)
            {
                button.onClick.RemoveListener(_clickAction);
            }

            if (priceTagButton != null && _clickAction != null)
            {
                priceTagButton.onClick.RemoveListener(_clickAction);
            }

            _clickAction = null;
        }

        private void BindInteractiveButton(Button targetButton, bool isInteractable, Action<string> onClick)
        {
            if (targetButton == null)
            {
                return;
            }

            targetButton.interactable = isInteractable;
            if (!isInteractable || onClick == null)
            {
                return;
            }

            _clickAction ??= () => onClick(_spellId);
            targetButton.onClick.AddListener(_clickAction);
        }

        private void SetOverlayDefaults()
        {
            if (boughtRoot != null)
            {
                boughtRoot.SetActive(false);
            }
        }

        private void SetVisible(bool isVisible)
        {
            gameObject.SetActive(isVisible);
        }
    }
}
