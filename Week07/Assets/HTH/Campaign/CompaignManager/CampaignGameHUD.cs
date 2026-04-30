using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 메인 HUD입니다.
    /// 기본모드는 GameHUD를 사용하세요.
    ///
    /// ─── 기본모드 GameHUD와의 차이 ───────────────────────────────────────
    ///   참조 대상: CampaignGameFlowController (기본: GameFlowController)
    ///   MaxLoops 참조: CampaignLoopStateMachine (기본: LoopStateMachine)
    ///   버튼(_endTurnButton / _deductionButton) 제거
    ///     → 입력은 CampaignHoldToAdvanceTurn / CampaignHoldToEnterFinalDecision이 담당
    ///   TurnStateMachine 이벤트 구독 제거 (버튼 활성/비활성 불필요)
    ///
    /// ─── Canvas 구조 ─────────────────────────────────────────────────────
    ///   Canvas
    ///   └── TopBar
    ///       ├── LoopTurnText  (TMP) ← LoopTurnText 연결
    ///       └── PhaseText     (TMP) ← PhaseText 연결
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignGameHUD : MonoBehaviour
    {
        [Header("루프 · 턴 정보")]
        [SerializeField] private TMP_Text _loopTurnText;
        [SerializeField] private TMP_Text _phaseText;

        [Header("시간대 이미지 (아침 · 점심 · 저녁 순서로 연결)")]
        [SerializeField] private Image[] _dayPhaseImages = new Image[3];

        [Header("턴 넘기기 텍스트")]
        [SerializeField] private TMP_Text _nextTurnText;

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            var gfc = CampaignGameFlowController.Instance;
            if (gfc == null)
            {
                Debug.LogError("[CampaignGameHUD] CampaignGameFlowController를 찾을 수 없습니다.");
                enabled = false;
            }
        }

        private void Update()
        {
            RefreshLoopTurnText();
            RefreshPhaseText();
            RefreshDayPhaseImages();
            RefreshNextTurnText();
        }

        // ── Private ──────────────────────────────────────────────────────

        private void RefreshLoopTurnText()
        {
            if (_loopTurnText == null) return;
            var gfc = CampaignGameFlowController.Instance;
            if (gfc == null) return;
            int daysLeft = CampaignLoopStateMachine.MaxLoops - gfc.LoopCount + 1;
            _loopTurnText.text = $"마감일까지 {daysLeft}일";
        }

        private void RefreshPhaseText()
        {
            if (_phaseText == null) return;
            var gfc = CampaignGameFlowController.Instance;
            if (gfc == null) return;
            _phaseText.text = GetPhaseLabel(gfc.CurrentLoopState, gfc.TurnCount);
        }

        private void RefreshDayPhaseImages()
        {
            if (_dayPhaseImages == null || _dayPhaseImages.Length == 0) return;
            var gfc = CampaignGameFlowController.Instance;
            if (gfc == null) return;
            int activeIndex = gfc.TurnCount - 1;
            for (int i = 0; i < _dayPhaseImages.Length; i++)
                if (_dayPhaseImages[i] != null)
                    _dayPhaseImages[i].gameObject.SetActive(i == activeIndex);
        }

        private static string GetPhaseLabel(LoopStateType loop, int turnCount = 0)
        {
            return loop switch
            {
                LoopStateType.RunningTurn => turnCount switch
                {
                    1 => "아침",
                    2 => "점심",
                    3 => "저녁",
                    _ => ""
                },
                LoopStateType.AwaitingFinalDecision => "최종 결정 대기",
                LoopStateType.FinalDecision => "최종 결정",
                LoopStateType.GameEnd => "게임 종료",
                _ => ""
            };
        }

        private void RefreshNextTurnText()
        {
            if (_nextTurnText == null) return;
            var gfc = CampaignGameFlowController.Instance;
            if (gfc == null) return;
            _nextTurnText.text = gfc.TurnCount switch
            {
                1 => "점심으로 가기",
                2 => "저녁으로 가기",
                3 => "퇴고하기",
                _ => ""
            };
        }
    }
}