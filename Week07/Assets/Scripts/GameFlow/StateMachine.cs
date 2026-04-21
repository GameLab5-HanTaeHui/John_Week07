/// <summary>
/// LoopStateMachine / TurnStateMachine 공통 베이스 클래스입니다.
/// 상태 전환(ChangeState)과 매 Tick 위임을 담당합니다.
/// 상태 전환은 반드시 파생 클래스의 전환 메서드를 통해서만 호출하세요.
/// </summary>
public abstract class StateMachine
{
    private IState _current;

    /// <summary>
    /// 현재 상태를 종료하고 다음 상태로 전환합니다.
    /// 파생 클래스의 전환 메서드에서만 호출하세요.
    /// </summary>
    protected void ChangeState(IState next)
    {
        _current?.Exit();
        _current = next;
        _current?.Enter();
    }

    /// <summary>현재 상태의 Tick을 실행합니다. GameFlowController.Update()에서 호출합니다.</summary>
    public void Tick()
    {
        _current?.Tick();
    }
}
