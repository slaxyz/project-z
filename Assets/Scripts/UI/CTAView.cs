using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class CTAView : MonoBehaviour
    {
        [SerializeField] private bool previewDisabled;
        [SerializeField] private GameObject ctaShadow;
        [SerializeField] private Image ctaRadial;
        [SerializeField] private Sprite activeRadialSprite;
        [SerializeField] private Color activeRadialColor = Color.white;
        [SerializeField] private Color disabledRadialColor = new(171f / 255f, 171f / 255f, 171f / 255f, 1f);

        private Button _button;
        private bool _appliedDisabled;
        private bool _hasAppliedState;

        private void Reset()
        {
            AutoAssign();
            CaptureDefaultsIfNeeded();
            ApplyCurrentState();
        }

        private void Awake()
        {
            AutoAssign();
            CaptureDefaultsIfNeeded();
            CacheButton();
            ApplyCurrentState();
        }

        private void OnEnable()
        {
            CacheButton();
            ApplyCurrentState();
        }

        private void Update()
        {
            if (_button == null)
            {
                CacheButton();
            }

            var shouldDisable = ResolveDisabledState();
            if (_hasAppliedState && shouldDisable == _appliedDisabled)
            {
                return;
            }

            ApplyState(shouldDisable);
        }

        private void OnValidate()
        {
            AutoAssign();
            CaptureDefaultsIfNeeded();

            if (Application.isPlaying)
            {
                return;
            }

            ApplyCurrentState();
        }

        public void SetInteractable(bool isInteractable)
        {
            previewDisabled = !isInteractable;
            ApplyState(!isInteractable);
        }

        private void CacheButton()
        {
            if (_button == null)
            {
                TryGetComponent(out _button);
            }
        }

        private void AutoAssign()
        {
            if (ctaShadow == null)
            {
                var shadow = transform.Find("CTA Visual/CTA_Shadow");
                if (shadow != null)
                {
                    ctaShadow = shadow.gameObject;
                }
            }

            if (ctaRadial == null)
            {
                var radial = transform.Find("CTA Visual/CTA_Radial");
                if (radial != null)
                {
                    ctaRadial = radial.GetComponent<Image>();
                }
            }
        }

        private void CaptureDefaultsIfNeeded()
        {
            if (ctaRadial == null)
            {
                return;
            }

            if (activeRadialSprite == null && ctaRadial.sprite != null)
            {
                activeRadialSprite = ctaRadial.sprite;
            }
        }

        private bool ResolveDisabledState()
        {
            return _button != null ? !_button.interactable : previewDisabled;
        }

        private void ApplyCurrentState()
        {
            ApplyState(ResolveDisabledState());
        }

        private void ApplyState(bool disabled)
        {
            if (ctaShadow != null)
            {
                ctaShadow.SetActive(!disabled);
            }

            if (ctaRadial != null)
            {
                if (disabled)
                {
                    ctaRadial.sprite = null;
                    ctaRadial.color = disabledRadialColor;
                }
                else
                {
                    ctaRadial.sprite = activeRadialSprite;
                    ctaRadial.color = activeRadialColor;
                }
            }

            _appliedDisabled = disabled;
            _hasAppliedState = true;
        }
    }
}
