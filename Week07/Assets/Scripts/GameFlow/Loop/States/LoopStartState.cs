/// <summary>
/// 각 루프의 시작 단계입니다.
/// 2루프 이상의 GameState 재생성은 GameSetupState에서 처리하므로
/// 이 상태는 RunningTurn으로 즉시 전환합니다.
/// </summary>
public class LoopStartState : IState
{
    private readonly LoopStateMachine _loopSM;

    public LoopStartState(LoopStateMachine loopSM)
    {
        _loopSM = loopSM;
    }

    public void Enter() => _loopSM.EnterRunningTurn();

    public void Tick() { }
    public void Exit() { }
}
