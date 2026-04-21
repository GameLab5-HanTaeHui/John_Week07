using DG.Tweening;
using UnityEngine;

/// <summary>
/// 평소에는 최소 속도로 X축 회전하다가, 클릭 시 가속되고 서서히 평속으로 복귀합니다.
///
/// ─── ClickScaleBounce와 연동 ────────────────────────────────────────────
///   같은 GameObject에 ClickScaleBounce가 있으면 Awake에서 자동 구독합니다.
///   없을 경우 AddSpin()을 코드에서 직접 호출하세요.
/// </summary>
[DisallowMultipleComponent]
public class ClickSpinRotation : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("대상")]
    [Tooltip("회전시킬 자식 Transform. 비워두면 이 GameObject 자신을 사용합니다.")]
    [SerializeField] private Transform _target;

    [Header("속도 설정 (도/초)")]
    [Tooltip("평상시 유지 회전 속도")]
    [SerializeField] private float _minVelocity      = 30f;
    [Tooltip("클릭 1회당 추가되는 각속도")]
    [SerializeField] private float _velocityPerClick = 200f;
    [Tooltip("최대 각속도 제한")]
    [SerializeField] private float _maxVelocity      = 720f;
    [Tooltip("가속 후 평속으로 되돌아오는 감속 (도/초²)")]
    [SerializeField] private float _drag             = 150f;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────

    private float            _angularVelocity;
    private float            _currentAngleX;
    private ClickScaleBounce _bounce;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_target == null)
            _target = transform;

        _angularVelocity = _minVelocity;

        if (TryGetComponent(out _bounce))
            _bounce.OnBounceStarted += AddSpin;
    }

    private void OnDestroy()
    {
        if (_bounce != null)
            _bounce.OnBounceStarted -= AddSpin;
    }

    private void Update()
    {
        _currentAngleX += _angularVelocity * Time.deltaTime;
        _target.localRotation = Quaternion.Euler(_currentAngleX, 0f, 0f);

        // 평속으로 감속
        _angularVelocity = Mathf.MoveTowards(_angularVelocity, _minVelocity, _drag * Time.deltaTime);
    }

    // ── 외부 API ──────────────────────────────────────────────────────────────

    /// <summary>클릭 1회 분량의 각속도를 누적합니다.</summary>
    public void AddSpin()
    {
        _angularVelocity = Mathf.Clamp(
            _angularVelocity + _velocityPerClick,
            _minVelocity,
            _maxVelocity);
    }

    // ── Editor 방어 ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_minVelocity < 0f)
            Debug.LogWarning("[ClickSpinRotation] _minVelocity는 0 이상이어야 합니다.");
        if (_maxVelocity <= _minVelocity)
            Debug.LogWarning("[ClickSpinRotation] _maxVelocity는 _minVelocity보다 커야 합니다.");
        if (_drag <= 0f)
            Debug.LogWarning("[ClickSpinRotation] _drag는 0보다 커야 합니다.");
    }
#endif
}
