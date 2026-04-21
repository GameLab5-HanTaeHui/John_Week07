/// <summary>
/// 패배 상태입니다. 게임 종료 다이얼로그가 끝난 후 진입합니다.
/// </summary>
public class LoseState : IState
{
    private readonly LoopStateMachine _loopSM;

    public LoseState(LoopStateMachine loopSM)
    {
        _loopSM = loopSM;
    }

    public void Enter()
    {
        _loopSM.FireGameEnded(false);
    }

    public void Tick() { }
    public void Exit() { }
}
