using System.Collections.Generic;

/// <summary>
/// 턴 종료 단계입니다.
/// Enter() 시 OnTurnEndEntered 이벤트를 발생시켜 DialogueManager에 다이어로그 재생을 위임합니다.
/// CompleteTurn()은 DialogueManager가 재생을 마친 뒤 Finish()를 통해 호출됩니다.
/// </summary>
public class TurnEndState : IState
{
    private readonly TurnStateMachine _turnSM;

    private IReadOnlyList<string> _eventLog;
    private bool                  _isLoopCondition;

    public TurnEndState(TurnStateMachine turnSM)
    {
        _turnSM = turnSM;
    }

    /// <summary>RoleActivationState가 상태 전환 직전에 컨텍스트를 주입합니다.</summary>
    public void SetContext(IReadOnlyList<string> eventLog, bool isLoopCondition)
    {
        _eventLog        = eventLog;
        _isLoopCondition = isLoopCondition;
    }

    public void Enter()
    {
        // DialogueManager가 이 이벤트를 받아 다이어로그를 재생합니다.
        // 재생이 끝나면 GameFlowController.FinishTurnEnd() → Finish()가 호출됩니다.
        _turnSM.FireTurnEndEntered(_eventLog, _isLoopCondition);
    }

    /// <summary>
    /// DialogueManager의 재생 완료 후 GameFlowController.FinishTurnEnd()를 통해 호출됩니다.
    /// 루프 조건이면 TriggerLoopCondition, 아니면 CompleteTurn으로 분기합니다.
    /// </summary>
    public void Finish()
    {
        if (_isLoopCondition)
            _turnSM.TriggerLoopCondition();
        else
            _turnSM.CompleteTurn();
    }

    public void Tick() { }
    public void Exit() { }
}
