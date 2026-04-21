using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 패널을 X축 드래그로 꺼내고 집어넣는 Drawer 컴포넌트.
///
/// ─── 동작 원칙 ───────────────────────────────────────────────────────────
///   Hidden 상태 : Awake에서 캡처한 초기 anchoredPosition.x / localRotation
///   Shown  상태 : _shownAnchoredX 위치 / Rotation(0,0,0)
///
///   _shownAnchoredX > 초기 X → 왼쪽 숨김, 오른쪽으로 꺼냄 (기본)
///   _shownAnchoredX < 초기 X → 오른쪽 숨김, 왼쪽으로 꺼냄
///
///   드래그 종료 → 임계값(거리/속도) 기준으로 완료 또는 snap-back
///
/// ─── 설정 방법 ───────────────────────────────────────────────────────────
///   1. 프리팹/씬에서 패널의 anchoredPosition.x 와 localRotation을 숨김 상태로 배치
///   2. Inspector의 _shownAnchoredX 에 완전히 꺼낸 상태의 X값을 입력
///      (숨김 X보다 크면 왼→오른, 작으면 오른→왼)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class DrawerPanel : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("위치 설정")]
    [Tooltip("완전히 꺼낸 상태의 anchoredPosition X.\n" +
             "숨김 위치는 Awake에서 초기 anchoredPosition.x로 자동 캡처됩니다.")]
    [SerializeField] private float _shownAnchoredX = 0f;

    [Header("드래그 임계값")]
    [Tooltip("이 거리(px) 이상 드래그하면 완료 처리합니다.")]
    [SerializeField] private float _distanceThreshold = 100f;
    [Tooltip("이 속도(px/s) 이상이면 거리 미달이어도 완료 처리합니다.")]
    [SerializeField] private float _velocityThreshold  = 600f;

    [Header("DOTween 설정")]
    [SerializeField] private float _animDuration = 0.4f;
    [SerializeField] private Ease  _showEase     = Ease.OutCubic;
    [SerializeField] private Ease  _hideEase     = Ease.InCubic;

    [Header("드로어 버튼 목록")]
    [Tooltip("펼쳐졌을 때 활성화되는 버튼들. 누르면 드로어가 닫힙니다.")]
    [SerializeField] private Button[] _drawerButtons;

    // ── 이벤트 ───────────────────────────────────────────────────────────────

    /// <summary>Show 애니메이션이 완전히 끝났을 때 발생합니다.</summary>
    public event Action OnShown;

    /// <summary>Hide 애니메이션이 완전히 끝났을 때 발생합니다.</summary>
    public event Action OnHidden;

    // ── 공개 프로퍼티 ────────────────────────────────────────────────────────

    public bool IsShown { get; private set; }

    // ── 내부 상태 ────────────────────────────────────────────────────────────

    private RectTransform _rect;

    /// <summary>씬/프리팹에서 설정한 숨김 위치 X (Awake 시 자동 캡처)</summary>
    private float      _hiddenAnchoredX;
    /// <summary>씬/프리팹에서 설정한 숨김 Rotation (Awake 시 자동 캡처)</summary>
    private Quaternion _hiddenLocalRotation;

    // 꺼낸 상태 목표 Rotation은 항상 정방향
    private static readonly Quaternion ShownLocalRotation = Quaternion.identity;

    private Tweener _moveTween;
    private Tweener _rotateTween;

    /// <summary>드래그 중 계산한 X 속도 (px/s, 오른쪽 양수)</summary>
    private float _dragVelocityX;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rect                = GetComponent<RectTransform>();
        _hiddenAnchoredX     = _rect.anchoredPosition.x;
        _hiddenLocalRotation = _rect.localRotation;

        SetButtonsActive(false);

        if (_drawerButtons != null)
            foreach (var btn in _drawerButtons)
                if (btn != null)
                    btn.onClick.AddListener(() => Hide());
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
        _rotateTween?.Kill();

        if (_drawerButtons != null)
            foreach (var btn in _drawerButtons)
                if (btn != null)
                    btn.onClick.RemoveAllListeners();
    }

    // ── 외부 API ─────────────────────────────────────────────────────────────

    /// <summary>패널을 꺼냅니다 (Shown 위치로 슬라이드, Rotation → 0,0,0).</summary>
    public void Show(bool instant = false) => TransitionTo(isShowing: true, instant);

    /// <summary>패널을 집어넣습니다 (Hidden 위치로 슬라이드, Rotation → 초기값).</summary>
    public void Hide(bool instant = false) => TransitionTo(isShowing: false, instant);

    // _shownAnchoredX > _hiddenAnchoredX 이면 +1(왼→오른), 아니면 -1(오른→왼)
    private float ShowDirection => Mathf.Sign(_shownAnchoredX - _hiddenAnchoredX);

    // ── 드래그 핸들러 ────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        _moveTween?.Kill();
        _rotateTween?.Kill();
        _dragVelocityX = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Time.deltaTime > 0f)
            _dragVelocityX = eventData.delta.x / Time.deltaTime;

        // hiddenX ~ shownX 범위로 클램프 (방향 무관)
        float newX = _rect.anchoredPosition.x + eventData.delta.x;
        newX = Mathf.Clamp(newX,
            Mathf.Min(_hiddenAnchoredX, _shownAnchoredX),
            Mathf.Max(_hiddenAnchoredX, _shownAnchoredX));

        _rect.anchoredPosition = new Vector2(newX, _rect.anchoredPosition.y);

        // X 진행도(0-1)에 따라 Rotation 실시간 보간
        ApplyRotationByProgress();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 현재 상태에 따라 반대 방향으로 충분히 이동했는지 판단
        bool shouldShow = IsShown ? !ShouldHideOnRelease() : ShouldShowOnRelease();
        TransitionTo(isShowing: shouldShow, instant: false);
    }

    // ── 내부 전환 로직 ───────────────────────────────────────────────────────

    /// <summary>Hidden 상태에서 Shown 방향으로 충분히 드래그했는지 판단합니다.</summary>
    private bool ShouldShowOnRelease()
    {
        float draggedToShown = (_rect.anchoredPosition.x - _hiddenAnchoredX) * ShowDirection;
        bool  pastDistance   = draggedToShown > _distanceThreshold;
        bool  fastSwipe      = _dragVelocityX * ShowDirection > _velocityThreshold;
        return pastDistance || fastSwipe;
    }

    /// <summary>Shown 상태에서 Hidden 방향으로 충분히 드래그했는지 판단합니다.</summary>
    private bool ShouldHideOnRelease()
    {
        float draggedToHidden = (_shownAnchoredX - _rect.anchoredPosition.x) * ShowDirection;
        bool  pastDistance    = draggedToHidden > _distanceThreshold;
        bool  fastSwipe       = _dragVelocityX * ShowDirection < -_velocityThreshold;
        return pastDistance || fastSwipe;
    }

    private void TransitionTo(bool isShowing, bool instant)
    {
        IsShown = isShowing;

        float      targetX   = isShowing ? _shownAnchoredX      : _hiddenAnchoredX;
        Quaternion targetRot = isShowing ? ShownLocalRotation    : _hiddenLocalRotation;
        Ease       ease      = isShowing ? _showEase             : _hideEase;

        _moveTween?.Kill();
        _rotateTween?.Kill();

        if (instant)
        {
            _rect.anchoredPosition = new Vector2(targetX, _rect.anchoredPosition.y);
            _rect.localRotation    = targetRot;
            NotifyComplete(isShowing);
            return;
        }

        // 위치와 회전을 동일한 Duration/Ease로 동기화
        _moveTween = _rect
            .DOAnchorPosX(targetX, _animDuration)
            .SetEase(ease)
            .OnComplete(() => NotifyComplete(isShowing));

        _rotateTween = _rect
            .DOLocalRotateQuaternion(targetRot, _animDuration)
            .SetEase(ease);
    }

    /// <summary>드래그 X 진행도(0→1)를 기반으로 Rotation을 Lerp합니다.</summary>
    private void ApplyRotationByProgress()
    {
        float range = _shownAnchoredX - _hiddenAnchoredX;
        if (Mathf.Approximately(range, 0f)) return;

        float t = Mathf.Clamp01((_rect.anchoredPosition.x - _hiddenAnchoredX) / range);
        _rect.localRotation = Quaternion.Lerp(_hiddenLocalRotation, ShownLocalRotation, t);
    }

    private void NotifyComplete(bool isShowing)
    {
        SetButtonsActive(isShowing);
        if (isShowing) OnShown?.Invoke();
        else           OnHidden?.Invoke();
    }

    private void SetButtonsActive(bool active)
    {
        if (_drawerButtons == null) return;
        foreach (var btn in _drawerButtons)
            if (btn != null)
                btn.gameObject.SetActive(active);
    }

    // ── Editor 방어 ──────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        if (Mathf.Approximately(_shownAnchoredX, rt.anchoredPosition.x))
            Debug.LogWarning(
                $"[DrawerPanel] _shownAnchoredX({_shownAnchoredX})가 " +
                $"초기 anchoredPosition.x({rt.anchoredPosition.x})와 같습니다. " +
                "두 값이 달라야 패널이 움직입니다.");

        if (_distanceThreshold <= 0f)
            Debug.LogWarning("[DrawerPanel] _distanceThreshold는 0보다 커야 합니다.");

        if (_animDuration <= 0f)
            Debug.LogWarning("[DrawerPanel] _animDuration은 0보다 커야 합니다.");
    }
#endif
}
