/// <summary>
/// 이번 턴에 발생한 결과를 플레이어에게 보여주는 단계입니다.
/// 플레이어가 확인 버튼을 누르면 ConfirmResult()를 통해 TurnEnd로 전환합니다.
/// </summary>
public class ResultDisplayState : IState
{
    private readonly TurnStateMachine _turnSM;

    public ResultDisplayState(TurnStateMachine turnSM)
    {
        _turnSM = turnSM;
    }

    public void Enter()
    {
        // TODO: 이번 턴 결과 UI 표시 (어떤 능력이 발동됐는지 등)
    }

    public void Tick() { }

    public void Exit()
    {
        // TODO: 결과 UI 닫기
    }

    /// <summary>결과 확인 UI 버튼에서 호출합니다.</summary>
    public void ConfirmResult()
    {
        _turnSM.EnterTurnEnd();
    }
}
