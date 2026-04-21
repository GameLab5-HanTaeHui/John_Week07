using UnityEngine;

/// <summary>
/// 꾹 누르면 최종 결정(FinalDecision) 단계로 진입합니다.
/// 손을 떼면 게이지가 서서히 감소합니다.
///
/// Inspector 필수 연결:
///   FillObject    → 홀드 진행도를 표현할 GameObject (Scale 0→1로 커짐)
///   HoldDuration  → 가득 차는 데 걸리는 시간(초)
///   DrainSpeed    → 손을 뗐을 때 감소 배율 (2 = 채울 때의 2배 속도로 감소)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HoldToEnterFinalDecision : MonoBehaviour
{
    [SerializeField] private GameObject _fillObject;
    [SerializeField] private float      _holdDuration = 1.5f;
    [SerializeField] private float      _drainSpeed   = 2f;

    private Vector3 _fullScale;
    private float   _holdTimer;
    private bool    _isHolding;
    private bool    _isDraining;
    private bool    _triggered;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_fillObject != null)
        {
            _fullScale = _fillObject.transform.localScale;
            _fillObject.transform.localScale = Vector3.zero;
        }
    }

    private void Update()
    {
        if (_triggered) return;

        if (_isHolding)
        {
            _holdTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_holdTimer / _holdDuration);
            SetFill(t);
            if (t >= 1f) Trigger();
        }
        else if (_isDraining)
        {
            _holdTimer -= Time.deltaTime * _drainSpeed;
            if (_holdTimer <= 0f)
            {
                _holdTimer  = 0f;
                _isDraining = false;
                HideFill();
            }
            else
            {
                SetFill(_holdTimer / _holdDuration);
            }
        }
    }

    // ── 외부 API (MapObjectInputHandler에서 호출) ─────────────────────────────

    public void BeginHold()
    {
        if (!CanActivate()) return;
        if (TutorialManager.IsActive && !TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.EnterFinalDecision))
            return;
        _isDraining = false;
        _isHolding  = true;
        _triggered  = false;
    }

    public void EndHold()
    {
        if (_triggered) return;
        _isHolding = false;

        if (_holdTimer > 0f)
            _isDraining = true;
        else
            HideFill();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void Trigger()
    {
        _triggered  = true;
        _isHolding  = false;
        _isDraining = false;
        SetFill(1f);
        GameFlowController.Instance?.EnterFinalDecision();
        Invoke(nameof(HideFill), 0.15f);
    }

    private bool CanActivate()
    {
        var gfc = GameFlowController.Instance;
        return gfc != null && gfc.CanEnterFinalDecision;
    }

    private void HideFill()
    {
        _holdTimer = 0f;
        _triggered = false;
        if (_fillObject != null)
            _fillObject.transform.localScale = Vector3.zero;
    }

    private void SetFill(float t)
    {
        if (_fillObject == null) return;
        _fillObject.transform.localScale = _fullScale * t;
    }
}
