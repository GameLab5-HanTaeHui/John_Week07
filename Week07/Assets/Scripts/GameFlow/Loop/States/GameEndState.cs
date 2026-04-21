/// <summary>
/// 게임 결과를 표시하고 게임을 종료하는 최종 단계입니다.
/// LoopStateMachine.IsWin으로 정답 여부를 확인할 수 있습니다.
/// </summary>
public class GameEndState : IState
{
    private readonly LoopStateMachine _loopSM;

    public GameEndState(LoopStateMachine loopSM)
    {
        _loopSM = loopSM;
    }

    public void Enter()
    {
        _loopSM.FireGameEnded(_loopSM.IsWin);
    }

    public void Tick() { }
    public void Exit() { }
}
