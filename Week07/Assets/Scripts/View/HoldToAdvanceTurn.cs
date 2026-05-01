using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using HTH;

/// <summary>
/// 턴 넘기기 버튼 컴포넌트입니다. 기본모드 전용입니다.
/// 캠페인 모드는 CampaignHoldToAdvanceTurn을 사용하세요.
///
/// ─── 동작 흐름 ────────────────────────────────────────────────────────────
///   1. 플레이어가 버튼을 클릭합니다.
///   2. _fillDuration 동안 Fill 오브젝트가 애니메이션으로 올라갑니다.
///   3. Fill이 가득 차면 공유 확인 패널(ConfirmPanel)이 표시됩니다.
///   4. 확인 → GameFlowController.ForceEndTurn() 호출, 다음 턴으로 이동합니다.
///      취소 → Fill이 초기화되고 현재 상태를 유지합니다.
///
/// ─── 차단 조건 ────────────────────────────────────────────────────────────
///   - 이번 턴에 한 명도 이동하지 않은 경우 (HasAnyMove = false)
///     → Fill 애니메이션 없이 _blockText가 떠오르는 피드백만 표시합니다.
///   - 튜토리얼 진행 중 AdvanceTurn 권한이 없는 경우
///     → 입력 자체를 무시합니다.
///
/// ─── Inspector 설정 ───────────────────────────────────────────────────────
///   FillObject      → Fill 연출용 GameObject (DOScale 0 → fullScale)
///   FillDuration    → Fill이 올라가는 시간(초). 기본값 0.5초.
///   BlockText       → 이동 없을 때 표시할 World Space TextMeshPro
///   ConfirmMessage  → 확인 패널에 표시할 메시지 텍스트
///
/// ─── 외부 연결 ────────────────────────────────────────────────────────────
///   MapObjectInputHandler → 클릭 시 BeginHold() 호출
///   ConfirmPanel          → 공유 확인 패널 (싱글톤)
///   GameFlowController    → ForceEndTurn() 호출
///   TutorialManager       → 입력 권한 체크
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HoldToAdvanceTurn : MonoBehaviour
{
    [SerializeField] private GameObject _fillObject;
    [SerializeField] private float _fillDuration = 0.5f;

    [Header("이동 없음 차단 피드백")]
    [SerializeField] private TextMeshPro _blockText;
    [SerializeField] private float _blockTextFloatHeight = 1f;
    [SerializeField] private float _blockTextDuration = 1f;

    [Header("확인 패널 메시지")]
    [SerializeField] private string _confirmMessage = "대화를 시작 하겠습니까?";
    [SerializeField] private string _confirmEnterMessage = "대화하기";
    [SerializeField] private string _confirmUndoMessage = "더 생각하기";

    private Vector3 _fullScale;
    private bool _triggered;
    private Tween _fillTween;
    private Vector3 _blockTextOriginLocal;
    private Coroutine _blockTextCoroutine;

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

    private void OnDestroy()
    {
        _fillTween?.Kill();
    }

    // ── 외부 API ─────────────────────────────────────────────────────────────

    public void BeginHold()
    {
        if (_triggered) return;

        if (!TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.AdvanceTurn))
            return;

        var playerAction = GameFlowController.Instance?.GetPlayerActionState();
        if (playerAction != null && !playerAction.HasAnyMove)
        {
            ShowBlockText();
            return;
        }

        PlayFillAnimation();
    }

    public void EndHold() { }

    // ── Private ──────────────────────────────────────────────────────────────

    private void PlayFillAnimation()
    {
        if (_fillObject == null) return;

        _fillTween?.Kill();
        _fillObject.transform.localScale = Vector3.zero;

        _fillTween = _fillObject.transform
            .DOScale(_fullScale, _fillDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(OnFillComplete);
    }

    private void OnFillComplete()
    {
        _triggered = true;

        ConfirmPanel.Instance?.Show(_confirmMessage, _confirmEnterMessage, _confirmUndoMessage,
            onConfirm: () =>
            {
                _triggered = false;
                HideFill();
                GameFlowController.Instance?.ForceEndTurn();
            },
            onCancel: () =>
            {
                _triggered = false;
                HideFill();
            });
    }

    private void HideFill()
    {
        _fillTween?.Kill();
        if (_fillObject == null) return;

        _fillTween = _fillObject.transform
            .DOScale(Vector3.zero, _fillDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                if (_fillObject != null)
                    _fillObject.transform.localScale = Vector3.zero;
            });
    }

    private void ShowBlockText()
    {
        if (_blockText == null) return;
        if (_blockTextCoroutine != null) StopCoroutine(_blockTextCoroutine);
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
        Vector3 endLocal = startLocal + Vector3.up * _blockTextFloatHeight;
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