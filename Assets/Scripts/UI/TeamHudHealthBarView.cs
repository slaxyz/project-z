using ProjectZ.Combat;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class TeamHudHealthBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform hpBarRoot;
        [SerializeField] private RectTransform pointTemplate;
        [SerializeField] private float pointSpacing = -4f;
        [SerializeField] private TMP_Text currentMainText;
        [SerializeField] private TMP_Text currentShadowText;
        [SerializeField] private TMP_Text maxMainText;
        [SerializeField] private TMP_Text maxShadowText;

        private readonly List<HpPointView> _points = new List<HpPointView>();
        private FightMockController _fight;
        private RectTransform _pointsContainer;
        private int _lastCurrentHp = -1;
        private int _lastMaxHp = -1;
        private int _lastShieldHp = -1;

        private void Awake()
        {
            AutoAssign();
            Sync();
        }

        private void OnEnable()
        {
            AutoAssign();
            Sync();
        }

        private void Update()
        {
            Sync();
        }

        private void OnValidate()
        {
            AutoAssign();
        }

        private void AutoAssign()
        {
            if (hpBarRoot == null)
            {
                hpBarRoot = FindRectTransformByName("HPBar");
            }

            if (pointTemplate == null && hpBarRoot != null)
            {
                var found = FindChildRectTransformByName(hpBarRoot, "HPBar_Point");
                if (found != null)
                {
                    pointTemplate = found;
                }
            }

            if (currentMainText == null)
            {
                currentMainText = FindText("HP-remaining", "Label_Main");
            }

            if (currentShadowText == null)
            {
                currentShadowText = FindText("HP-remaining", "Label_Shadow");
            }

            if (maxMainText == null)
            {
                maxMainText = FindText("HP-full", "Label_Main");
            }

            if (maxShadowText == null)
            {
                maxShadowText = FindText("HP-full", "Label_Shadow");
            }
        }

        private TMP_Text FindText(string groupName, string textName)
        {
            var group = FindRectTransformByName(groupName);
            if (group == null)
            {
                return null;
            }

            var labels = group.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].name == textName)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private void Sync()
        {
            if (_fight == null)
            {
                _fight = Object.FindFirstObjectByType<FightMockController>();
            }

            if (_fight == null || !_fight.TryGetTeamHealthState(out var currentHp, out var maxHp, out var shieldHp))
            {
                return;
            }

            var safeMaxHp = Mathf.Max(1, maxHp);
            shieldHp = Mathf.Clamp(shieldHp, 0, safeMaxHp);

            if (currentHp == _lastCurrentHp && safeMaxHp == _lastMaxHp && shieldHp == _lastShieldHp)
            {
                return;
            }

            ApplyText(currentMainText, currentHp.ToString());
            ApplyText(currentShadowText, currentHp.ToString());
            ApplyText(maxMainText, "/" + safeMaxHp);
            ApplyText(maxShadowText, "/" + safeMaxHp);

            EnsurePointViews(safeMaxHp);
            ApplyPointStates(currentHp, safeMaxHp, shieldHp);

            _lastCurrentHp = currentHp;
            _lastMaxHp = safeMaxHp;
            _lastShieldHp = shieldHp;
        }

        private static void ApplyText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private void EnsurePointViews(int maxHp)
        {
            if (hpBarRoot == null || pointTemplate == null || maxHp <= 0)
            {
                return;
            }

            EnsurePointsContainer();

            while (_points.Count < maxHp)
            {
                var pointRoot = _points.Count == 0
                    ? pointTemplate
                    : Instantiate(pointTemplate, _pointsContainer);

                pointRoot.gameObject.name = "HPBar_Point";
                pointRoot.gameObject.SetActive(true);
                NormalizePointRect(pointRoot);
                _points.Add(new HpPointView(pointRoot));
            }

            while (_points.Count > maxHp)
            {
                var lastIndex = _points.Count - 1;
                var point = _points[lastIndex];
                if (point != null && point.Root != null)
                {
                    Destroy(point.Root.gameObject);
                }

                _points.RemoveAt(lastIndex);
            }
        }

        private void EnsurePointsContainer()
        {
            if (_pointsContainer != null)
            {
                return;
            }

            _pointsContainer = pointTemplate.parent as RectTransform;
            if (_pointsContainer != null && _pointsContainer != hpBarRoot)
            {
                return;
            }

            var containerGo = new GameObject("HPBar_Points", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _pointsContainer = containerGo.GetComponent<RectTransform>();
            _pointsContainer.SetParent(hpBarRoot, false);
            _pointsContainer.SetSiblingIndex(pointTemplate.GetSiblingIndex());
            CopyRect(pointTemplate, _pointsContainer);

            var layout = containerGo.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = pointSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;

            pointTemplate.SetParent(_pointsContainer, false);
        }

        private void ApplyPointStates(int currentHp, int maxHp, int shieldHp)
        {
            var clampedCurrentHp = Mathf.Clamp(currentHp, 0, maxHp);
            var clampedShieldHp = Mathf.Clamp(shieldHp, 0, maxHp);
            var shieldCount = clampedShieldHp;
            var fullCount = Mathf.Clamp(clampedCurrentHp - shieldCount, 0, maxHp - shieldCount);
            var offCount = Mathf.Max(0, maxHp - shieldCount - fullCount);

            for (var i = 0; i < _points.Count; i++)
            {
                var point = _points[i];
                if (point == null)
                {
                    continue;
                }

                if (i < shieldCount)
                {
                    point.SetState(showShield: true, showFull: false, showOff: false);
                    continue;
                }

                if (i < shieldCount + fullCount)
                {
                    point.SetState(showShield: false, showFull: true, showOff: false);
                    continue;
                }

                if (i < shieldCount + fullCount + offCount)
                {
                    point.SetState(showShield: false, showFull: false, showOff: true);
                }
            }
        }

        private RectTransform FindRectTransformByName(string targetName)
        {
            var rects = GetComponentsInChildren<RectTransform>(true);
            for (var i = 0; i < rects.Length; i++)
            {
                if (rects[i] != null && rects[i].name == targetName)
                {
                    return rects[i];
                }
            }

            return null;
        }

        private static RectTransform FindChildRectTransformByName(RectTransform parent, string targetName)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i) as RectTransform;
                if (child != null && child.name == targetName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void NormalizePointRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localScale = Vector3.one;
        }

        private sealed class HpPointView
        {
            public HpPointView(RectTransform root)
            {
                Root = root;
                Full = FindChild(root, "HPBar_Point_Full");
                Shield = FindChild(root, "HPBar_Point_Shield");
                Off = FindChild(root, "HPBar_Point_Off");
            }

            public RectTransform Root { get; }
            private GameObject Full { get; }
            private GameObject Shield { get; }
            private GameObject Off { get; }

            public void SetState(bool showShield, bool showFull, bool showOff)
            {
                if (Shield != null)
                {
                    Shield.SetActive(showShield);
                }

                if (Full != null)
                {
                    Full.SetActive(showFull);
                }

                if (Off != null)
                {
                    Off.SetActive(showOff);
                }
            }

            private static GameObject FindChild(RectTransform root, string childName)
            {
                if (root == null)
                {
                    return null;
                }

                for (var i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i);
                    if (child != null && child.name == childName)
                    {
                        return child.gameObject;
                    }
                }

                return null;
            }
        }
    }
}
