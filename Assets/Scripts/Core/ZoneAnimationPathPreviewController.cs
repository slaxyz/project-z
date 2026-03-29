using System.Collections.Generic;
using ProjectZ.Run;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.Core
{
    public class ZoneAnimationPathPreviewController : MonoBehaviour
    {
        private const string StepsDoneTemplateName = "Steps_Done";
        private const string StepsNextTemplateName = "Steps_Next";
        private const string StepsOtherTemplateName = "Steps_Other";
        private const string StepsFailTemplateName = "Steps_Fail";
        private const string LinkTemplateName = "Steps";
        private const string IconChildName = "Icon";
        private const string RuntimePrefix = "Runtime_PathPreview_";

        private static readonly Dictionary<BoardTileType, string> OtherIconResourceNameByType = new Dictionary<BoardTileType, string>
        {
            { BoardTileType.Event, "surprise" },
            { BoardTileType.Shop, "gift" },
            { BoardTileType.Fight, "monster" },
            { BoardTileType.Boss, "boss" }
        };

        private readonly List<GameObject> _runtimeInstances = new List<GameObject>();

        private Transform _stepsDoneTemplate;
        private Transform _stepsNextTemplate;
        private Transform _stepsOtherTemplate;
        private Transform _stepsFailTemplate;
        private Transform _linkTemplate;

        private void Start()
        {
            RefreshPreview();
        }

        [ContextMenu("Refresh Preview")]
        public void RefreshPreview()
        {
            CacheTemplates();
            if (!HasTemplates())
            {
                Debug.LogWarning("PathPreview_Top setup incomplete: missing one or more step templates.");
                return;
            }

            ClearRuntimeInstances();
            SetTemplatesVisible(false);

            var manager = GameFlowManager.Instance;
            if (manager == null || manager.CurrentRun == null || !manager.CurrentRun.isActive)
            {
                return;
            }

            var previewSteps = BuildPreviewSteps(
                manager.GetTilesForCurrentZone(),
                GetPreviewBranchChoice(manager));

            if (previewSteps.Count == 0)
            {
                return;
            }

            var activeIndex = Mathf.Clamp(manager.GetActiveTileIndex(), 0, previewSteps.Count - 1);
            BuildRuntimeFeed(previewSteps, activeIndex);
        }

        private void CacheTemplates()
        {
            _stepsDoneTemplate = FindTemplate(StepsDoneTemplateName);
            _stepsNextTemplate = FindTemplate(StepsNextTemplateName);
            _stepsOtherTemplate = FindTemplate(StepsOtherTemplateName);
            _stepsFailTemplate = FindTemplate(StepsFailTemplateName);
            _linkTemplate = FindTemplate(LinkTemplateName);
        }

        private bool HasTemplates()
        {
            return _stepsDoneTemplate != null
                && _stepsNextTemplate != null
                && _stepsOtherTemplate != null
                && _linkTemplate != null;
        }

        private Transform FindTemplate(string templateName)
        {
            var child = transform.Find(templateName);
            if (child != null)
            {
                return child;
            }

            for (var i = 0; i < transform.childCount; i++)
            {
                var current = transform.GetChild(i);
                if (current.name == templateName)
                {
                    return current;
                }
            }

            return null;
        }

        private void ClearRuntimeInstances()
        {
            for (var i = 0; i < _runtimeInstances.Count; i++)
            {
                var instance = _runtimeInstances[i];
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            _runtimeInstances.Clear();
        }

        private void SetTemplatesVisible(bool isVisible)
        {
            _stepsDoneTemplate.gameObject.SetActive(isVisible);
            _stepsNextTemplate.gameObject.SetActive(isVisible);
            _stepsOtherTemplate.gameObject.SetActive(isVisible);
            if (_stepsFailTemplate != null)
            {
                _stepsFailTemplate.gameObject.SetActive(isVisible);
            }
            _linkTemplate.gameObject.SetActive(isVisible);
        }

        private void BuildRuntimeFeed(List<BoardTileType> previewSteps, int activeIndex)
        {
            var showFailStep = ShouldShowFailStep();
            for (var stepIndex = 0; stepIndex < previewSteps.Count; stepIndex++)
            {
                if (stepIndex < activeIndex)
                {
                    CreateRuntimeStep(_stepsDoneTemplate, "Done", stepIndex, previewSteps[stepIndex], shouldApplyTypeIcon: false, shouldForceDoneIcon: true);
                    continue;
                }

                if (stepIndex == activeIndex)
                {
                    if (showFailStep && _stepsFailTemplate != null)
                    {
                        CreateRuntimeStep(_stepsFailTemplate, "Fail", stepIndex, previewSteps[stepIndex], shouldApplyTypeIcon: false, shouldForceDoneIcon: false);
                    }
                    else
                    {
                        CreateRuntimeStep(_stepsNextTemplate, "Next", stepIndex, previewSteps[stepIndex], shouldApplyTypeIcon: true, shouldForceDoneIcon: false);
                    }
                }
                else
                {
                    CreateRuntimeStep(_stepsOtherTemplate, "Other", stepIndex, previewSteps[stepIndex], shouldApplyTypeIcon: true, shouldForceDoneIcon: false);
                }

                if (stepIndex < previewSteps.Count - 1)
                {
                    CreateRuntimeLink(stepIndex);
                }
            }
        }

        private bool ShouldShowFailStep()
        {
            var manager = GameFlowManager.Instance;
            return manager != null
                && manager.HasLastFightResult
                && !manager.LastFightWasVictory
                && IsUnderLoseScreen();
        }

        private bool IsUnderLoseScreen()
        {
            var current = transform.parent;
            while (current != null)
            {
                if (current.name == "LoseScreen")
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void CreateRuntimeStep(
            Transform template,
            string label,
            int stepIndex,
            BoardTileType tileType,
            bool shouldApplyTypeIcon,
            bool shouldForceDoneIcon)
        {
            var instance = Instantiate(template.gameObject, transform, false);
            instance.name = RuntimePrefix + label + "_" + stepIndex;
            instance.SetActive(true);
            _runtimeInstances.Add(instance);

            if (shouldForceDoneIcon)
            {
                ApplyIcon(instance.transform, "done");
                return;
            }

            if (shouldApplyTypeIcon)
            {
                ApplyIcon(instance.transform, GetOtherIconResourceName(tileType));
            }
        }

        private void CreateRuntimeLink(int stepIndex)
        {
            var instance = Instantiate(_linkTemplate.gameObject, transform, false);
            instance.name = RuntimePrefix + "Link_" + stepIndex;
            instance.SetActive(true);
            _runtimeInstances.Add(instance);
        }

        private static int GetPreviewBranchChoice(GameFlowManager manager)
        {
            if (manager.CurrentRun.branchChoice >= 0)
            {
                return manager.CurrentRun.branchChoice;
            }

            // The zone preview happens before the branch is chosen, so we preview the left path by default.
            return 0;
        }

        private static List<BoardTileType> BuildPreviewSteps(int tileCount, int branchChoice)
        {
            var previewSteps = new List<BoardTileType>();
            for (var step = 0; step < tileCount; step++)
            {
                if (step == 2)
                {
                    previewSteps.Add(branchChoice == 0 ? BoardTileType.Fight : BoardTileType.Event);
                    continue;
                }

                if (step == 3)
                {
                    previewSteps.Add(branchChoice == 0 ? BoardTileType.Event : BoardTileType.Fight);
                    continue;
                }

                if (step == 6)
                {
                    previewSteps.Add(BoardTileType.Shop);
                    continue;
                }

                if (step == tileCount - 1)
                {
                    previewSteps.Add(BoardTileType.Boss);
                    continue;
                }

                previewSteps.Add(BoardTileType.Fight);
            }

            return previewSteps;
        }

        private void ApplyIcon(Transform stepRoot, string iconResourceName)
        {
            if (string.IsNullOrWhiteSpace(iconResourceName))
            {
                return;
            }

            var iconTransform = stepRoot.Find(IconChildName);
            if (iconTransform == null)
            {
                return;
            }

            var iconImage = iconTransform.GetComponent<Image>();
            if (iconImage == null)
            {
                return;
            }

            var iconSprite = Resources.Load<Sprite>("Art/UI/ZoneAnimation/Steps/" + iconResourceName);
            if (iconSprite == null)
            {
                Debug.LogWarning("Missing path preview icon sprite: " + iconResourceName);
                return;
            }

            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
        }

        private static string GetOtherIconResourceName(BoardTileType tileType)
        {
            if (OtherIconResourceNameByType.TryGetValue(tileType, out var iconName))
            {
                return iconName;
            }

            return "monster";
        }
    }
}
