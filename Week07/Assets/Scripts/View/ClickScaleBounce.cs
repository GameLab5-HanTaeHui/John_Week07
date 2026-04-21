using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 클릭 시 DOPunchScale 바운스 효과를 재생하는 애니메이션 컴포넌트.
/// 입력 감지는 MapObjectInputHandler가 담당합니다.
///
/// ─── 확장 방법 ─────────────────────────────────────────────────────────
///   • OnBounceStarted / OnBounceCompleted 이벤트로 사운드·파티클 연결
///   • PlayBounce()를 코드에서 직접 호출해 다른 트리거에도 재사용 가능
///   • _preset으로 Inspector에서 자주 쓰는 바운스 느낌을 빠르게 선택
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ClickScaleBounce : MonoBehaviour
{
    // ── 프리셋 ────────────────────────────────────────────────────────────────

    public enum BouncePreset
    {
        Custom,
        Soft,       // 부드럽고 작은 바운스
        Snappy,     // 빠르고 짧은 바운스
        Jelly,      // 느리고 탄성 강한 바운스
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("프리셋")]
    [Tooltip("자주 쓰는 느낌을 빠르게 선택. Custom이면 아래 값을 직접 사용합니다.")]
    [SerializeField] private BouncePreset _preset = BouncePreset.Snappy;

    [Header("Custom 설정 (프리셋이 Custom일 때만 적용)")]
    [Tooltip("펀치 스케일 크기 (원래 스케일에 더해지는 상대값)")]
    [SerializeField] private Vector3 _punch        = new Vector3(0.25f, 0.25f, 0.25f);
    [Tooltip("애니메이션 총 시간(초)")]
    [SerializeField] private float   _duration     = 0.35f;
    [Tooltip("진동 횟수 — 1이면 단순 바운스, 높을수록 파르르 떨림")]
    [SerializeField] [Range(1, 10)] private int   _vibrato     = 1;
    [Tooltip("탄성 (0 = 오버슈트 없음, 1 = 최대 오버슈트)")]
    [SerializeField] [Range(0f, 1f)] private float _elasticity  = 0.5f;

    [Header("제어")]
    [Tooltip("true이면 애니메이션 진행 중 재클릭을 무시합니다.")]
    [SerializeField] private bool _ignoreDuringAnimation = false;

    // ── 이벤트 ────────────────────────────────────────────────────────────────

    public event Action OnBounceStarted;
    public event Action OnBounceCompleted;

    // ── 공개 프로퍼티 ─────────────────────────────────────────────────────────

    public bool IsPlaying { get; private set; }

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    private Vector3 _originalScale;
    private Tweener _tween;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _originalScale = transform.localScale;
    }

    private void OnDestroy()
    {
        _tween?.Kill();
    }

    // ── 외부 API ──────────────────────────────────────────────────────────────

    /// <summary>바운스 애니메이션을 재생합니다. MapObjectInputHandler 또는 코드에서 직접 호출합니다.</summary>
    public void PlayBounce()
    {
        if (_ignoreDuringAnimation && IsPlaying) return;

        _tween?.Kill();
        transform.localScale = _originalScale;

        ResolvedParams p = Resolve();

        IsPlaying = true;
        OnBounceStarted?.Invoke();

        _tween = transform
            .DOPunchScale(p.Punch, p.Duration, p.Vibrato, p.Elasticity)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                transform.localScale = _originalScale;
                IsPlaying = false;
                OnBounceCompleted?.Invoke();
            });
    }

    // ── 내부 로직 ─────────────────────────────────────────────────────────────

    private readonly struct ResolvedParams
    {
        public readonly Vector3 Punch;
        public readonly float   Duration;
        public readonly int     Vibrato;
        public readonly float   Elasticity;

        public ResolvedParams(Vector3 punch, float duration, int vibrato, float elasticity)
        {
            Punch      = punch;
            Duration   = duration;
            Vibrato    = vibrato;
            Elasticity = elasticity;
        }
    }

    private ResolvedParams Resolve()
    {
        return _preset switch
        {
            BouncePreset.Soft   => new ResolvedParams(Vector3.one * 0.12f, 0.45f, 1, 0.8f),
            BouncePreset.Snappy => new ResolvedParams(Vector3.one * 0.22f, 0.30f, 1, 0.4f),
            BouncePreset.Jelly  => new ResolvedParams(Vector3.one * 0.18f, 0.60f, 3, 1.0f),
            _                   => new ResolvedParams(_punch, _duration, _vibrato, _elasticity),
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_duration <= 0f)
            Debug.LogWarning("[ClickScaleBounce] _duration은 0보다 커야 합니다.");
    }
#endif
}
