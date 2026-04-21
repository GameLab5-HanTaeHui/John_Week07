using System;
using System.Collections.Generic;

/// <summary>
/// 턴 내부 흐름을 관리하는 상태머신입니다.
/// LoopStateMachine의 RunningTurnState가 소유하며, Tick()을 위임받아 실행됩니다.
///
/// 이벤트:
///   OnTurnCompleted        - TurnEndState 완료 시 발생 → LoopStateMachine.AdvanceTurn() 호출
///   OnDeductionDeclared    - PlayerAction 단계에서 추리 선언 시 발생
///   OnPlayerActionStarted  - PlayerAction 단계 진입 시 발생 → GameHUD에서 구독
///   OnTurnEndEntered       - TurnEnd 단계 진입 시 발생 (이벤트 로그, 루프 리셋 여부 전달)
///                            → DialogueManager에서 구독해 다이어로그 재생
///
/// 흐름: PlayerAction → RoleActivation → TurnEnd(다이어로그) → CompleteTurn or TriggerLoopCondition
/// </summary>
public class TurnStateMachine : StateMachine
{
    public event Action                                    OnTurnCompleted;
    public event Action                                    OnDeductionDeclared;
    public event Action                                    OnPlayerActionStarted;
    public event Action<IReadOnlyList<string>, bool>       OnTurnEndEntered;

    /// <summary>
    /// 루프 종료 조건이 달성됐을 때 발생합니다.
    /// LoopStateMachine이 구독해 다음 루프로 강제 진행합니다.
    /// </summary>
    public event Action OnLoopConditionTriggered;

    public TurnStateType CurrentState { get; private set; }

    /// <summary>현재 PlayerActionState 인스턴스입니다. PlayerTurnInputHandler에서 이벤트 구독에 사용합니다.</summary>
    public PlayerActionState PlayerAction => _playerAction;

    private readonly PlayerActionState   _playerAction;
    private readonly RoleActivationState _roleActivation;
    private readonly TurnEndState        _turnEnd;

    /// <param name="orderConfig">직업 능력 발동 순서 설정 (GameFlowController에서 주입)</param>
    /// <param name="getGameState">런타임 GameState를 반환하는 델리게이트 (LoopStateMachine에서 주입)</param>
    /// <param name="historyRepo">턴 기록 저장소 (LoopStateMachine에서 주입)</param>
    /// <param name="getSeed">현재 루프 시드를 반환하는 델리게이트</param>
    /// <param name="getLoopIndex">현재 루프 인덱스를 반환하는 델리게이트</param>
    /// <param name="getTurnIndex">현재 턴 인덱스를 반환하는 델리게이트</param>
    /// <param name="getLoopCondition">현재 스테이지의 루프 종료 조건을 반환하는 델리게이트</param>
    public TurnStateMachine(
        RoleActivationOrderConfig orderConfig,
        Func<GameState>           getGameState,
        TurnHistoryRepository     historyRepo,
        Func<int>                 getSeed,
        Func<int>                 getLoopIndex,
        Func<int>                 getTurnIndex,
        Func<LoopConditionConfig> getLoopCondition = null)
    {
        _playerAction   = new PlayerActionState(this, getGameState);
        _roleActivation = new RoleActivationState(
            this, orderConfig, getGameState,
            historyRepo, getSeed, getLoopIndex, getTurnIndex, getLoopCondition);
        _turnEnd        = new TurnEndState(this);
    }

    // ── 입력 전달 (PlayerTurnInputHandler → GameFlowController → LoopSM → 여기) ──

    public void NotifyCharacterClicked(int characterId) => _playerAction.NotifyCharacterClicked(characterId);
    public void NotifyZoneClicked(int zoneId)           => _playerAction.NotifyZoneClicked(zoneId);
    public void ForceEndPlayerAction()                  => _playerAction.ForceEnd();

    /// <summary>
    /// 드래그 시작 시 캐릭터를 강제 선택합니다. NotifyCharacterClicked와 달리 재클릭=대기 로직을 타지 않습니다.
    /// characterId=-1이면 선택 해제.
    /// </summary>
    public void BeginDragSelect(int characterId)        => _playerAction.BeginDragSelect(characterId);

    // ── 상태 전환 메서드 (Turn State 클래스에서 호출) ───────────

    /// <summary>새 턴을 시작합니다. PlayerAction 상태로 진입합니다.</summary>
    public void StartTurn()
    {
        CurrentState = TurnStateType.PlayerAction;
        ChangeState(_playerAction);
        OnPlayerActionStarted?.Invoke();
    }

    /// <summary>PlayerActionState 완료 후 호출합니다.</summary>
    public void EnterRoleActivation()
    {
        CurrentState = TurnStateType.RoleActivation;
        ChangeState(_roleActivation);
    }

    /// <summary>
    /// RoleActivationState 완료 후 호출합니다.
    /// 다이어로그 재생을 위해 TurnEnd 상태로 전환하며 OnTurnEndEntered 이벤트를 발생시킵니다.
    /// </summary>
    /// <param name="eventLog">이번 턴 결과 로그. 다이어로그 마지막에 표시됩니다.</param>
    /// <param name="isLoopCondition">루프 강제 종료 조건 달성 여부.</param>
    public void EnterTurnEnd(IReadOnlyList<string> eventLog = null, bool isLoopCondition = false)
    {
        CurrentState = TurnStateType.TurnEnd;
        _turnEnd.SetContext(eventLog, isLoopCondition);
        ChangeState(_turnEnd);
    }

    /// <summary>
    /// DialogueManager가 모든 다이어로그 재생을 마친 후 호출합니다.
    /// 루프 조건 여부에 따라 CompleteTurn 또는 TriggerLoopCondition으로 분기합니다.
    /// </summary>
    public void FinishTurnEnd() => _turnEnd.Finish();

    /// <summary>TurnEndState에서 정리 완료 후 마지막으로 호출합니다. LoopStateMachine에 턴 완료를 알립니다.</summary>
    public void CompleteTurn() => OnTurnCompleted?.Invoke();

    /// <summary>루프 종료 조건 달성 시 호출합니다. LoopStateMachine이 다음 루프로 진행합니다.</summary>
    public void TriggerLoopCondition() => OnLoopConditionTriggered?.Invoke();

    /// <summary>TurnEndState에서 OnTurnEndEntered 이벤트를 발생시킵니다.</summary>
    internal void FireTurnEndEntered(IReadOnlyList<string> log, bool isLoopCondition)
        => OnTurnEndEntered?.Invoke(log, isLoopCondition);

    /// <summary>
    /// 추리를 선언합니다. PlayerAction 단계에서만 유효하며, 다른 단계의 호출은 무시됩니다.
    /// LoopStateMachine.RequestDeduction()을 통해 호출됩니다.
    /// </summary>
    public void DeclareDeduction()
    {
        if (CurrentState != TurnStateType.PlayerAction) return;
        OnDeductionDeclared?.Invoke();
    }
}
