using UnityEngine;

/// <summary>
/// 지정한 횟수만큼 클릭하면 파티클을 Play합니다.
///
/// ─── ClickScaleBounce와 연동 ────────────────────────────────────────────
///   같은 GameObject에 ClickScaleBounce가 있으면 Awake에서 자동 구독합니다.
///   없을 경우 RegisterClick()을 코드에서 직접 호출하세요.
/// </summary>
[DisallowMultipleComponent]
public class ClickParticleBurst : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("파티클")]
    [Tooltip("Play할 ParticleSystem. 비워두면 이 GameObject의 ParticleSystem을 사용합니다.")]
    [SerializeField] private ParticleSystem _particle;

    [Header("발동 조건")]
    [Tooltip("이 횟수만큼 클릭하면 파티클이 Play됩니다.")]
    [SerializeField] private int _clicksRequired = 5;
    [Tooltip("파티클 발동 후 카운터를 초기화합니다.")]
    [SerializeField] private bool _resetAfterBurst = true;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    private int               _clickCount;
    private ClickScaleBounce  _bounce;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_particle == null)
            TryGetComponent(out _particle);

        if (TryGetComponent(out _bounce))
            _bounce.OnBounceStarted += RegisterClick;
    }

    private void OnDestroy()
    {
        if (_bounce != null)
            _bounce.OnBounceStarted -= RegisterClick;
    }

    // ── 외부 API ──────────────────────────────────────────────────────────────

    /// <summary>클릭 1회를 등록합니다. 목표 횟수 도달 시 파티클을 Play합니다.</summary>
    public void RegisterClick()
    {
        _clickCount++;
        if (_clickCount < _clicksRequired) return;

        if (_particle != null) _particle.Play();
        if (_resetAfterBurst) _clickCount = 0;
    }

    // ── Editor 방어 ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_clicksRequired < 1)
            Debug.LogWarning("[ClickParticleBurst] _clicksRequired는 1 이상이어야 합니다.");
    }
#endif
}
