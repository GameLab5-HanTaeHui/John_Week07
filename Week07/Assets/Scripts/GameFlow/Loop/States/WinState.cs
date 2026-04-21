/// <summary>
/// 승리 상태입니다. 게임 종료 다이얼로그가 끝난 후 진입합니다.
/// </summary>
public class WinState : IState
{
    private readonly LoopStateMachine _loopSM;

    public WinState(LoopStateMachine loopSM)
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
