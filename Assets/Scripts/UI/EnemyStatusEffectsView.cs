using ProjectZ.Combat;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class EnemyStatusEffectsView : MonoBehaviour
    {
        [SerializeField] private RectTransform enemyHudRoot;
        [SerializeField] private RectTransform statusRoot;
        [SerializeField] private StatusEffectView statusTemplate;

        private FightMockController _fight;
        private readonly List<StatusEffectView> _views = new List<StatusEffectView>();

        private void Awake()
        {
            AutoAssignIfNeeded();
            Sync();
        }

        private void OnEnable()
        {
            AutoAssignIfNeeded();
            Sync();
        }

        private void Update()
        {
            Sync();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
        }

        private void AutoAssignIfNeeded()
        {
            if (enemyHudRoot == null)
            {
                var hudObject = GameObject.Find("EnemyHUD_Right");
                if (hudObject != null)
                {
                    enemyHudRoot = hudObject.GetComponent<RectTransform>();
                }
            }

            if (statusRoot == null)
            {
                if (enemyHudRoot != null)
                {
                    statusRoot = FindDeepChild(enemyHudRoot, "Status");
                }

                if (statusRoot == null)
                {
                    var rootObject = GameObject.Find("Status");
                    if (rootObject != null)
                    {
                        statusRoot = rootObject.GetComponent<RectTransform>();
                    }
                }
            }

            if (statusTemplate == null && statusRoot != null)
            {
                statusTemplate = FindExistingTemplate(statusRoot);
            }

            if (statusTemplate == null && statusRoot != null && statusRoot.childCount > 0)
            {
                var firstChild = statusRoot.GetChild(0);
                statusTemplate = firstChild != null ? firstChild.GetComponent<StatusEffectView>() : null;
                if (statusTemplate == null && firstChild != null)
                {
                    statusTemplate = firstChild.gameObject.AddComponent<StatusEffectView>();
                }
            }

            if (statusTemplate != null && statusTemplate.GetComponent<StatusEffectView>() == null)
            {
                statusTemplate = statusTemplate.gameObject.AddComponent<StatusEffectView>();
            }
        }

        private void Sync()
        {
            AutoAssignIfNeeded();

            if (_fight == null)
            {
                _fight = FindFirstObjectByType<FightMockController>();
            }

            if (statusRoot == null || _fight == null || !_fight.TryGetEnemyStatusEffects(out var statuses))
            {
                SetViewCount(0);
                return;
            }

            SetViewCount(statuses != null ? statuses.Count : 0);

            for (var i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view == null)
                {
                    continue;
                }

                if (statuses == null || i >= statuses.Count)
                {
                    view.Clear();
                    view.gameObject.SetActive(false);
                    continue;
                }

                view.gameObject.SetActive(true);
                view.SetStatus(statuses[i]);
            }
        }

        private void SetViewCount(int desiredCount)
        {
            if (statusRoot == null || desiredCount < 0)
            {
                return;
            }

            if (statusTemplate == null)
            {
                return;
            }

            EnsureTemplateReady();

            if (desiredCount == 0 && _views.Count == 0)
            {
                statusTemplate.Clear();
                statusTemplate.gameObject.SetActive(false);
                return;
            }

            while (_views.Count < desiredCount)
            {
                StatusEffectView view;
                if (_views.Count == 0)
                {
                    view = statusTemplate;
                }
                else
                {
                    var clone = Instantiate(statusTemplate.gameObject, statusRoot, false);
                    clone.name = statusTemplate.gameObject.name;
                    view = clone.GetComponent<StatusEffectView>();
                    if (view == null)
                    {
                        view = clone.AddComponent<StatusEffectView>();
                    }
                }

                if (view != null)
                {
                    view.gameObject.SetActive(true);
                    _views.Add(view);
                }
                else
                {
                    break;
                }
            }

            for (var i = _views.Count - 1; i >= desiredCount; i--)
            {
                var view = _views[i];
                if (view != null)
                {
                    view.Clear();
                    view.gameObject.SetActive(false);
                }
            }

            if (desiredCount == 0)
            {
                for (var i = 0; i < _views.Count; i++)
                {
                    var view = _views[i];
                    if (view != null)
                    {
                        view.Clear();
                        view.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void EnsureTemplateReady()
        {
            if (statusTemplate == null)
            {
                return;
            }

            if (statusTemplate.GetComponent<StatusEffectView>() == null)
            {
                statusTemplate = statusTemplate.gameObject.AddComponent<StatusEffectView>();
            }

            if (statusTemplate.transform.parent != statusRoot)
            {
                statusTemplate.transform.SetParent(statusRoot, false);
            }
        }

        private static StatusEffectView FindExistingTemplate(RectTransform root)
        {
            if (root == null)
            {
                return null;
            }

            var views = root.GetComponentsInChildren<StatusEffectView>(true);
            if (views != null)
            {
                for (var i = 0; i < views.Length; i++)
                {
                    if (views[i] != null)
                    {
                        return views[i];
                    }
                }
            }

            return null;
        }

        private static RectTransform FindDeepChild(RectTransform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == targetName)
                {
                    return children[i] as RectTransform;
                }
            }

            return null;
        }
    }
}
