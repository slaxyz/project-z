using UnityEngine;
using UnityEngine.UI;

namespace ProjectZ.UI
{
    [RequireComponent(typeof(RawImage))]
    public class UIRawImageScroller : MonoBehaviour
    {
        [SerializeField] private Vector2 speed = new Vector2(0.02f, 0.02f);

        private RawImage _rawImage;
        private Rect _uvRect;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _uvRect = _rawImage.uvRect;
        }

        private void Update()
        {
            _uvRect.position += speed * Time.deltaTime;
            _rawImage.uvRect = _uvRect;
        }
    }
}
