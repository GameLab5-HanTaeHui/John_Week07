using UnityEngine;
/// <summary>
/// 3턴이 완료된 뒤 루프를 정리하는 단계입니다.
/// 정리 완료 후 LoopStateMachine.AdvanceLoop()를 호출해 다음 루프 또는 DeductionPhase로 전환합니다.
/// </summary>
public class LoopEndState : IState
{
    private readonly LoopStateMachine _loopSM;

    public LoopEndState(LoopStateMachine loopSM)
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
