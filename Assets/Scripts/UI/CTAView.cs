using System.Collections;
using UnityEngine.Serialization;
using UnityEngine;
using UnityEngine.UI;
using ProjectZ.Combat;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public sealed class CTAView : MonoBehaviour
    {
        public enum CTAVisualMode
        {
            Interactable = 0,
            Manual = 1,
        }

        public enum CTAState
        {
            Default = 0,
            Grey = 1,
        }

        [FormerlySerializedAs("previewState")]
        [SerializeField] private CTAState ctaState = CTAState.Default;
        [SerializeField] private CTAVisualMode visualMode = CTAVisualMode.Interactable;
        [SerializeField, HideInInspector] private bool previewDisabled;
        [SerializeField] private GameObject ctaShadow;
        [SerializeField] private RectTransform ctaVisual;
        [SerializeField] private Image ctaRadial;
        [SerializeField] private GameObject ctaRadialGrey;
        [SerializeField] private Sprite activeRadialSprite;
        [SerializeField] private Color activeRadialColor = Color.white;
        [SerializeField] private Color disabledRadialColor = new(171f / 255f, 171f / 255f, 171f / 255f, 1f);
        [SerializeField] private float clickBounceScale = 0.94f;
        [SerializeField] private float clickBounceInDuration = 0.06f;
        [SerializeField] private float clickBounceOutDuration = 0.08f;
        [SerializeField] private float clickBounceDelayBeforeAction = 0.02f;

        private Button _button;
        private bool _appliedDisabled;
        private bool _hasAppliedState;
        private Vector3 _baseScale;
        private Coroutine _clickBounceRoutine;

        private void Reset()
        {
            AutoAssign();
            CaptureDefaultsIfNeeded();
            ApplyCurrentState();
        }

        private void Awake()
        {
            CacheVisual();
            _baseScale = GetVisualTransform().localScale;
            AutoAssign();
            CaptureDefaultsIfNeeded();
            CacheButton();
            ApplyCurrentState();
        }

        private void OnEnable()
        {
            CacheButton();
            HookButton();
            ApplyCurrentState();
        }

        private void OnDisable()
        {
            StopClickBounce();

            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        private void Update()
        {
            if (visualMode != CTAVisualMode.Interactable)
            {
                return;
            }

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
            if (_button == null)
            {
                CacheButton();
            }

            if (_button != null)
            {
                _button.interactable = isInteractable;
            }

            if (visualMode == CTAVisualMode.Interactable)
            {
                ApplyState(!isInteractable);
            }
        }

        private void CacheButton()
        {
            if (_button == null)
            {
                TryGetComponent(out _button);
            }
        }

        private void HookButton()
        {
            if (_button == null)
            {
                return;
            }

            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            PlayClickBounce();
        }

        private void PlayClickBounce()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            StopClickBounce();
            _clickBounceRoutine = StartCoroutine(PlayClickBounceRoutine());
        }

        private IEnumerator PlayClickBounceRoutine()
        {
            var visual = GetVisualTransform();
            visual.localScale = _baseScale;

            var scaledUp = _baseScale * clickBounceScale;
            yield return ScaleOverTime(_baseScale, scaledUp, clickBounceInDuration);
            yield return ScaleOverTime(scaledUp, _baseScale, clickBounceOutDuration);

            if (clickBounceDelayBeforeAction > 0f)
            {
                yield return new WaitForSeconds(clickBounceDelayBeforeAction);
            }

            _clickBounceRoutine = null;

            var fight = Object.FindFirstObjectByType<FightMockController>();
            if (fight == null)
            {
                yield break;
            }

            fight.RequestEndTurn();
        }

        private IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
        {
            var visual = GetVisualTransform();

            if (duration <= 0f)
            {
                visual.localScale = to;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                visual.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            visual.localScale = to;
        }

        private void StopClickBounce()
        {
            if (_clickBounceRoutine != null)
            {
                StopCoroutine(_clickBounceRoutine);
                _clickBounceRoutine = null;
            }

            GetVisualTransform().localScale = _baseScale;
        }

        private void AutoAssign()
        {
            CacheVisual();

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

            if (ctaRadialGrey == null)
            {
                var radialGrey = transform.Find("CTA Visual/CTA_Radial_Grey");
                if (radialGrey != null)
                {
                    ctaRadialGrey = radialGrey.gameObject;
                }
            }
        }

        private void CacheVisual()
        {
            if (ctaVisual == null)
            {
                var visual = transform.Find("CTA Visual");
                if (visual != null)
                {
                    ctaVisual = visual as RectTransform;
                }
            }
        }

        private Transform GetVisualTransform()
        {
            return ctaVisual != null ? ctaVisual : transform;
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
            if (visualMode == CTAVisualMode.Manual)
            {
                return ctaState == CTAState.Grey;
            }

            if (_button != null)
            {
                return !_button.interactable;
            }

            return previewDisabled;
        }

        private void ApplyCurrentState()
        {
            ApplyState(ResolveDisabledState());
        }

        private void ApplyState(bool disabled)
        {
            if (ctaShadow != null)
            {
                ctaShadow.SetActive(true);
            }

            if (ctaRadialGrey != null)
            {
                ctaRadialGrey.SetActive(disabled);
            }

            if (ctaRadial != null)
            {
                ctaRadial.gameObject.SetActive(!disabled);
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
