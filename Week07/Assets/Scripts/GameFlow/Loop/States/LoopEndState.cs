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
        // TODO: 루프 종료 연출 또는 UI 표시 (필요 시 비동기 처리)
        _loopSM.AdvanceLoop(); // 연출이 추가되면 연출 완료 콜백으로 이동
    }

    public void Tick() { }
    public void Exit() { }
}
