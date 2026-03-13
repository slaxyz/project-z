using TMPro;
using UnityEngine;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public class CollectionStatRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text statLabelText;
        [SerializeField] private TMP_Text statValueText;

        private void Reset()
        {
            AutoAssignIfNeeded();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
        }

        public void SetData(string label, string value)
        {
            AutoAssignIfNeeded();

            if (statLabelText != null)
            {
                statLabelText.text = label;
            }

            if (statValueText != null)
            {
                statValueText.text = value;
            }
        }

        private void AutoAssignIfNeeded()
        {
            if (statLabelText == null)
            {
                statLabelText = FindTextByName("StatLabel");
            }

            if (statValueText == null)
            {
                statValueText = FindTextByName("StatValue");
            }
        }

        private TMP_Text FindTextByName(string targetName)
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
    }
}
