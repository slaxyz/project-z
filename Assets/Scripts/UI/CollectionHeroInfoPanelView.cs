using System.Linq;
using ProjectZ.Run;
using TMPro;
using UnityEngine;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public class CollectionHeroInfoPanelView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private CollectionHeroCarouselController carouselController;

        [Header("Header")]
        [SerializeField] private TMP_Text heroNameText;
        [SerializeField] private TMP_Text heroRealNameText;
        [SerializeField] private UnityEngine.UI.Button refreshButton;

        [Header("Front")]
        [SerializeField] private GameObject frontContent;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Back")]
        [SerializeField] private GameObject backContent;
        [SerializeField] private TMP_Text classDescriptionText;
        [SerializeField] private Transform statsRowsRoot;
        [SerializeField] private GameObject statRowPrefab;

        private readonly System.Collections.Generic.List<CollectionStatRowView> _statRows = new System.Collections.Generic.List<CollectionStatRowView>();
        private ChampionDefinitionAsset _currentChampion;
        private bool _showingBackFace;

        private void OnEnable()
        {
            AutoAssignIfNeeded();
            HookRefreshButton();

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
            UnhookRefreshButton();

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
            _currentChampion = champion;

            if (champion == null)
            {
                ClearTexts();
                return;
            }

            if (_showingBackFace)
            {
                ApplyBackFace(champion);
            }
            else
            {
                ApplyFrontFace(champion);
            }
        }

        private void OnSelectionChanged(ChampionDefinitionAsset champion)
        {
            SetChampion(champion);
        }

        public void ShowFront()
        {
            _showingBackFace = false;
            UpdateFaceVisibility();
            SetChampion(_currentChampion);
        }

        public void ShowBack()
        {
            _showingBackFace = true;
            UpdateFaceVisibility();
            SetChampion(_currentChampion);
        }

        public void ToggleFace()
        {
            _showingBackFace = !_showingBackFace;
            UpdateFaceVisibility();
            SetChampion(_currentChampion);
        }

        private void AutoAssignIfNeeded()
        {
            if (carouselController == null)
            {
                carouselController = FindFirstObjectByType<CollectionHeroCarouselController>();
            }

            if (frontContent == null)
            {
                frontContent = FindChild("Content_Switcher/FrontContent") ?? FindChild("FrontContent");
            }

            if (backContent == null)
            {
                backContent = FindChild("Content_Switcher/BackContent") ?? FindChild("BackContent");
            }

            if (heroNameText == null)
            {
                heroNameText = FindTextByName("HeroName");
            }

            if (heroRealNameText == null)
            {
                heroRealNameText = FindTextByName("HeroRealName");
            }

            if (refreshButton == null)
            {
                refreshButton = transform.Find("Refresh")?.GetComponent<UnityEngine.UI.Button>();
            }

            if (descriptionText == null)
            {
                descriptionText = FindTextByName("Description")
                    ?? FindTextByName("DescriptionText")
                    ?? FindTextByName("HeroDescription");
            }

            if (classDescriptionText == null)
            {
                classDescriptionText = FindTextByName("ClassDescription")
                    ?? FindTextByName("BackDescription")
                    ?? FindTextByName("ClassBody");
            }

            if (statsRowsRoot == null)
            {
                statsRowsRoot = FindTransform("Content_Switcher/BackContent/StatsRows")
                    ?? FindTransform("BackContent/StatsRows")
                    ?? FindTransform("Content_Switcher/BackContent/StatsBlock")
                    ?? FindTransform("BackContent/StatsBlock");
            }

#if UNITY_EDITOR
            if (statRowPrefab == null)
            {
                var prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/CollectionStats.prefab");
                if (prefabAsset != null)
                {
                    statRowPrefab = prefabAsset;
                }
            }
#endif

            UpdateFaceVisibility();
        }

        private void HookRefreshButton()
        {
            if (refreshButton == null)
            {
                return;
            }

            refreshButton.onClick.RemoveListener(ToggleFace);
            refreshButton.onClick.AddListener(ToggleFace);
        }

        private void UnhookRefreshButton()
        {
            if (refreshButton == null)
            {
                return;
            }

            refreshButton.onClick.RemoveListener(ToggleFace);
        }

        private TMP_Text FindTextByName(string targetName)
        {
            return GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text != null && text.name == targetName);
        }

        private Transform FindTransform(string path)
        {
            return transform.Find(path);
        }

        private GameObject FindChild(string path)
        {
            return transform.Find(path)?.gameObject;
        }

        private void ApplyFrontFace(ChampionDefinitionAsset champion)
        {
            if (heroNameText != null)
            {
                heroNameText.text = champion.DisplayName;
            }

            if (heroRealNameText != null)
            {
                heroRealNameText.text = champion.FullName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = champion.Description;
            }
        }

        private void ApplyBackFace(ChampionDefinitionAsset champion)
        {
            if (heroNameText != null)
            {
                heroNameText.text = champion.ClassLabel;
            }

            if (heroRealNameText != null)
            {
                heroRealNameText.text = champion.ChampionClass.ToString().ToUpperInvariant();
            }

            if (classDescriptionText != null)
            {
                classDescriptionText.text = champion.ClassDefinition != null && !string.IsNullOrWhiteSpace(champion.ClassDefinition.Description)
                    ? champion.ClassDefinition.Description
                    : champion.ClassLabel;
            }

            RefreshStats(champion);
        }

        private void RefreshStats(ChampionDefinitionAsset champion)
        {
            if (statsRowsRoot == null || statRowPrefab == null || champion == null)
            {
                return;
            }

            EnsureStatRows();

            SetStatRow(0, "HP", champion.BaseHp);
            SetStatRow(1, "ATK", champion.BaseAttack);
            SetStatRow(2, "DEF", champion.BaseDefense);
            SetStatRow(3, "SPE", champion.BaseSpecial);
        }

        private void EnsureStatRows()
        {
            for (var i = _statRows.Count - 1; i >= 0; i--)
            {
                if (_statRows[i] == null)
                {
                    _statRows.RemoveAt(i);
                }
            }

            if (_statRows.Count == 0)
            {
                var existingRows = statsRowsRoot.GetComponentsInChildren<CollectionStatRowView>(true);
                for (var i = 0; i < existingRows.Length; i++)
                {
                    if (existingRows[i] != null)
                    {
                        _statRows.Add(existingRows[i]);
                    }
                }

                if (_statRows.Count == 0)
                {
                    for (var i = 0; i < statsRowsRoot.childCount; i++)
                    {
                        var child = statsRowsRoot.GetChild(i);
                        if (child == null)
                        {
                            continue;
                        }

                        var row = child.GetComponent<CollectionStatRowView>();
                        if (row == null)
                        {
                            row = child.gameObject.AddComponent<CollectionStatRowView>();
                        }

                        _statRows.Add(row);
                    }
                }
            }

            while (_statRows.Count < 4)
            {
                var rowObject = Instantiate(statRowPrefab, statsRowsRoot);
                var row = rowObject.GetComponent<CollectionStatRowView>();
                if (row == null)
                {
                    row = rowObject.AddComponent<CollectionStatRowView>();
                }

                row.name = "StatRow_" + (_statRows.Count + 1);
                _statRows.Add(row);
            }
        }

        private void SetStatRow(int index, string label, int value)
        {
            if (index < 0 || index >= _statRows.Count || _statRows[index] == null)
            {
                return;
            }

            _statRows[index].gameObject.SetActive(true);
            _statRows[index].SetData(label, value.ToString());
        }

        private void UpdateFaceVisibility()
        {
            if (frontContent != null)
            {
                frontContent.SetActive(!_showingBackFace);
            }

            if (backContent != null)
            {
                backContent.SetActive(_showingBackFace);
            }
        }

        private void ClearTexts()
        {
            if (heroNameText != null)
            {
                heroNameText.text = string.Empty;
            }

            if (heroRealNameText != null)
            {
                heroRealNameText.text = string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.Empty;
            }

            if (classDescriptionText != null)
            {
                classDescriptionText.text = string.Empty;
            }

            for (var i = 0; i < _statRows.Count; i++)
            {
                if (_statRows[i] != null)
                {
                    _statRows[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
