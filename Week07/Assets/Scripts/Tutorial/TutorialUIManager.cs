using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 시각 요소(가이드 텍스트 패널, 하이라이트)를 제어합니다.
///
/// Canvas 구성 예시:
///   TutorialUIManager
///   ├── GuideTextRoot
///   │   └── GuideText (TMP)   ← _guideText
///   ├── HighlightFrame (선택) ← _highlightFrame
///   └── WorldHighlightFx (선택) ← _worldHighlightFx
/// </summary>
[DisallowMultipleComponent]
public class TutorialUIManager : MonoBehaviour
{
    [Header("가이드 텍스트 패널")]
    [SerializeField] private GameObject _guideTextRoot;
    [SerializeField] private TMP_Text   _guideText;

    [Tooltip("ShowGuide 직후 이 시간(초) 동안 클릭 입력을 무시합니다.")]
    [SerializeField] private float _advanceInputDelay = 0.3f;

    [Header("하이라이트 오버레이 (선택)")]
    [SerializeField] private RectTransform _highlightFrame;
    [SerializeField] private GameObject   _worldHighlightFx;

    [Header("바운스 설정")]
    [Tooltip("원본 스케일 기준 최대 배율 (1.2 = 20% 크게)")]
    [SerializeField] private float _bounceScale     = 1.2f;
    [Tooltip("팝 애니메이션 총 시간(초)")]
    [SerializeField] private float _bounceDuration  = 0.4f;
    [Tooltip("가이드 텍스트 팝인 시간(초)")]
    [SerializeField] private float _guidePopDuration = 0.3f;

    [Header("영역 바운스 설정 (보조 바운스)")]
    [Tooltip("영역 원본 스케일 기준 최대 배율")]
    [SerializeField] private float _secondaryBounceScale    = 1.15f;
    [Tooltip("영역 팝 애니메이션 총 시간(초)")]
    [SerializeField] private float _secondaryBounceDuration = 0.5f;

    [Header("아이들 반복 설정")]
    [Tooltip("이 시간(초) 동안 입력이 없으면 바운스를 반복합니다.")]
    [SerializeField] private float _idleRepeatInterval = 3f;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    private Tween     _bounceTween;
    private Transform _highlightedTarget;
    private Vector3   _originalScale;

    // 보조 바운스 (주 바운스와 독립적으로 동작)
    private Tween     _secondaryTween;
    private Transform _secondaryTarget;
    private Vector3   _secondaryOriginalScale;

    private bool  _guideVisible;
    private float _inputBlockTimer;
    private bool  _clickAdvanceEnabled = true;
    private float _idleTimer;

    // ── 이벤트 ───────────────────────────────────────────────────────────────

    public event Action OnGuideAdvanced;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        SetGuideVisible(false);
        if (_highlightFrame != null)   _highlightFrame.gameObject.SetActive(false);
        if (_worldHighlightFx != null) _worldHighlightFx.SetActive(false);
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        bool anyInput = Mouse.current.leftButton.wasPressedThisFrame
                     || Mouse.current.rightButton.wasPressedThisFrame
                     || Mouse.current.delta.ReadValue().sqrMagnitude > 1f;

        if (anyInput) _idleTimer = 0f;

        // 클릭으로 가이드 넘기기
        if (_guideVisible && _clickAdvanceEnabled)
        {
            if (_inputBlockTimer > 0f)
                _inputBlockTimer -= Time.deltaTime;
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
                OnGuideAdvanced?.Invoke();
        }

