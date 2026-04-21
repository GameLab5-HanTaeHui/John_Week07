/// <summary>
/// 플레이어가 최종 결정(역할 추리 제출)을 수행하는 단계입니다.
///
/// 진입 경로:
///   1. 루프 소진 후 AwaitingFinalDecisionState에서 HoldToEnterFinalDecision 완료 시
///   2. PlayerAction 단계에서 HoldToEnterFinalDecision 조기 완료 시
/// </summary>
public class FinalDecisionState : IState
{
    private readonly LoopStateMachine _loopSM;

    public FinalDecisionState(LoopStateMachine loopSM)
    {
        _loopSM = loopSM;
    }

    public void Enter()
    {
        _loopSM.FireFinalDecisionEntered();
    }

    public void Tick() { }

    public void Exit()
    {
        _loopSM.FireFinalDecisionExited();
    }

    /// <summary>플레이어가 최종 결정을 제출했을 때 FinalDecisionUI에서 호출합니다.</summary>
    public void SubmitDecision(bool isWin)
    {
        _loopSM.EnterGameEnd(isWin);
    }
}
