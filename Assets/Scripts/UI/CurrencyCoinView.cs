using ProjectZ.Core;
using TMPro;
using UnityEngine;

namespace ProjectZ.UI
{
    public class CurrencyCoinView : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinValueText;

        private int _lastShownValue = int.MinValue;

        private void Awake()
        {
            EnsureReferences();
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Reset()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            EnsureReferences();
        }

        private void Refresh()
        {
            if (coinValueText == null)
            {
                return;
            }

            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            var coins = ResolveDisplayedCoins(manager);
            if (coins == _lastShownValue)
            {
                return;
            }

            _lastShownValue = coins;
            coinValueText.text = coins.ToString();
        }

        private static int ResolveDisplayedCoins(GameFlowManager manager)
        {
            if (manager == null)
            {
                return 0;
            }

            // During an active run (including Shop), display run coins so purchases are visible.
            if (manager.CurrentRun != null && manager.CurrentRun.isActive)
            {
                return Mathf.Max(0, manager.CurrentRun.coinsGained);
            }

            return Mathf.Max(0, manager.GetPlayerCoins());
        }

        private void EnsureReferences()
        {
            if (coinValueText != null)
            {
                return;
            }

            var coinValueTransform = transform.Find("CoinValue");
            if (coinValueTransform != null)
            {
                coinValueText = coinValueTransform.GetComponent<TMP_Text>();
            }
        }
    }
}
