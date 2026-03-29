using System.Collections.Generic;
using ProjectZ.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class SpellEffectView : MonoBehaviour
    {
        private static readonly Dictionary<int, Sprite> BackgroundCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> IconCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<string, Sprite> RuneBackgroundCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> StatBadgeCache = new Dictionary<string, Sprite>();

        [Header("Data")]
        [SerializeField] private CombatSpellAsset spell;
        [SerializeField] private List<SpellEffectLineDefinition> previewLines = new List<SpellEffectLineDefinition>();

        [Header("Badges")]
        [SerializeField] private GameObject newSpellRoot;

        [Header("Layout")]
        [SerializeField] private RectTransform linesRoot;
        [SerializeField] private float lineSpacing = 8f;
        [SerializeField] private float tokenSpacing = 8f;
        [SerializeField] private float badgeSize = 48f;
        [SerializeField] private float badgeIconSize = 28f;

        [Header("Colors")]
        [SerializeField] private Color labelColor = new Color(0.05f, 0.05f, 0.08f, 1f);
        [SerializeField] private Color valueColor = new Color(0.95f, 0.38f, 0.07f, 1f);
        [SerializeField] private Color specialColor = new Color(0.95f, 0.38f, 0.07f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        [Header("Typography")]
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private float labelFontSize = 34f;
        [SerializeField] private float valueFontSize = 34f;

        [Header("Visual")]
        [SerializeField] private int freezeNeutralVisualId = 5;

        private bool _refreshing;

        private void Awake()
        {
            CacheNewSpellRoot();
            EnsureNewSpellLabelText();
            SetNewSpellVisible(false);
            EnsureRoot();
        }

        private void OnValidate()
        {
            CacheNewSpellRoot();
            EnsureNewSpellLabelText();
            if (newSpellRoot != null)
            {
                newSpellRoot.SetActive(false);
            }

            if (!Application.isPlaying)
            {
                RefreshView();
            }
        }

        public void SetSpell(CombatSpellAsset spellAsset)
        {
            spell = spellAsset;
            if (spellAsset == null)
            {
                SetNewSpellVisible(false);
            }

            RefreshView();
        }

        public void SetNewSpellVisible(bool isVisible)
        {
            CacheNewSpellRoot();
            if (newSpellRoot != null)
            {
                newSpellRoot.SetActive(isVisible);
            }
        }

        public void SetPreviewLines(List<SpellEffectLineDefinition> lines)
        {
            previewLines.Clear();
            if (lines != null)
            {
                previewLines.AddRange(lines);
            }

            RefreshView();
        }

        public void RefreshView()
        {
            if (_refreshing)
            {
                return;
            }

            _refreshing = true;
            try
            {
                var root = EnsureRoot();
                if (root == null)
                {
                    return;
                }

                var sourceLines = ResolveSourceLines();
                if (sourceLines == null || sourceLines.Count == 0)
                {
                    ApplyLegacyLine(root);
                    RefreshRuneCosts();
                    return;
                }

                EnsureRowCount(root, sourceLines.Count);
                var rows = CollectRows(root);

                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row == null)
                    {
                        continue;
                    }

                    if (i >= sourceLines.Count)
                    {
                        row.gameObject.SetActive(false);
                        continue;
                    }

                    row.gameObject.SetActive(true);
                    ApplyLineToRow(row, sourceLines[i]);
                }

                RefreshRuneCosts();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private IReadOnlyList<SpellEffectLineDefinition> ResolveSourceLines()
        {
            if (spell != null && spell.EffectLines != null && spell.EffectLines.Count > 0)
            {
                return spell.EffectLines;
            }

            if (previewLines != null && previewLines.Count > 0)
            {
                return previewLines;
            }

            return null;
        }

        private RectTransform EnsureRoot()
        {
            if (linesRoot != null)
            {
                SetupRootLayout(linesRoot);
                return linesRoot;
            }

            var existingRoot = transform.Find("Effect_Lines");
            if (existingRoot is RectTransform existingRect)
            {
                linesRoot = existingRect;
                SetupRootLayout(linesRoot);
                return linesRoot;
            }

            var rootGo = new GameObject("Effect_Lines", typeof(RectTransform));
            rootGo.transform.SetParent(transform, false);
            linesRoot = rootGo.GetComponent<RectTransform>();
            SetupRootLayout(linesRoot);
            return linesRoot;
        }

        private void CacheNewSpellRoot()
        {
            if (newSpellRoot != null)
            {
                return;
            }

            var badgeRoot = FindNewSpellRootInParents();
            if (badgeRoot != null)
            {
                newSpellRoot = badgeRoot.gameObject;
            }
        }

        private void EnsureNewSpellLabelText()
        {
            if (newSpellRoot == null)
            {
                return;
            }

            var texts = newSpellRoot.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text != null && text.gameObject.name == "NewSpell_Name")
                {
                    text.text = "NEW SPELL";
                    break;
                }
            }
        }

        private Transform FindNewSpellRootInParents()
        {
            var current = transform;
            while (current != null)
            {
                var badgeRoot = current.Find("NewSpell");
                if (badgeRoot != null)
                {
                    return badgeRoot;
                }

                current = current.parent;
            }

            return null;
        }

        private void RefreshRuneCosts()
        {
            var runesRoot = EnsureRunesRoot();
            if (runesRoot == null)
            {
                return;
            }

            var runeSequence = ResolveRuneCostSequence();
            EnsureRuneSlotCount(runesRoot, runeSequence.Count);

            var runeSlots = CollectRuneSlots(runesRoot);
            for (var i = 0; i < runeSlots.Count; i++)
            {
                var slot = runeSlots[i];
                if (slot == null)
                {
                    continue;
                }

                var isActive = i < runeSequence.Count;
                slot.gameObject.SetActive(isActive);
                if (!isActive)
                {
                    continue;
                }

                var background = FindImage(slot, "BG");
                if (background == null)
                {
                    background = slot.GetComponentInChildren<Image>(true);
                }

                if (background != null)
                {
                    var element = runeSequence[i];
                    background.sprite = LoadRuneBackgroundSprite(element);
                    background.color = background.sprite != null ? Color.white : Color.clear;
                    background.preserveAspect = true;
                }
            }
        }

        private RectTransform EnsureRunesRoot()
        {
            var current = transform != null ? transform.parent : null;
            while (current != null)
            {
                var existingRoot = current.Find("Runes_content");
                if (existingRoot is RectTransform existingRect)
                {
                    return existingRect;
                }

                current = current.parent;
            }

            return null;
        }

        private IReadOnlyList<ElementType> ResolveRuneCostSequence()
        {
            if (spell == null)
            {
                return new List<ElementType>();
            }

            return spell.BuildRuneCostSequence();
        }

        private void EnsureRuneSlotCount(RectTransform root, int desiredCount)
        {
            if (root == null || desiredCount <= 0)
            {
                return;
            }

            var slots = CollectRuneSlots(root);
            if (slots.Count == 0)
            {
                CreateRuntimeRuneSlot(root);
                slots = CollectRuneSlots(root);
            }

            while (slots.Count < desiredCount)
            {
                var template = slots[0];
                var clone = Instantiate(template.gameObject, root, false);
                clone.name = "Rune_slot_" + slots.Count;
                clone.SetActive(true);
                slots = CollectRuneSlots(root);
            }
        }

        private static List<RectTransform> CollectRuneSlots(Transform root)
        {
            var slots = new List<RectTransform>();
            if (root == null)
            {
                return slots;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                if (child.name.StartsWith("Rune_slot"))
                {
                    slots.Add(child);
                }
            }

            return slots;
        }

        private static RectTransform CreateRuntimeRuneSlot(RectTransform root)
        {
            if (root == null)
            {
                return null;
            }

            var slotGo = new GameObject("Rune_slot_0", typeof(RectTransform), typeof(CanvasRenderer));
            slotGo.transform.SetParent(root, false);

            var slotRect = slotGo.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(20f, 20f);

            var bgGo = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(slotGo.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            StretchRect(bgRect);

            return slotRect;
        }

        private static Sprite LoadRuneBackgroundSprite(ElementType element)
        {
            var spriteName = ResolveRuneSpriteName(element);
            if (RuneBackgroundCache.TryGetValue(spriteName, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = Resources.Load<Sprite>("Art/UI/FightScene/Runes/" + spriteName);
            RuneBackgroundCache[spriteName] = sprite;
            return sprite;
        }

        private static string ResolveRuneSpriteName(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:
                    return "Fire";
                case ElementType.Water:
                    return "Water";
                case ElementType.Nature:
                    return "Nature";
                case ElementType.Poison:
                    return "Poison";
                case ElementType.Mystic:
                    return "Mystic";
                case ElementType.Ground:
                    return "Normal";
                default:
                    return "Normal";
            }
        }

        private void SetupRootLayout(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            var verticalLayout = root.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout == null)
            {
                verticalLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            verticalLayout.spacing = lineSpacing;
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = false;
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.childScaleWidth = false;
            verticalLayout.childScaleHeight = false;

            var fitter = root.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void EnsureRowCount(RectTransform root, int desiredCount)
        {
            var rows = CollectRows(root);
            if (rows.Count == 0)
            {
                CreateRuntimeRow(root);
                rows = CollectRows(root);
            }

            while (rows.Count < desiredCount)
            {
                var template = rows[0];
                var clone = Instantiate(template.gameObject, root, false);
                clone.name = "EffectLine_" + rows.Count;
                clone.SetActive(true);
                rows = CollectRows(root);
            }
        }

        private static List<RectTransform> CollectRows(Transform root)
        {
            var rows = new List<RectTransform>();
            if (root == null)
            {
                return rows;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                if (child.name.StartsWith("EffectLine_"))
                {
                    rows.Add(child);
                }
            }

            return rows;
        }

        private void ApplyLegacyLine(RectTransform root)
        {
            var rows = CollectRows(root);
            if (rows.Count == 0)
            {
                CreateRuntimeRow(root);
                rows = CollectRows(root);
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    continue;
                }

                row.gameObject.SetActive(i == 0);
            }

            if (rows.Count == 0)
            {
                return;
            }

            var rowRoot = rows[0];
            var effectType = spell != null ? spell.EffectType : CardEffectType.Damage;
            var label = ResolveLegacyLabel(effectType);
            var amount = spell != null ? spell.Value : 0;
            ApplyLabelAndAmount(rowRoot, label, "(" + amount + ")", false, 1, valueColor, false, 0);
        }

        private RectTransform CreateRuntimeRow(RectTransform root)
        {
            var rowGo = new GameObject("EffectLine_0", typeof(RectTransform));
            rowGo.transform.SetParent(root, false);

            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = tokenSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = rowGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildRuntimeLabel(rowGo.transform, "Text_Label", labelColor, labelFontSize);
            BuildRuntimeBadge(rowGo.transform);
            BuildRuntimeStatBadge(rowGo.transform);
            BuildRuntimeAmount(rowGo.transform, "Text_Value", valueColor, valueFontSize);

            return rowGo.GetComponent<RectTransform>();
        }

        private void ApplyLineToRow(RectTransform row, SpellEffectLineDefinition line)
        {
            if (row == null || line == null)
            {
                return;
            }

            var label = FindText(row, "Text_Label");
            var amount = FindText(row, "Text_Value");
            var badge = FindChild(row, "ElementBadge");
            var statBadge = FindChild(row, "ElementBadgeStat");
            var isElementLine = line.UsesElement;
            var isSupportLine = IsSupportLine(line);
            var isTrailing = line.UsesTrailingElement;
            var visualId = line.UsesFixedNeutralElement ? freezeNeutralVisualId : ResolveVisualId(line.element);
            var amountColor = ResolveAmountColor(line);

            if (label != null)
            {
                label.text = line.BuildLabel();
                label.color = labelColor;
                SetupTextStyle(label, labelFontSize);
            }

            if (amount != null)
            {
                amount.text = line.BuildAmountText();
                amount.color = amountColor;
                SetupTextStyle(amount, valueFontSize);
            }

            if (badge != null)
            {
                badge.gameObject.SetActive(isElementLine);
                if (isElementLine)
                {
                    var background = FindImage(badge, "BG");
                    if (background != null)
                    {
                        background.sprite = LoadBackgroundSprite(visualId);
                        background.color = background.sprite != null ? Color.white : Color.clear;
                        background.preserveAspect = true;
                    }

                    var icon = FindImage(badge, "Icon");
                    if (icon != null)
                    {
                        icon.sprite = LoadIconSprite(visualId);
                        icon.color = icon.sprite != null ? Color.white : Color.clear;
                        icon.preserveAspect = true;

                        var iconRect = icon.rectTransform;
                        if (iconRect != null)
                        {
                            iconRect.sizeDelta = new Vector2(badgeIconSize, badgeIconSize);
                        }
                    }

                    var layout = badge.GetComponent<LayoutElement>();
                    if (layout == null)
                    {
                        layout = badge.gameObject.AddComponent<LayoutElement>();
                    }

                    layout.preferredWidth = badgeSize;
                    layout.preferredHeight = badgeSize;
                    layout.minWidth = badgeSize;
                    layout.minHeight = badgeSize;
                    layout.flexibleWidth = 0f;
                    layout.flexibleHeight = 0f;

                    if (isTrailing)
                    {
                        badge.SetAsLastSibling();
                    }
                    else
                    {
                        var childIndex = Mathf.Min(1, row.childCount - 1);
                        badge.SetSiblingIndex(childIndex);
                    }
                }
            }

            if (statBadge != null)
            {
                statBadge.gameObject.SetActive(isSupportLine);
                if (isSupportLine)
                {
                    var background = FindImage(statBadge, "BG");
                    if (background != null)
                    {
                        background.sprite = LoadStatBadgeSprite(line.kind);
                        background.color = background.sprite != null ? Color.white : Color.clear;
                        background.preserveAspect = true;
                    }

                    var layout = statBadge.GetComponent<LayoutElement>();
                    if (layout == null)
                    {
                        layout = statBadge.gameObject.AddComponent<LayoutElement>();
                    }

                    layout.preferredWidth = badgeSize;
                    layout.preferredHeight = badgeSize;
                    layout.minWidth = badgeSize;
                    layout.minHeight = badgeSize;
                    layout.flexibleWidth = 0f;
                    layout.flexibleHeight = 0f;
                }
            }
        }

        private void ApplyLabelAndAmount(
            RectTransform row,
            string labelText,
            string amountText,
            bool showBadge,
            int visualId,
            Color amountTextColor,
            bool trailingBadge,
            int badgeVisualId)
        {
            if (row == null)
            {
                return;
            }

            var label = FindText(row, "Text_Label");
            var amount = FindText(row, "Text_Value");
            var badge = FindChild(row, "ElementBadge");
            var statBadge = FindChild(row, "ElementBadgeStat");

            if (label != null)
            {
                label.text = labelText;
                label.color = labelColor;
                SetupTextStyle(label, labelFontSize);
            }

            if (amount != null)
            {
                amount.text = amountText;
                amount.color = amountTextColor;
                SetupTextStyle(amount, valueFontSize);
            }

            if (badge != null)
            {
                badge.gameObject.SetActive(showBadge);
                if (showBadge)
                {
                    var background = FindImage(badge, "BG");
                    if (background != null)
                    {
                        background.sprite = LoadBackgroundSprite(badgeVisualId);
                        background.color = background.sprite != null ? Color.white : Color.clear;
                        background.preserveAspect = true;
                    }

                    var icon = FindImage(badge, "Icon");
                    if (icon != null)
                    {
                        icon.sprite = LoadIconSprite(badgeVisualId);
                        icon.color = icon.sprite != null ? Color.white : Color.clear;
                        icon.preserveAspect = true;

                        var iconRect = icon.rectTransform;
                        if (iconRect != null)
                        {
                            iconRect.sizeDelta = new Vector2(badgeIconSize, badgeIconSize);
                        }
                    }

                    if (trailingBadge)
                    {
                        badge.SetAsLastSibling();
                    }
                }
            }

            if (statBadge != null)
            {
                statBadge.gameObject.SetActive(false);
            }
        }

        private static bool IsSupportLine(SpellEffectLineDefinition line)
        {
            return line != null && (line.kind == SpellEffectKind.Heal || line.kind == SpellEffectKind.Shield);
        }

        private static void BuildRuntimeLabel(Transform parent, string name, Color color, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.color = color;
            text.fontSize = fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = -1f;
            layout.preferredHeight = -1f;
        }

        private static void BuildRuntimeAmount(Transform parent, string name, Color color, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.color = color;
            text.fontSize = fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = -1f;
            layout.preferredHeight = -1f;
        }

        private static void BuildRuntimeBadge(Transform parent)
        {
            var go = new GameObject("ElementBadge", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 48f;
            layout.preferredHeight = 48f;
            layout.minWidth = 48f;
            layout.minHeight = 48f;

            var bgGo = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            StretchRect(bgRect);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(28f, 28f);
            iconRect.anchoredPosition = Vector2.zero;
        }

        private static void BuildRuntimeStatBadge(Transform parent)
        {
            var go = new GameObject("ElementBadgeStat", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.SetActive(false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 48f;
            layout.preferredHeight = 48f;
            layout.minWidth = 48f;
            layout.minHeight = 48f;

            var bgGo = new GameObject("BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            StretchRect(bgRect);
        }

        private static void StretchRect(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private TMP_Text FindText(Transform root, string childName)
        {
            var child = FindChild(root, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private Image FindImage(Transform root, string childName)
        {
            var child = FindChild(root, childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null && child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private void SetupTextStyle(TMP_Text text, float fontSize)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            if (fontAsset != null)
            {
                text.font = fontAsset;
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
        }

        private Color ResolveAmountColor(SpellEffectLineDefinition line)
        {
            if (line == null)
            {
                return valueColor;
            }

            if (line.kind == SpellEffectKind.Special)
            {
                return specialColor;
            }

            if (line.kind == SpellEffectKind.Freeze)
            {
                return neutralColor;
            }

            if (line.UsesElement)
            {
                return ResolveElementColor(line.element);
            }

            return valueColor;
        }

        private static string ResolveLegacyLabel(CardEffectType effectType)
        {
            switch (effectType)
            {
                case CardEffectType.Damage:
                    return "Deal";
                case CardEffectType.Shield:
                    return "Shield";
                case CardEffectType.Heal:
                    return "Heal";
                default:
                    return effectType.ToString();
            }
        }

        private static Color ResolveElementColor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:
                    return new Color(0.95f, 0.40f, 0.07f, 1f);
                case ElementType.Water:
                    return new Color(0.20f, 0.58f, 0.98f, 1f);
                case ElementType.Ground:
                    return new Color(0.62f, 0.43f, 0.28f, 1f);
                case ElementType.Mystic:
                    return new Color(0.60f, 0.35f, 0.96f, 1f);
                case ElementType.Nature:
                    return new Color(0.24f, 0.66f, 0.33f, 1f);
                case ElementType.Poison:
                    return new Color(0.62f, 0.28f, 0.78f, 1f);
                default:
                    return new Color(0.95f, 0.38f, 0.07f, 1f);
            }
        }

        private static int ResolveVisualId(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:
                    return 1;
                case ElementType.Nature:
                    return 2;
                case ElementType.Water:
                    return 3;
                case ElementType.Poison:
                    return 4;
                case ElementType.Ground:
                    return 5;
                case ElementType.Mystic:
                    return 6;
                default:
                    return 1;
            }
        }

        private static Sprite LoadBackgroundSprite(int visualId)
        {
            if (BackgroundCache.TryGetValue(visualId, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = Resources.Load<Sprite>("Art/UI/TypeBackgrounds/" + visualId);
            BackgroundCache[visualId] = sprite;
            return sprite;
        }

        private static Sprite LoadIconSprite(int visualId)
        {
            if (IconCache.TryGetValue(visualId, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = Resources.Load<Sprite>("Art/UI/TypeIcons/" + visualId);
            IconCache[visualId] = sprite;
            return sprite;
        }

        private static Sprite LoadStatBadgeSprite(SpellEffectKind kind)
        {
            var spriteName = kind == SpellEffectKind.Heal ? "icon_hp" : "icon_shield";
            if (StatBadgeCache.TryGetValue(spriteName, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = Resources.Load<Sprite>("Art/UI/SystemIcons/" + spriteName);
            StatBadgeCache[spriteName] = sprite;
            return sprite;
        }
    }
}