        // 아이들 바운스 반복
        if (_highlightedTarget != null && (_bounceTween == null || !_bounceTween.IsActive()))
        {
            _idleTimer += Time.deltaTime;
            if (_idleTimer >= _idleRepeatInterval)
            {
                _idleTimer = 0f;
                PlayBounce(_highlightedTarget);
            }
        }
    }

    private void OnDestroy()
    {
        _bounceTween?.Kill();
        _secondaryTween?.Kill();
    }

    // ── 가이드 텍스트 API ────────────────────────────────────────────────────

    public void ShowGuide(string text)
    {
        if (_guideText != null) _guideText.text = text;
        _inputBlockTimer = _advanceInputDelay;
        SetGuideVisible(true);
    }

    public void HideGuide() => SetGuideVisible(false);

    /// <summary>
    /// 클릭으로 가이드를 넘길지 여부를 설정합니다.
    /// 드래그가 필요한 Phase에서는 false로 설정하세요.
    /// </summary>
    public void SetClickAdvance(bool enabled) => _clickAdvanceEnabled = enabled;

    private void SetGuideVisible(bool visible)
    {
        _guideVisible = visible;
        if (_guideTextRoot != null)
            _guideTextRoot.SetActive(visible);
    }

    // ── UI 하이라이트 API ────────────────────────────────────────────────────

    public void SetUIHighlight(RectTransform target, float padding = 20f)
    {
        if (target == null) return;

        if (_highlightFrame != null)
        {
            _highlightFrame.position  = target.position;
            _highlightFrame.sizeDelta = target.rect.size + Vector2.one * padding;
            _highlightFrame.gameObject.SetActive(true);
        }

        PlayBounce(target);
    }

    public void ClearUIHighlight()
    {
        if (_highlightFrame != null)
            _highlightFrame.gameObject.SetActive(false);
        StopBounce();
    }

    // ── World-Space 하이라이트 API ───────────────────────────────────────────

    public void SetWorldHighlight(Transform target)
    {
        if (target == null) return;

        if (_worldHighlightFx != null)
        {
            _worldHighlightFx.transform.position = target.position;
            _worldHighlightFx.SetActive(true);
        }

        PlayBounce(target);
    }

    public void ClearWorldHighlight()
    {
        if (_worldHighlightFx != null)
            _worldHighlightFx.SetActive(false);
        StopBounce();
    }

    /// <summary>하이라이트 오버레이 없이 바운스만 재생합니다.</summary>
    /// <param name="loop">true이면 StopBounce() 호출 전까지 무한 반복합니다.</param>
    public void SetBounceOnly(Transform target, bool loop = false)
    {
        if (target == null) return;
        PlayBounce(target, loop);
    }

    // ── 보조 바운스 API (영역 등 주 바운스와 동시에 사용) ──────────────────────

    /// <summary>주 바운스와 독립적으로 target에 반복 바운스를 재생합니다.</summary>
    public void SetSecondaryBounce(Transform target)
    {
        if (target == null) return;
        StopSecondaryBounce();
        _secondaryTarget        = target;
        _secondaryOriginalScale = target.localScale;

        Vector3 baseScale = (_secondaryOriginalScale == Vector3.zero) ? Vector3.one : _secondaryOriginalScale;

        _secondaryTween = DOTween.Sequence()
            .Append(target.DOScale(baseScale * _secondaryBounceScale, _secondaryBounceDuration * 0.4f).SetEase(Ease.OutQuad))
            .Append(target.DOScale(baseScale, _secondaryBounceDuration * 0.6f).SetEase(Ease.InOutQuad))
            .SetLoops(-1, LoopType.Restart)
            .SetLink(target.gameObject);
    }

    public void ClearSecondaryBounce()
    {
        StopSecondaryBounce();
    }

    private void StopSecondaryBounce()
    {
        _secondaryTween?.Kill();
        if (_secondaryTarget != null)
        {
            _secondaryTarget.localScale = (_secondaryOriginalScale == Vector3.zero)
                ? Vector3.one : _secondaryOriginalScale;
            _secondaryTarget = null;
        }
    }

    // ── 전체 초기화 ──────────────────────────────────────────────────────────

    public void ClearAll()
    {
        HideGuide();
        ClearUIHighlight();
        ClearWorldHighlight();
        ClearSecondaryBounce();
    }

    // ── 바운스 ───────────────────────────────────────────────────────────────

    private void PlayBounce(Transform target, bool loop = false)
    {
        StopBounce();
        _highlightedTarget = target;
        _originalScale     = target.localScale;
        _idleTimer         = 0f;

        // localScale이 zero인 오브젝트는 one으로 기준 삼음
        Vector3 baseScale = (_originalScale == Vector3.zero) ? Vector3.one : _originalScale;

        var seq = DOTween.Sequence()
            .Append(target.DOScale(baseScale * _bounceScale, _bounceDuration * 0.4f)
                .SetEase(Ease.OutQuad))
            .Append(target.DOScale(baseScale, _bounceDuration * 0.6f)
                .SetEase(Ease.InOutQuad));

        if (loop)
            seq.SetLoops(-1, LoopType.Restart);

        _bounceTween = seq.SetLink(target.gameObject);
    }

    private void StopBounce()
    {
        _bounceTween?.Kill();
        if (_highlightedTarget != null)
        {
            _highlightedTarget.localScale = _originalScale == Vector3.zero
                ? Vector3.one : _originalScale;
            _highlightedTarget = null;
        }
        _idleTimer = 0f;
    }
}
