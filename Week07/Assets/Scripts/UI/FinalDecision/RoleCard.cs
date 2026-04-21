using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 씬에 미리 배치된 드래그 가능한 역할 카드입니다.
/// OnEnable에서 홈 위치를 기록하고, 슬롯 드롭 시 즉시 스냅합니다.
/// 슬롯에서 밀려나면 DOTween으로 홈 복귀합니다.
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(CanvasGroup))]
public class RoleCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RoleType _roleType;

    public RoleType RoleType => _roleType;

    private Canvas        _canvas;
    private CanvasGroup   _canvasGroup;
    private RectTransform _rect;
    private RectTransform _parentRT;

    private Vector3    _homePosition;
    private Quaternion _homeRotation;
    private RoleSlot   _currentSlot;
    private Vector2    _dragOffset;
    private Tween      _returnTween;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _canvas      = GetComponentInParent<Canvas>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _rect        = GetComponent<RectTransform>();
        _parentRT    = _rect.parent as RectTransform;
        GetComponent<Image>().raycastTarget = true;
    }

    private void OnEnable()
    {
        // 활성화될 때마다 현재 위치를 홈으로 기록
        if (_rect == null) return;
        _homePosition = _rect.position;
        _homeRotation = _rect.localRotation;
    }

    // ── 초기화 ───────────────────────────────────────────────────────────────


    // ── 드래그 ───────────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        _returnTween?.Kill();

        if (_currentSlot != null)
        {
            _currentSlot.Release(this);
            _currentSlot = null;
        }

        // 클릭 지점 오프셋 기록 (마우스와 카드 중심 간 차이)
        if (TryScreenToLocal(eventData.position, out var localPoint))
            _dragOffset = _rect.anchoredPosition - localPoint;

        _canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (TryScreenToLocal(eventData.position, out var localPoint))
            _rect.anchoredPosition = localPoint + _dragOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.blocksRaycasts = true;

        if (_currentSlot == null)
            ReturnHome();
    }

    // ── 슬롯 상호작용 ────────────────────────────────────────────────────────

    /// <summary>드롭 시 슬롯 중심에 즉시 스냅합니다.</summary>
    public void SnapToSlot(RoleSlot slot)
    {
        _returnTween?.Kill();
        _currentSlot = slot;

        _rect.position    = ((RectTransform)slot.transform).position;
        _rect.localRotation = Quaternion.identity;
    }

    /// <summary>슬롯에서 밀려날 때 홈으로 DOTween 복귀합니다.</summary>
    public void ReturnHome()
    {
        _returnTween?.Kill();

        if (_currentSlot != null)
        {
            _currentSlot.Release(this);
            _currentSlot = null;
        }

        _returnTween = DOTween.Sequence()
            .Join(_rect.DOMove(_homePosition, 0.3f).SetEase(Ease.OutCubic))
            .Join(_rect.DOLocalRotateQuaternion(_homeRotation, 0.3f).SetEase(Ease.OutCubic))
            .SetLink(gameObject);
    }

    /// <summary>넉백 포물선 낙하. 위로 튕긴 뒤 X drift와 함께 로컬 Y축으로 낙하합니다.</summary>
    public void FallDown(System.Action onComplete = null)
    {
        _returnTween?.Kill();
        _canvasGroup.blocksRaycasts = false;

        float upDur    = Random.Range(0.15f, 0.22f);   // 위로 튕기는 시간
        float downDur  = Random.Range(0.55f, 0.80f);   // 낙하 시간

        float peakY    = Random.Range(80f, 180f);       // 위로 튕기는 높이
        float driftX   = Random.Range(-1f, 1f) >= 0f   // 넉백 방향 (좌우 랜덤)
                         ? Random.Range(200f, 450f)
                         : Random.Range(-450f, -200f);
        float fallY    = -(Screen.height * 1.6f);

        var start = _rect.localPosition;
        var peak  = start + new Vector3(driftX * 0.25f, peakY, 0f);
        var end   = start + new Vector3(driftX, fallY, 0f);

        _returnTween = DOTween.Sequence()
            // 1단계: 위로 튕김 (빠르게)
            .Append(_rect.DOLocalMove(peak, upDur).SetEase(Ease.OutQuad))
            // 2단계: 포물선 낙하 (중력 느낌)
            .Append(_rect.DOLocalMove(end, downDur).SetEase(Ease.InCubic))
            .Join(_canvasGroup.DOFade(0f, downDur * 0.7f).SetEase(Ease.InQuad))
            .OnComplete(() => onComplete?.Invoke())
            .SetLink(gameObject);
    }

    /// <summary>UI 닫을 때 애니메이션 없이 즉시 홈으로 복귀합니다.</summary>
    public void ReturnHomeInstant()
    {
        _returnTween?.Kill();

        if (_currentSlot != null)
        {
            _currentSlot.Release(this);
            _currentSlot = null;
        }

        _rect.position           = _homePosition;
        _rect.localRotation      = _homeRotation;
        _canvasGroup.alpha        = 1f;
        _canvasGroup.blocksRaycasts = true;
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────

    private bool TryScreenToLocal(Vector2 screenPos, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (_parentRT == null || _canvas == null) return false;
        var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRT, screenPos, cam, out localPoint);
    }
}
