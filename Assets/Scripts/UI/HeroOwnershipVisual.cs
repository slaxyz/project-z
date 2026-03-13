using ProjectZ.Run;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [DisallowMultipleComponent]
    public class HeroOwnershipVisual : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] private Image ownedAvatarImage;
        [SerializeField] private Image lockedAvatarImage;
        [SerializeField] private Image darkOverlayImage;

        [Header("Tuning")]
        [SerializeField, Range(0f, 1f)] private float idleOverlayAlpha = 0.2f;

        private ChampionDefinitionAsset _champion;
        private bool _isOwned = true;
        private bool _isSelected;

        public void Bind(ChampionDefinitionAsset champion, bool isOwned, bool isSelected)
        {
            _champion = champion;
            _isOwned = isOwned;
            _isSelected = isSelected;
            RefreshVisuals();
        }

        public void SetOwned(bool isOwned)
        {
            _isOwned = isOwned;
            RefreshVisuals();
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            RefreshVisuals();
        }

        private void Reset()
        {
            AutoAssignIfNeeded();
        }

        private void OnValidate()
        {
            AutoAssignIfNeeded();
        }

        private void AutoAssignIfNeeded()
        {
            if (ownedAvatarImage == null)
            {
                ownedAvatarImage = transform.Find("Avatar")?.GetComponent<Image>();
            }

            if (lockedAvatarImage == null)
            {
                lockedAvatarImage = transform.Find("DisabledAvatar")?.GetComponent<Image>();
            }

            if (darkOverlayImage == null)
            {
                darkOverlayImage = transform.Find("DarkOverlay")?.GetComponent<Image>();
            }
        }

        private void RefreshVisuals()
        {
            AutoAssignIfNeeded();

            var ownedSprite = ResolveOwnedSprite(_champion);
            var lockedSprite = ResolveLockedSprite(_champion) ?? ownedSprite;

            if (ownedAvatarImage != null)
            {
                ownedAvatarImage.sprite = ownedSprite;
                ownedAvatarImage.color = ownedSprite != null ? Color.white : Color.clear;
                ownedAvatarImage.preserveAspect = true;
                ownedAvatarImage.gameObject.SetActive(_isOwned);
            }

            if (lockedAvatarImage != null)
            {
                lockedAvatarImage.sprite = lockedSprite;
                lockedAvatarImage.color = lockedSprite != null ? Color.white : Color.clear;
                lockedAvatarImage.preserveAspect = true;
                lockedAvatarImage.gameObject.SetActive(!_isOwned);
            }

            if (darkOverlayImage != null)
            {
                var showOverlay = _isOwned && !_isSelected;
                darkOverlayImage.gameObject.SetActive(showOverlay);

                if (showOverlay)
                {
                    var color = darkOverlayImage.color;
                    color.a = idleOverlayAlpha;
                    darkOverlayImage.color = color;
                }
            }
        }

        private static Sprite ResolveOwnedSprite(ChampionDefinitionAsset champion)
        {
            if (champion == null)
            {
                return null;
            }

            return champion.AvatarSprite != null ? champion.AvatarSprite : champion.SplashSprite;
        }

        private static Sprite ResolveLockedSprite(ChampionDefinitionAsset champion)
        {
            if (champion == null || string.IsNullOrWhiteSpace(champion.Id))
            {
                return null;
            }

            var normalizedId = champion.Id.Trim().ToLowerInvariant();
            return Resources.Load<Sprite>("Art/Characters/" + normalizedId + "/" + normalizedId + "_avatarBlack");
        }
    }
}
