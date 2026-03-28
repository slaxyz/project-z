using ProjectZ.Combat;
using ProjectZ.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class EnemyFightPanelView : MonoBehaviour
    {
        private static bool _sceneHookRegistered;
        [SerializeField] private RectTransform rightEnemyPanelRoot;
        [SerializeField] private RectTransform enemyHudRoot;
        [SerializeField] private Image zoneBackgroundImage;
        [SerializeField] private Image splashImage;
        [SerializeField] private TypeBadgeView typeBadgeView;
        [SerializeField] private TMP_Text enemyNameMainText;
        [SerializeField] private TMP_Text enemyNameShadowText;

        private FightMockController _fight;
        private TeamHudHealthBarView _enemyHealthBarView;
        private string _lastEnemyId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            if (_sceneHookRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneHookRegistered = true;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameScenes.Fight)
            {
                return;
            }

            if (FindFirstObjectByType<EnemyFightPanelView>() != null)
            {
                return;
            }

            var host = GameObject.Find("Fight_UI_Manual");
            if (host == null)
            {
                host = new GameObject("EnemyFightPanelView");
            }

            host.AddComponent<EnemyFightPanelView>();
        }

        private void Awake()
        {
            AutoAssignIfNeeded();
            EnsureEnemyHudBindings();
            Sync();
        }

        private void OnEnable()
        {
            AutoAssignIfNeeded();
            EnsureEnemyHudBindings();
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
            if (rightEnemyPanelRoot == null)
            {
                rightEnemyPanelRoot = GameObject.Find("RightEnemyPanel")?.GetComponent<RectTransform>();
            }

            if (enemyHudRoot == null)
            {
                enemyHudRoot = GameObject.Find("EnemyHUD_Right")?.GetComponent<RectTransform>();
            }

            if (zoneBackgroundImage == null)
            {
                zoneBackgroundImage = FindDeepChild(rightEnemyPanelRoot, "zone_BG")?.GetComponent<Image>();
            }

            if (splashImage == null)
            {
                splashImage = FindDeepChild(rightEnemyPanelRoot, "monster_splash")?.GetComponent<Image>();
            }

            if (typeBadgeView == null)
            {
                var badgeRoot = FindDeepChild(rightEnemyPanelRoot, "ElementBadge");
                if (badgeRoot != null)
                {
                    typeBadgeView = badgeRoot.GetComponent<TypeBadgeView>();
                    if (typeBadgeView == null)
                    {
                        typeBadgeView = badgeRoot.gameObject.AddComponent<TypeBadgeView>();
                    }
                }
            }

            if (!IsUnderRoot(enemyNameMainText, enemyHudRoot))
            {
                enemyNameMainText = FindLabel(enemyHudRoot, "Nickname", "Label_Main");
            }

            if (!IsUnderRoot(enemyNameShadowText, enemyHudRoot))
            {
                enemyNameShadowText = FindLabel(enemyHudRoot, "Nickname", "Label_Shadow");
            }
        }

        private void EnsureEnemyHudBindings()
        {
            if (enemyHudRoot == null)
            {
                return;
            }

            _enemyHealthBarView = enemyHudRoot.GetComponent<TeamHudHealthBarView>();
            if (_enemyHealthBarView == null)
            {
                _enemyHealthBarView = enemyHudRoot.gameObject.AddComponent<TeamHudHealthBarView>();
            }

            _enemyHealthBarView.SetTarget(CombatHudTarget.Enemy);
        }

        private void Sync()
        {
            if (_fight == null)
            {
                _fight = FindFirstObjectByType<FightMockController>();
            }

            if (_fight == null || !_fight.TryGetCurrentEnemyDefinition(out var enemy))
            {
                ClearVisuals();
                return;
            }

            if (_lastEnemyId == enemy.Id)
            {
                return;
            }

            ApplySprite(zoneBackgroundImage, enemy.ZoneBackgroundSprite, preserveAspect: false);
            ApplySprite(splashImage, enemy.SplashSprite, preserveAspect: true);

            if (typeBadgeView != null)
            {
                typeBadgeView.SetType(enemy.TypeDefinition, false);
            }

            ApplyText(enemyNameMainText, enemy.DisplayName);
            ApplyText(enemyNameShadowText, enemy.DisplayName);

            _lastEnemyId = enemy.Id;
        }

        private void ClearVisuals()
        {
            ApplySprite(zoneBackgroundImage, null, preserveAspect: false);
            ApplySprite(splashImage, null, preserveAspect: true);

            if (typeBadgeView != null)
            {
                typeBadgeView.SetType(null, false);
            }

            ApplyText(enemyNameMainText, string.Empty);
            ApplyText(enemyNameShadowText, string.Empty);
            _lastEnemyId = null;
        }

        private static void ApplyText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static void ApplySprite(Image target, Sprite sprite, bool preserveAspect)
        {
            if (target == null)
            {
                return;
            }

            target.sprite = sprite;
            target.color = sprite != null ? Color.white : Color.clear;
            target.preserveAspect = preserveAspect;
        }

        private static TMP_Text FindLabel(RectTransform root, string groupName, string labelName)
        {
            var group = FindDeepChild(root, groupName);
            if (group == null)
            {
                return null;
            }

            var labels = group.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].name == labelName)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private static bool IsUnderRoot(Component target, RectTransform root)
        {
            return target != null && root != null && target.transform.IsChildOf(root);
        }

        private static Transform FindDeepChild(RectTransform root, string targetName)
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
                    return children[i];
                }
            }

            return null;
        }
    }
}
