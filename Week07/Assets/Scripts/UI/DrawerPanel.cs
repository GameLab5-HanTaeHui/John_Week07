using DG.Tweening;
using System;
using System.Collections.Generic;
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
///   _moveVertically 체크 (Y축 이동):
///   _shownAnchoredY > 초기 Y → 아래 숨김, 위로 꺼냄
///   _shownAnchoredY < 초기 Y → 위 숨김, 아래로 꺼냄
/// 
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
public class DrawerPanel : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("방향 설정")]
    [Tooltip("체크하면 Y축(위아래)으로 움직이고, 해제하면 X축(좌우)으로 움직입니다.")]
    [SerializeField] private bool _moveVertically = false;

    [Header("위치 설정")]
    [Tooltip("완전히 꺼낸 상태의 anchoredPosition X.\n" +
             "숨김 위치는 Awake에서 초기 anchoredPosition.x로 자동 캡처됩니다.")]
    [SerializeField] private float _shownAnchoredX = 0f;
    [Tooltip("완전히 꺼낸 상태의 anchoredPosition Y.\n" +
             "숨김 위치는 Awake에서 초기 anchoredPosition.y로 자동 캡처됩니다.")]
    [SerializeField] private float _shownAnchoredY = 0f;

    [Header("DOTween 설정")]
    [SerializeField] private float _animDuration = 0.4f;
    [SerializeField] private Ease  _showEase     = Ease.OutCubic;
    [SerializeField] private Ease  _hideEase     = Ease.InCubic;

    [Header("드로어 버튼 목록")]
    [Tooltip("펼쳐졌을 때 활성화되는 버튼들. 누르면 드로어가 닫힙니다.")]
    [SerializeField] private Button _drawerButton;

    // ── 이벤트 ───────────────────────────────────────────────────────────────

    /// <summary>Show 애니메이션이 완전히 끝났을 때 발생합니다.</summary>
    public event Action OnShown;

    /// <summary>Hide 애니메이션이 완전히 끝났을 때 발생합니다.</summary>
    public event Action OnHidden;

    // ── 공개 프로퍼티 ────────────────────────────────────────────────────────

    public bool IsShown { get; private set; }

    // ── 내부 상태 ────────────────────────────────────────────────────────────

    private RectTransform _rect;

    /// <summary>씬/프리팹에서 설정한 숨김 위치 X, Y (Awake 시 자동 캡처)</summary>
    private float      _hiddenAnchoredX;
    private float      _hiddenAnchoredY;
    /// <summary>씬/프리팹에서 설정한 숨김 Rotation (Awake 시 자동 캡처)</summary>
    private Quaternion _hiddenLocalRotation;

    // 꺼낸 상태 목표 Rotation은 항상 정방향
    private static readonly Quaternion ShownLocalRotation = Quaternion.identity;

    private Tweener _moveTween;
    private Tweener _rotateTween;

    /// <summary>이번 세션에서 이 패널을 열람한 횟수</summary>
    private int _openCount;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rect                = GetComponent<RectTransform>();
        _hiddenAnchoredX     = _rect.anchoredPosition.x;
        _hiddenAnchoredY     = _rect.anchoredPosition.y;
        _hiddenLocalRotation = _rect.localRotation;

        if (_drawerButton != null)
            _drawerButton.onClick.AddListener(() => Toggle());
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
        _rotateTween?.Kill();

        if (_drawerButton != null)
            _drawerButton.onClick.RemoveAllListeners();
    }

    // ── 외부 API ─────────────────────────────────────────────────────────────

    /// <summary>패널을 꺼냅니다 (Shown 위치로 슬라이드, Rotation → 0,0,0).</summary>
    public void Show(bool instant = false) => TransitionTo(isShowing: true, instant);

    /// <summary>패널을 집어넣습니다 (Hidden 위치로 슬라이드, Rotation → 초기값).</summary>
    public void Hide(bool instant = false) => TransitionTo(isShowing: false, instant);
    /// <summary>현재 상태의 반대로 패널을 열거나 닫습니다.</summary>
    public void Toggle(bool instant = false)
    {
        // [HTH추가] FinalDecision 상태에서는 닫기 차단
        if (GameFlowController.Instance?.CurrentLoopState == LoopStateType.FinalDecision)
        {
            if (IsShown) return; // 이미 열려있다면 무시
        }

        if (IsShown) Hide(instant);
        else Show(instant);
    }

    // ── 내부 전환 로직 ───────────────────────────────────────────────────────

    private void TransitionTo(bool isShowing, bool instant)
    {
        IsShown = isShowing;

        // 선택된 축에 따라 목표 위치(Vector2) 설정
        float targetX = _moveVertically ? _rect.anchoredPosition.x
            : (isShowing ? _shownAnchoredX : _hiddenAnchoredX);

        float targetY = _moveVertically ? (isShowing ? _shownAnchoredY : _hiddenAnchoredY)
            : _rect.anchoredPosition.y;

        Vector2 targetPos = new Vector2(targetX, targetY);

        Quaternion targetRot = isShowing ? ShownLocalRotation    : _hiddenLocalRotation;
        Ease       ease      = isShowing ? _showEase             : _hideEase;

        _moveTween?.Kill();
        _rotateTween?.Kill();

        if (instant)
        {
            _rect.anchoredPosition = targetPos;
            _rect.localRotation    = targetRot;
            NotifyComplete(isShowing);
            return;
        }

        // 위치 이동
        _moveTween = _rect
        .DOAnchorPos(targetPos, _animDuration)
        .SetEase(ease)
        .OnComplete(() => NotifyComplete(isShowing));

        _rotateTween = _rect
            .DOLocalRotateQuaternion(targetRot, _animDuration)
            .SetEase(ease);
    }

    private void NotifyComplete(bool isShowing)
    {
        //if (isShowing) OnShown?.Invoke();
        //else           OnHidden?.Invoke();

        // [HTH추가]
        if (isShowing)
        {
            OnShown?.Invoke();
            // ★ 패널 열람 로그 (지표 #13)
            _openCount++;
            var gfc = GameFlowController.Instance;
            GameLogger.Instance?.LogEvent("panel_open", new Dictionary<string, object>
            {
                { "panel_name",            gameObject.name },
                { "day",                   gfc?.CurrentDay ?? 0 },
                { "time_of_day",           gfc?.CurrentTimeOfDay ?? "" },
                { "open_count_this_session", _openCount },
            });
        }
        else
        {
            OnHidden?.Invoke();
        }
    }

    // ── Editor 방어 ──────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        if (!_moveVertically && Mathf.Approximately(_shownAnchoredX, rt.anchoredPosition.x))
            Debug.LogWarning(
                $"[DrawerPanel] _shownAnchoredX({_shownAnchoredX})가 " +
                $"초기 anchoredPosition.x({rt.anchoredPosition.x})와 같습니다. " +
                "두 값이 달라야 패널이 움직입니다.");

        if (_moveVertically && Mathf.Approximately(_shownAnchoredY, rt.anchoredPosition.y))
            Debug.LogWarning(
                $"[DrawerPanel] _shownAnchoredY({_shownAnchoredY})가 " +
                $"초기 anchoredPosition.y({rt.anchoredPosition.y})와 같습니다. " +
                "두 값이 달라야 패널이 움직입니다.");

        if (_animDuration <= 0f)
            Debug.LogWarning("[DrawerPanel] _animDuration은 0보다 커야 합니다.");
    }
#endif
}
