using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 3턴이 완료된 뒤 루프를 정리하는 단계입니다.
    /// 정리 완료 후 CampaignLoopStateMachine.AdvanceLoop()를 호출합니다.
    /// </summary>
    public class LoopEndState : IState
    {
        private readonly CampaignLoopStateMachine _loopSM;

        public LoopEndState(CampaignLoopStateMachine loopSM)
        {
            _loopSM = loopSM;
        }

        public void Enter()
        {
            Debug.Log("[LoopEndState] Enter → AdvanceLoop 호출");
            _loopSM.AdvanceLoop();
        }

        public void Tick() { }
        public void Exit() { }
    }

    /// <summary>
    /// 루프를 모두 소진한 뒤 최종 결정 진입을 대기하는 단계입니다.
    /// 캠페인 모드에서는 AdvanceLoop()가 루프를 무한 반복하므로
    /// 일반적으로 이 상태에 진입하지 않습니다.
    /// CampaignHoldToEnterFinalDecision으로만 FinalDecision 진입 가능합니다.
    /// </summary>
    public class AwaitingFinalDecisionState : IState
    {
        private readonly CampaignLoopStateMachine _loopSM;

        public AwaitingFinalDecisionState(CampaignLoopStateMachine loopSM)
        {
            _loopSM = loopSM;
        }

        public void Enter() { }
        public void Tick() { }
        public void Exit() { }
    }

    /// <summary>
    /// 플레이어가 최종 결정(역할 추리 제출)을 수행하는 단계입니다.
    ///
    /// 진입 경로:
    ///   1. PlayerAction 단계에서 CampaignHoldToEnterFinalDecision 완료 시
    ///   2. AwaitingFinalDecisionState에서 완료 시 (캠페인에서는 드묾)
    /// </summary>
    public class FinalDecisionState : IState
    {
        private readonly CampaignLoopStateMachine _loopSM;

        public FinalDecisionState(CampaignLoopStateMachine loopSM)
        {
            _loopSM = loopSM;
        }

        public void Enter() => _loopSM.FireFinalDecisionEntered();
        public void Tick() { }
        public void Exit() => _loopSM.FireFinalDecisionExited();

        /// <summary>플레이어가 최종 결정을 제출했을 때 FinalDecisionUI에서 호출합니다.</summary>
        public void SubmitDecision(bool isWin) => _loopSM.EnterGameEnd(isWin);
    }

    /// <summary>
    /// 승리 상태입니다. 캠페인에서는 WinState 유지 중에도 캐릭터 이동/조각 수집이 가능합니다.
    /// </summary>
    public class WinState : IState
    {
        private readonly CampaignLoopStateMachine _loopSM;

        public WinState(CampaignLoopStateMachine loopSM)
        {
            _loopSM = loopSM;
        }

        public void Enter()
        {
            if (!string.IsNullOrEmpty(_loopSM.StageId))
                StageClearRepository.Instance.RecordClear(_loopSM.StageId);
            _loopSM.FireGameEnded(true);
        }

        public void Tick() { }
        public void Exit() { }
    }

    /// <summary>
    /// 패배 상태입니다. CampaignGameFlowController가 구독해 로비로 이동합니다.
    /// </summary>
    public class LoseState : IState
    {
        private readonly CampaignLoopStateMachine _loopSM;

        public LoseState(CampaignLoopStateMachine loopSM)
        {
            _loopSM = loopSM;
        }

        public void Enter() => _loopSM.FireGameEnded(false);
        public void Tick() { }
        public void Exit() { }
    }

    /// <summary>
    /// 게임 결과를 표시하고 게임을 종료하는 최종 단계입니다.
    /// </summary>
    public class GameEndState : IState
    {
        private readonly CampaignLoopStateMachine _loopSM;

        public GameEndState(CampaignLoopStateMachine loopSM)
        {
            _loopSM = loopSM;
        }

        public void Enter() => _loopSM.FireGameEnded(_loopSM.IsWin);
        public void Tick() { }
        public void Exit() { }
    }
}