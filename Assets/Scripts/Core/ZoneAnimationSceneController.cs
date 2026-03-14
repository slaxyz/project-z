using System.Collections;
using TMPro;
using ProjectZ.Run;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Core
{
    public class ZoneAnimationSceneController : MonoBehaviour
    {
        private const string RuntimeStarPrefix = "Runtime_Star_";
        [SerializeField] private float minimumSceneDuration = 4f;

        private AsyncOperation _boardPreloadOperation;
        private bool _isBoardActivationRequested;
        private bool _minimumDurationReached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnZoneAnimationScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.ZoneAnimation)
            {
                return;
            }

            if (FindFirstObjectByType<ZoneAnimationSceneController>() != null)
            {
                return;
            }

            var go = new GameObject("ZoneAnimationSceneController");
            go.AddComponent<ZoneAnimationSceneController>();
        }

        private void Start()
        {
            RefreshHeaderTexts();
            StartCoroutine(PreloadBoardAndTransition());
        }

        public void ContinueToBoard()
        {
            if (_boardPreloadOperation == null)
            {
                var manager = GameFlowManager.Instance;
                if (manager == null)
                {
                    return;
                }

                manager.OpenBoard();
                return;
            }

            if (_boardPreloadOperation.progress < 0.9f)
            {
                return;
            }

            if (!_minimumDurationReached)
            {
                return;
            }

            RequestBoardActivation();
        }

        private void RefreshHeaderTexts()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null || manager.CurrentRun == null)
            {
                return;
            }

            var runZoneIndex = Mathf.Max(0, manager.CurrentRun.zoneIndex);
            var zoneData = ResolveZoneData(runZoneIndex);
            var zoneName = zoneData != null ? zoneData.ZoneName : "Zone";
            var zoneLabel = ResolveRunZoneLabel(runZoneIndex);

            ApplyTextToGroup("Header_ZoneName", zoneName);
            ApplyTextToGroup("Header_ZoneLabel", zoneLabel);
            ApplyStars(zoneData != null ? zoneData.Difficulty : 0);
        }

        private static ZoneDataAsset ResolveZoneData(int runZoneIndex)
        {
            var registry = RuntimeAssetRegistryAsset.Load();
            var runLoopConfig = registry != null ? registry.RunLoopConfig : null;
            var zoneDatabase = registry != null ? registry.ZoneDatabase : null;

            if (runLoopConfig == null || zoneDatabase == null)
            {
                return null;
            }

            var zoneId = runLoopConfig.GetZoneIdForRunIndex(runZoneIndex, runZoneIndex + 1);
            return zoneDatabase.GetZoneById(zoneId);
        }

        private static string ResolveRunZoneLabel(int runZoneIndex)
        {
            switch (runZoneIndex)
            {
                case 0:
                    return "First Zone";
                case 1:
                    return "Second Zone";
                case 2:
                    return "Third Zone";
                case 3:
                    return "Fourth Zone";
                case 4:
                    return "Fifth Zone";
                default:
                    return (runZoneIndex + 1) + "th Zone";
            }
        }

        private static void ApplyTextToGroup(string groupName, string value)
        {
            var group = GameObject.Find(groupName);
            if (group == null)
            {
                return;
            }

            var texts = group.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                texts[i].text = value;
            }
        }

        private static void ApplyStars(int difficulty)
        {
            var starsRoot = FindStarsRoot();
            if (starsRoot == null)
            {
                return;
            }

            var template = starsRoot.childCount > 0 ? starsRoot.GetChild(0) : null;
            if (template == null)
            {
                return;
            }

            ClearRuntimeStars(starsRoot);

            var starCount = Mathf.Max(0, difficulty);
            template.gameObject.SetActive(starCount > 0);
            template.name = "Stars";

            for (var i = 1; i < starCount; i++)
            {
                var clone = Instantiate(template.gameObject, starsRoot, false);
                clone.name = RuntimeStarPrefix + i;
                clone.SetActive(true);
            }
        }

        private static Transform FindStarsRoot()
        {
            var layouts = Object.FindObjectsByType<UnityEngine.UI.HorizontalLayoutGroup>(FindObjectsSortMode.None);
            for (var i = 0; i < layouts.Length; i++)
            {
                var current = layouts[i];
                if (current != null && current.gameObject.name == "Stars" && current.transform.parent != null && current.transform.parent.name == "SafeArea")
                {
                    return current.transform;
                }
            }

            return null;
        }

        private static void ClearRuntimeStars(Transform starsRoot)
        {
            var toRemove = new List<GameObject>();
            for (var i = 0; i < starsRoot.childCount; i++)
            {
                var child = starsRoot.GetChild(i);
                if (child != null && child.name.StartsWith(RuntimeStarPrefix))
                {
                    toRemove.Add(child.gameObject);
                }
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                Destroy(toRemove[i]);
            }
        }

        private IEnumerator PreloadBoardAndTransition()
        {
            _boardPreloadOperation = SceneManager.LoadSceneAsync(GameScenes.Board, LoadSceneMode.Single);
            if (_boardPreloadOperation == null)
            {
                yield break;
            }

            _boardPreloadOperation.allowSceneActivation = false;
            var elapsed = 0f;
            var minDuration = Mathf.Max(0f, minimumSceneDuration);

            while (true)
            {
                elapsed += Time.deltaTime;

                var boardReady = _boardPreloadOperation.progress >= 0.9f;
                _minimumDurationReached = elapsed >= minDuration;
                if (boardReady && _minimumDurationReached)
                {
                    break;
                }

                yield return null;
            }

            RequestBoardActivation();
        }

        private void RequestBoardActivation()
        {
            if (_isBoardActivationRequested || _boardPreloadOperation == null)
            {
                return;
            }

            _isBoardActivationRequested = true;
            _boardPreloadOperation.allowSceneActivation = true;
        }
    }
}
