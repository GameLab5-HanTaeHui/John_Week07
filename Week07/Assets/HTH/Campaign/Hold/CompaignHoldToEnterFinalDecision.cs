using DG.Tweening;
using UnityEngine;
using HTH;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 인물 추리 진입 버튼 컴포넌트입니다.
    /// 기본모드는 HoldToEnterFinalDecision을 사용하세요.
    ///
    /// ─── 기본모드와의 차이 ───────────────────────────────────────────────────
    ///   참조 대상: CampaignGameFlowController (기본: GameFlowController)
    ///   IsPhase2Active 체크 제거 — 캠페인 씬은 항상 Phase2
    ///   확인 후 ProfileInquirySelectPanel 직접 표시 (FinalDecision 진입 없음)
    ///   CanActivate() — WinState 포함 항상 활성 (Phase2 조각 수집 루프)
    ///   TutorialManager 체크 제거
    ///
    /// ─── 동작 흐름 ────────────────────────────────────────────────────────────
    ///   1. 플레이어가 버튼을 클릭합니다.
    ///   2. _fillDuration 동안 Fill 오브젝트가 애니메이션으로 올라갑니다.
    ///   3. Fill이 가득 차면 공유 확인 패널(ConfirmPanel)이 표시됩니다.
    ///   4. 확인 → ProfileInquirySelectPanel.Show() 호출.
    ///      취소 → Fill이 초기화되고 현재 상태를 유지합니다.
    ///
    /// ─── Inspector 설정 ───────────────────────────────────────────────────────
    ///   FillObject                → Fill 연출용 GameObject
    ///   FillDuration              → Fill 애니메이션 시간(초). 기본값 0.5초.
    ///   ConfirmMessage            → 확인 패널 메시지
    ///   ProfileInquirySelectPanel → 캐릭터 선택 패널
    ///
    /// ─── 외부 연결 ────────────────────────────────────────────────────────────
    ///   MapObjectInputHandler         → 클릭 시 BeginHold() 호출
    ///   ConfirmPanel                  → 공유 확인 패널 (싱글톤)
    ///   ProfileInquirySelectPanel     → 캐릭터 선택 패널
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class CampaignHoldToEnterFinalDecision : MonoBehaviour
    {
        [SerializeField] private GameObject _fillObject;
        [SerializeField] private float _fillDuration = 0.5f;

        [Header("확인 패널 메시지")]
        [SerializeField] private string _confirmMessage = "인물 추리를 시작하시겠습니까?";

        [Header("캠페인 전용 — 캐릭터 선택 패널")]
        [Tooltip("확인 후 표시할 ProfileInquirySelectPanel입니다.")]
        [SerializeField] private ProfileInquirySelectPanel _profileInquirySelectPanel;

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

        // ── 외부 API ─────────────────────────────────────────────────────────

        public void BeginHold()
        {
            if (_triggered) return;
            // ★ 캠페인 씬은 항상 활성 — CanActivate 체크 불필요
            PlayFillAnimation();
        }

        public void EndHold() { }

        // ── Private ──────────────────────────────────────────────────────────

        private void PlayFillAnimation()
        {
            if (_fillObject == null) return;
            if (!_fillObject.activeSelf)
                _fillObject.SetActive(true);

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

            ConfirmPanel.Instance?.Show(_confirmMessage,
                onConfirm: () =>
                {
                    _triggered = false;
                    HideFill();

                    // ★ 캠페인 전용 — ProfileInquirySelectPanel 직접 표시
                    if (_profileInquirySelectPanel != null)
                        _profileInquirySelectPanel.Show();
                    else
                        Debug.LogWarning("[CampaignHoldToEnterFinalDecision] " +
                                         "ProfileInquirySelectPanel이 연결되지 않았습니다.");
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
                    if (_fillObject.activeSelf) 
                        _fillObject.SetActive(false);
                });
        }
    }
}