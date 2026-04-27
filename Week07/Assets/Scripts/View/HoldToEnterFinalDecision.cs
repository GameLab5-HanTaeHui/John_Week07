using DG.Tweening;
using UnityEngine;
using HTH;
/// <summary>
/// 최종 추리 진입 버튼 컴포넌트입니다.
///
/// ─── 동작 흐름 ────────────────────────────────────────────────────────────
///   1. 플레이어가 버튼을 클릭합니다.
///   2. _fillDuration 동안 Fill 오브젝트가 애니메이션으로 올라갑니다.
///   3. Fill이 가득 차면 공유 확인 패널(ConfirmPanel)이 표시됩니다.
///   4. 확인 → GameFlowController.EnterFinalDecision() 호출, 최종 추리로 진입합니다.
///      취소 → Fill이 초기화되고 현재 상태를 유지합니다.
///
/// ─── 진입 가능 조건 (CanActivate) ─────────────────────────────────────────
///   - LoopState가 AwaitingFinalDecision 상태이거나
///   - LoopState가 RunningTurn이고 TurnState가 PlayerAction 상태일 때만 유효합니다.
///   - 그 외 상태에서의 클릭은 무시됩니다.
///
/// ─── 차단 조건 ────────────────────────────────────────────────────────────
///   - 튜토리얼 진행 중 EnterFinalDecision 권한이 없는 경우
///     → 입력 자체를 무시합니다.
///
/// ─── Inspector 설정 ───────────────────────────────────────────────────────
///   FillObject      → Fill 연출용 GameObject (DOScale 0 → fullScale)
///   FillDuration    → Fill이 올라가는 시간(초). 기본값 0.5초.
///   ConfirmMessage  → 확인 패널에 표시할 메시지 텍스트
///
/// ─── 외부 연결 ────────────────────────────────────────────────────────────
///   MapObjectInputHandler          → 클릭 시 BeginHold() 호출
///   ConfirmPanel                   → 공유 확인 패널 (싱글톤)
///   GameFlowController             → EnterFinalDecision() / CanEnterFinalDecision 체크
///   TutorialManager                → 입력 권한 체크
///   ProfileInquirySelectPanel      → Phase2 캐릭터 선택 패널
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HoldToEnterFinalDecision : MonoBehaviour
{
    [SerializeField] private GameObject _fillObject;
    [SerializeField] private float _fillDuration = 0.5f;

    [Header("확인 패널 메시지")]
    [SerializeField] private string _confirmMessage = "최종 추리를 시작하시겠습니까?";
    [SerializeField] private string _confirmMessagePhase2 = "인물 추리를 시작하시겠습니까?";

    [Header("Phase2 연결")]
    [Tooltip("Phase2에서 캐릭터를 선택하는 패널입니다.")]
    [SerializeField] private HTH.Campaign.ProfileInquirySelectPanel _profileInquirySelectPanel;

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

    // ── 외부 API (MapObjectInputHandler에서 호출) ─────────────────────────────

    public void BeginHold()
    {
        if (_triggered) return;
        if (!CanActivate()) return;

        if (TutorialManager.IsActive &&
            !TutorialManager.Instance.IsInputAllowed(TutorialInputPermission.EnterFinalDecision))
            return;

        // 튜토리얼 중 책 클릭 시 화살표 숨김 요청
        if (TutorialManager.IsActive)
            TutorialManager.Instance?.NotifyFinalDecisionBookClicked();

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

        // Phase2 여부에 따라 메시지 분기
        string message = HTH.Campaign.CampaignModeManager.IsPhase2Active
            ? _confirmMessagePhase2
            : _confirmMessage;

        ConfirmPanel.Instance?.Show(message, onConfirm: () =>
            {
                _triggered = false;
                HideFill();

                // 튜토리얼 중에는 최종 추리 진입 차단
                if (TutorialManager.IsActive)
                {
                    Debug.Log("[HoldToEnterFinalDecision] 튜토리얼 중 — 최종 추리 진입 차단");
                    TutorialManager.Instance?.RestoreClickAdvanceAfterBlock();
                    return;
                }

                // Phase2: 캐릭터 선택 패널 표시
                if (HTH.Campaign.CampaignModeManager.IsPhase2Active)
                {
                    if (_profileInquirySelectPanel != null)
                        _profileInquirySelectPanel.Show();
                    else
                        Debug.LogWarning("[HoldToEnterFinalDecision] ProfileInquirySelectPanel이 연결되지 않았습니다.");
                    return;
                }

                // Phase1: 기존 최종 추리
                GameFlowController.Instance?.EnterFinalDecision();
            },
            onCancel: () =>
            {
                _triggered = false;
                HideFill();

                if (TutorialManager.IsActive)
                    TutorialManager.Instance?.NotifyFinalDecisionCancelled();
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