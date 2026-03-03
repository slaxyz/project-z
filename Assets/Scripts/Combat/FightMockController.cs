using ProjectZ.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectZ.Combat
{
    public class FightMockController : MonoBehaviour
    {
        [SerializeField] private float enemyAttackInterval = 1.25f;

        private int _playerMaxHp;
        private int _enemyMaxHp;
        private int _playerHp;
        private int _enemyHp;
        private int _block;

        private float _enemyAttackTimer;
        private float _strikeCooldown;
        private float _guardCooldown;
        private float _healCooldown;

        private bool _isResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceOnFightScene()
        {
            if (SceneManager.GetActiveScene().name != GameScenes.Fight)
            {
                return;
            }

            var existing = FindFirstObjectByType<FightMockController>();
            if (existing != null)
            {
                return;
            }

            var go = new GameObject("FightMockController");
            go.AddComponent<FightMockController>();
        }

        private void Start()
        {
            var manager = GameFlowManager.Instance;
            var nodeIndex = manager != null ? manager.CurrentRun.boardNodeIndex : 0;
            var difficulty = Mathf.Max(0, nodeIndex);

            _playerMaxHp = 54;
            _enemyMaxHp = 30 + difficulty * 6;
            _playerHp = _playerMaxHp;
            _enemyHp = _enemyMaxHp;
            _enemyAttackTimer = enemyAttackInterval;
        }

        private void Update()
        {
            if (_isResolved)
            {
                return;
            }

            TickCooldown(ref _strikeCooldown);
            TickCooldown(ref _guardCooldown);
            TickCooldown(ref _healCooldown);

            _enemyAttackTimer -= Time.deltaTime;
            if (_enemyAttackTimer <= 0f)
            {
                DoEnemyAttack();
                _enemyAttackTimer = enemyAttackInterval;
            }

            EvaluateFightOutcome();
        }

        private static void TickCooldown(ref float value)
        {
            if (value <= 0f)
            {
                return;
            }

            value -= Time.deltaTime;
            if (value < 0f)
            {
                value = 0f;
            }
        }

        private void DoEnemyAttack()
        {
            var manager = GameFlowManager.Instance;
            var nodeIndex = manager != null ? manager.CurrentRun.boardNodeIndex : 0;
            var incoming = 7 + nodeIndex;

            var reduced = Mathf.Max(0, incoming - _block);
            _block = Mathf.Max(0, _block - incoming);
            _playerHp = Mathf.Max(0, _playerHp - reduced);
        }

        private void DoStrike()
        {
            if (_strikeCooldown > 0f)
            {
                return;
            }

            _enemyHp = Mathf.Max(0, _enemyHp - 8);
            _strikeCooldown = 0.35f;
            EvaluateFightOutcome();
        }

        private void DoGuard()
        {
            if (_guardCooldown > 0f)
            {
                return;
            }

            _block += 7;
            _guardCooldown = 1.1f;
        }

        private void DoHeal()
        {
            if (_healCooldown > 0f)
            {
                return;
            }

            _playerHp = Mathf.Min(_playerMaxHp, _playerHp + 6);
            _healCooldown = 2.4f;
        }

        private void EvaluateFightOutcome()
        {
            if (_isResolved)
            {
                return;
            }

            var manager = GameFlowManager.Instance;
            if (manager == null || !manager.CanShowFightResult())
            {
                return;
            }

            if (_enemyHp <= 0)
            {
                _isResolved = true;
                manager.ShowResult(true);
            }
            else if (_playerHp <= 0)
            {
                _isResolved = true;
                manager.ShowResult(false);
            }
        }

        private void OnGUI()
        {
            var w = 520f;
            var h = 300f;
            var x = (Screen.width - w) * 0.5f;
            var y = (Screen.height - h) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
            GUILayout.Label("Fight Mock (v1)");
            GUILayout.Space(6f);

            GUILayout.Label("Player HP: " + _playerHp + " / " + _playerMaxHp + " | Block: " + _block);
            GUILayout.HorizontalSlider(_playerMaxHp > 0 ? (float)_playerHp / _playerMaxHp : 0f, 0f, 1f);
            GUILayout.Space(4f);
            GUILayout.Label("Enemy HP: " + _enemyHp + " / " + _enemyMaxHp);
            GUILayout.HorizontalSlider(_enemyMaxHp > 0 ? (float)_enemyHp / _enemyMaxHp : 0f, 0f, 1f);

            GUILayout.Space(8f);
            GUILayout.Label("Enemy next attack in: " + _enemyAttackTimer.ToString("0.0") + "s");

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUI.enabled = !_isResolved && _strikeCooldown <= 0f;
            if (GUILayout.Button("Strike (-8)"))
            {
                DoStrike();
            }

            GUI.enabled = !_isResolved && _guardCooldown <= 0f;
            if (GUILayout.Button("Guard (+7 block)"))
            {
                DoGuard();
            }

            GUI.enabled = !_isResolved && _healCooldown <= 0f;
            if (GUILayout.Button("Heal (+6)"))
            {
                DoHeal();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Cooldowns -> Strike: " + _strikeCooldown.ToString("0.0") + "s | Guard: " + _guardCooldown.ToString("0.0") + "s | Heal: " + _healCooldown.ToString("0.0") + "s");
            GUILayout.EndArea();
        }
    }
}
