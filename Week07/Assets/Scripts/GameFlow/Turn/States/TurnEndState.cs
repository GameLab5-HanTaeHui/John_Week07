using System.Collections.Generic;
using UnityEngine;

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

    // ★ 추가 — 자체 턴 카운터
    private int _turnCount = 0;

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
        Debug.Log($"[TurnEndState] Finish — isLoopCondition={_isLoopCondition}");

        if (_isLoopCondition)
        {
            _turnCount = 0; // 강제 퇴고 시 리셋
            _turnSM.TriggerLoopCondition();
            return;
        }

        // ★ 캠페인 모드에서 자체 턴 카운터로 3턴 체크
        if (HTH.Campaign.CampaignModeManager.IsPhase2Active)
        {
            _turnCount++;
            Debug.Log($"[TurnEndState] Phase2 턴 카운트={_turnCount}");

            if (_turnCount >= LoopStateMachine.TurnsPerLoop)
            {
                _turnCount = 0; // 퇴고 후 리셋
                Debug.Log("[TurnEndState] Phase2 — 3턴 완료, 퇴고 발동");
                _turnSM.TriggerLoopCondition();
                return;
            }
        }

        _turnSM.CompleteTurn();
    }

    public void Tick() { }
    public void Exit() { }
}
