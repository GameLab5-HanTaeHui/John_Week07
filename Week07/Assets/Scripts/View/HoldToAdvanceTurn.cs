using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 꾹 누르면 _fillObject가 활성화되며 점점 커지고, 가득 차면 턴을 넘깁니다.
/// 손을 떼면 게이지가 서서히 감소합니다.
/// 이번 턴에 한 명도 이동하지 않은 경우 홀드가 차단되고 _blockText가 위로 떠오릅니다.
///
/// Inspector 필수 연결:
///   FillObject    → 홀드 진행도를 표현할 GameObject (Scale 0→1로 커짐)
///   HoldDuration  → 가득 차는 데 걸리는 시간(초)
///   DrainSpeed    → 손을 뗐을 때 감소 배율 (2 = 채울 때의 2배 속도로 감소)
///   BlockText     → 이동 없을 때 표시할 World Space TextMeshPro
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HoldToAdvanceTurn : MonoBehaviour
{
    [SerializeField] private GameObject  _fillObject;
    [SerializeField] private float       _holdDuration = 1.5f;
    [SerializeField] private float       _drainSpeed   = 2f;

    [Header("이동 없음 차단 피드백")]
    [SerializeField] private TextMeshPro _blockText;
    [SerializeField] private float       _blockTextFloatHeight = 1f;
    [SerializeField] private float       _blockTextDuration    = 1f;

    private Vector3 _fullScale;
    private float   _holdTimer;
    private bool    _isHolding;
    private bool    _isDraining;
    private bool    _triggered;

    private Vector3   _blockTextOriginLocal;
    private Coroutine _blockTextCoroutine;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_fillObject != null)
        {
            _fullScale = _fillObject.transform.localScale;
            _fillObject.transform.localScale = Vector3.zero;
        }

        if (_blockText != null)
        {
            _blockTextOriginLocal = _blockText.transform.localPosition;
            _blockText.gameObject.SetActive(false);
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
        if (TutorialManager.IsActive && !TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.AdvanceTurn))
            return;

        var playerAction = GameFlowController.Instance?.GetPlayerActionState();
        if (playerAction != null && !playerAction.HasAnyMove)
        {
            ShowBlockText();
            return;
        }

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
        GameFlowController.Instance?.ForceEndTurn();
        Invoke(nameof(HideFill), 0.15f);
    }

    private void HideFill()
    {
        _holdTimer = 0f;
        if (_fillObject != null)
            _fillObject.transform.localScale = Vector3.zero;
    }

    private void SetFill(float t)
    {
        if (_fillObject == null) return;
        _fillObject.transform.localScale = _fullScale * t;
    }

    private void ShowBlockText()
    {
        if (_blockText == null) return;

        if (_blockTextCoroutine != null)
            StopCoroutine(_blockTextCoroutine);

        _blockTextCoroutine = StartCoroutine(FloatBlockText());
    }

    private IEnumerator FloatBlockText()
    {
        _blockText.transform.localPosition = _blockTextOriginLocal;

        var color = _blockText.color;
        color.a = 1f;
        _blockText.color = color;
        _blockText.gameObject.SetActive(true);

        Vector3 startLocal = _blockTextOriginLocal;
        Vector3 endLocal   = startLocal + Vector3.up * _blockTextFloatHeight;
        float elapsed = 0f;

        while (elapsed < _blockTextDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _blockTextDuration;
            _blockText.transform.localPosition = Vector3.Lerp(startLocal, endLocal, t);
            color.a = Mathf.Lerp(1f, 0f, t);
            _blockText.color = color;
            yield return null;
        }

        _blockText.gameObject.SetActive(false);
        _blockText.transform.localPosition = _blockTextOriginLocal;
        color.a = 1f;
        _blockText.color = color;
        _blockTextCoroutine = null;
    }
}
