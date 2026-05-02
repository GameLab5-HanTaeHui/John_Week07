using DG.Tweening;
using UnityEngine;
using HTH;

/// <summary>
/// 최종 추리 진입 버튼 컴포넌트입니다. 기본모드 전용입니다.
/// 캠페인 모드는 CampaignHoldToEnterFinalDecision을 사용하세요.
///
/// ─── 동작 흐름 ────────────────────────────────────────────────────────────
///   1. 플레이어가 버튼을 클릭합니다.
///   2. _fillDuration 동안 Fill 오브젝트가 애니메이션으로 올라갑니다.
///   3. Fill이 가득 차면 공유 확인 패널(ConfirmPanel)이 표시됩니다.
///   4. 확인 → GameFlowController.EnterFinalDecision() 호출.
///      취소 → Fill이 초기화되고 현재 상태를 유지합니다.
///
/// ─── 진입 가능 조건 ───────────────────────────────────────────────────────
///   LoopState가 AwaitingFinalDecision 또는
///   RunningTurn + TurnState가 PlayerAction 일 때만 유효합니다.
///
/// ─── Inspector 설정 ───────────────────────────────────────────────────────
///   FillObject      → Fill 연출용 GameObject
///   FillDuration    → Fill 애니메이션 시간(초). 기본값 0.5초.
///   ConfirmMessage  → 확인 패널에 표시할 메시지 텍스트
///
/// ─── 외부 연결 ────────────────────────────────────────────────────────────
///   MapObjectInputHandler → 클릭 시 BeginHold() 호출
///   ConfirmPanel          → 공유 확인 패널 (싱글톤)
///   GameFlowController    → EnterFinalDecision() / CanEnterFinalDecision 체크
///   TutorialManager       → 입력 권한 체크
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HoldToEnterFinalDecision : MonoBehaviour
{
    [SerializeField] private GameObject _fillObject;
    [SerializeField] private GameObject _finalPanel;
    [SerializeField] private float _fillDuration = 0.5f;

    [Header("확인 패널 메시지")]
    [SerializeField] private string _confirmMessage = "최종 추리를 시작하시겠습니까?";
    [SerializeField] private string _confirmEnterMessage = "추리 하기";
    [SerializeField] private string _confirmUndoMessage = "더 생각하기";

    private Vector3 _fullScale;
    private bool _triggered;
    private Tween _fillTween;

    private void Awake()
    {
        if (_fillObject != null)
        {
            _fullScale = _fillObject.transform.localScale;
            _fillObject.transform.localScale = Vector3.zero;
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
        if (!CanActivate()) return;

        // [수정됨] 최종 추리 집필 권한이 없다면 게이지가 오르지 않도록 차단
        if (!TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.FinalDecisionEnter))
            return;

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

                _finalPanel.SetActive(true);
                // 튜토리얼 매니저에게 "최종 추리 방에 들어왔음"을 명시적으로 보고합니다!
                if (TutorialManager.IsActive)
                {
                    TutorialManager.Instance.HandleFinalDecisionEntered();
                }
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

    private bool CanActivate()
    {
        var gfc = GameFlowController.Instance;
        return gfc != null && gfc.CanEnterFinalDecision;
    }
}