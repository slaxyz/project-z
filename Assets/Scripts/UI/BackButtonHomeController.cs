using ProjectZ.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public class BackButtonHomeController : MonoBehaviour
    {
        [SerializeField] private Button button;

        private void Awake()
        {
            EnsureRuntimeButton();
            HookButton();
        }

        private void OnEnable()
        {
            EnsureRuntimeButton();
            HookButton();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }
        }

        private void Reset()
        {
            FindExistingButton();
        }

        private void OnValidate()
        {
            FindExistingButton();
        }

        private void FindExistingButton()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button.targetGraphic == null)
            {
                button.targetGraphic = GetComponent<Image>();
            }
        }

        private void EnsureRuntimeButton()
        {
            FindExistingButton();

            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
                button.targetGraphic = GetComponent<Image>();
            }
        }

        private void HookButton()
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
        }

        private static void OnClicked()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            manager.GoToHome();
        }
    }
}
