using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class CustomCursorUI : MonoBehaviour
{
    // ─────────────────────── Inspector ───────────────────────────────────────

    [Header("참조")]
    [Tooltip("이 커서가 속한 Canvas")]
    [SerializeField] private Canvas _canvas;

    [Tooltip("커서 Image 컴포넌트 (보통 자기 자신)")]
    [SerializeField] private Image _cursorImage;

    [Header("핫스팟")]
    [Tooltip("커서 이미지 기준 실제 클릭 포인트 오프셋 (UI 픽셀 단위)")]
    [SerializeField] private Vector2 _hotspot = Vector2.zero;

    [Header("Sprites")]
    [SerializeField] private Sprite _normalSprite;
    [SerializeField] private Sprite _leftClickSprite;
    [SerializeField] private Sprite _rightClickSprite;

    // ─────────────────────── Private ─────────────────────────────────────────

    private RectTransform _rectTransform;
    private RectTransform _canvasRect;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();

        if (_cursorImage == null)
            _cursorImage = GetComponent<Image>();

        if (_canvas != null)
            _canvasRect = _canvas.GetComponent<RectTransform>();

        if (_normalSprite != null)
            _cursorImage.sprite = _normalSprite;
    }

    private void OnEnable()  => Cursor.visible = false;
    private void OnDisable() => Cursor.visible = true;

    private void Update()
    {
        UpdatePosition();
        UpdateSprite();
    }

    // ─────────────────────── 위치 ────────────────────────────────────────────

    private void UpdatePosition()
    {
        if (_canvasRect == null) return;

        Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            Input.mousePosition,
            cam,
            out Vector2 localPoint
        );

        _rectTransform.localPosition = localPoint - _hotspot;
    }

    // ─────────────────────── 스프라이트 교체 ─────────────────────────────────

    private void UpdateSprite()
    {
        Sprite next;

        if (Input.GetMouseButton(0) && _leftClickSprite != null)
            next = _leftClickSprite;
        else if (Input.GetMouseButton(1) && _rightClickSprite != null)
            next = _rightClickSprite;
        else
            next = _normalSprite;

        if (next != null && _cursorImage.sprite != next)
            _cursorImage.sprite = next;
    }
}