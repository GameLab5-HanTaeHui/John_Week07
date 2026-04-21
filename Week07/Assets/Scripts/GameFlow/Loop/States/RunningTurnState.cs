/// <summary>
/// LoopStateMachine과 TurnStateMachine을 연결하는 브릿지 상태입니다.
/// 한 루프 내 3개의 턴을 모두 처리하는 동안 이 상태를 유지합니다.
///
/// Enter : TurnStateMachine 이벤트 구독 + 첫 번째 턴 시작
/// Tick  : TurnStateMachine.Tick() 위임
/// Exit  : 이벤트 구독 해제 (메모리 누수 방지)
///
/// 이벤트 처리:
///   OnTurnCompleted     → LoopStateMachine.AdvanceTurn() (다음 턴 or LoopEnd)
///   OnDeductionDeclared → LoopStateMachine.EnterDeduction()
/// </summary>
public class RunningTurnState : IState
{
    private readonly LoopStateMachine _loopSM;
    private readonly TurnStateMachine _turnSM;

    public RunningTurnState(LoopStateMachine loopSM, TurnStateMachine turnSM)
    {
        _loopSM = loopSM;
        _turnSM = turnSM;
    }

    public void Enter()
    {
        _turnSM.OnTurnCompleted     += OnTurnCompleted;
        _turnSM.OnDeductionDeclared += OnDeductionDeclared;
        _turnSM.StartTurn();
    }

    public void Tick()
    {
        _turnSM.Tick();
    }

    public void Exit()
    {
        _turnSM.OnTurnCompleted     -= OnTurnCompleted;
        _turnSM.OnDeductionDeclared -= OnDeductionDeclared;
    }

    private void OnTurnCompleted()     => _loopSM.AdvanceTurn();
    private void OnDeductionDeclared() => _loopSM.EnterFinalDecision();
}
