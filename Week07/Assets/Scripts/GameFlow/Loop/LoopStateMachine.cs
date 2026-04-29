using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 루프 흐름을 관리하는 최상위 상태머신입니다.
/// 기본모드 전용입니다. 캠페인 모드는 CampaignLoopStateMachine을 사용하세요.
///
/// 흐름: GameSetup → LoopStart → RunningTurn(턴×3) → LoopEnd → 반복(최대 5루프) or AwaitingFinalDecision → FinalDecision → GameEnd
/// </summary>
public class LoopStateMachine : StateMachine
{
    public const int MaxLoops = 5;
    public const int TurnsPerLoop = 3;

    public LoopStateType CurrentState { get; private set; }

    public event System.Action OnLoopReset;
    public event System.Action OnGameStarted;
    public event System.Action OnFinalDecisionEntered;
    public event System.Action OnFinalDecisionExited;
    public event System.Action<bool> OnGameEnded;
    public event System.Action<bool> OnGameEndDialogueRequested;

    internal void FireFinalDecisionEntered() => OnFinalDecisionEntered?.Invoke();
    internal void FireFinalDecisionExited() => OnFinalDecisionExited?.Invoke();
    internal void FireLoopReset() => OnLoopReset?.Invoke();
    internal void FireGameEnded(bool isWin) => OnGameEnded?.Invoke(isWin);
    internal void FireGameEndDialogueRequested(bool isWin) => OnGameEndDialogueRequested?.Invoke(isWin);

    public string StageId { get; internal set; }
    public int LoopCount { get; internal set; }
    public int TurnCount { get; private set; }
    public int CurrentSeed { get; internal set; }
    public GameState GameState { get; set; }

    private int _resumeTurnCount;
    internal void SetResumeTurnCount(int turnCount) => _resumeTurnCount = turnCount;

    private readonly TurnStateMachine _turnSM;
    private readonly GameSetupState _gameSetup;
    private readonly LoopStartState _loopStart;
    private readonly RunningTurnState _runningTurn;
    private readonly LoopEndState _loopEnd;
    private readonly AwaitingFinalDecisionState _awaitingFinalDecision;
    private readonly FinalDecisionState _finalDecision;
    private readonly WinState _winState;
    private readonly LoseState _loseState;
    private readonly GameEndState _gameEnd;

    private bool _pendingIsWin;

    public LoopStateMachine(
        RoleActivationOrderConfig orderConfig,
        CharacterRegistry characterRegistry,
        StageRoleConfig stageRoleConfig,
        StageSetupConfig setupConfig = null)
    {
        _turnSM = new TurnStateMachine(
            orderConfig,
            () => GameState,
            TurnHistoryRepository.Instance,
            () => CurrentSeed,
            () => LoopCount,
            () => TurnCount,
            () => stageRoleConfig != null ? stageRoleConfig.LoopCondition : null);

        _gameSetup = new GameSetupState(this, characterRegistry, stageRoleConfig, setupConfig);
        _loopStart = new LoopStartState(this);
        _runningTurn = new RunningTurnState(this, _turnSM);
        _loopEnd = new LoopEndState(this);
        _awaitingFinalDecision = new AwaitingFinalDecisionState(this);
        _finalDecision = new FinalDecisionState(this);
        _winState = new WinState(this);
        _loseState = new LoseState(this);
        _gameEnd = new GameEndState(this);

        _turnSM.OnLoopConditionTriggered += AdvanceLoop;
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────

    public TurnStateMachine TurnSM => _turnSM;
    public PlayerActionState GetPlayerActionState() => _turnSM.PlayerAction;
    public FinalDecisionState GetFinalDecisionState() => _finalDecision;

    /// <summary>기본모드 — RunningTurn 상태에서만 ZoneClicked 허용.</summary>
    public void NotifyZoneClicked(int zoneId)
    {
        if (CurrentState != LoopStateType.RunningTurn) return;
        _turnSM.NotifyZoneClicked(zoneId);
    }

    /// <summary>기본모드 — RunningTurn 상태에서만 CharacterClicked 허용.</summary>
    public void NotifyCharacterClicked(int characterId)
    {
        if (CurrentState != LoopStateType.RunningTurn) return;
        _turnSM.NotifyCharacterClicked(characterId);
    }

    public void BeginDragSelect(int characterId) => _turnSM.BeginDragSelect(characterId);
    public void ForceEndPlayerAction() => _turnSM.ForceEndPlayerAction();

    // ── 상태 전환 ────────────────────────────────────────────────────────────

    public void StartGame()
    {
        LoopCount = 0;
        TurnCount = 0;
        CurrentState = LoopStateType.GameSetup;
        ChangeState(_gameSetup);
    }

    public void RequestFinalDecision()
    {
        if (CurrentState != LoopStateType.RunningTurn) return;
        _turnSM.DeclareDeduction();
    }

    public void EnterLoopStart()
    {
        CurrentState = LoopStateType.LoopStart;
        ChangeState(_loopStart);
    }

    public void EnterRunningTurn()
    {
        bool isFirstEver = LoopCount == 0 && _resumeTurnCount == 0;
        TurnCount = _resumeTurnCount;
        _resumeTurnCount = 0;
        CurrentState = LoopStateType.RunningTurn;
        ChangeState(_runningTurn);
        if (isFirstEver)
            OnGameStarted?.Invoke();
    }

    /// <summary>기본모드 — 3턴 완료 시 LoopEnd 진입.</summary>
    public void AdvanceTurn()
    {
        TurnCount++;
        if (TurnCount >= TurnsPerLoop)
        {
            CurrentState = LoopStateType.LoopEnd;
            ChangeState(_loopEnd);
        }
        else
        {
            _turnSM.StartTurn();
        }
    }

    /// <summary>기본모드 — MaxLoops 도달 시 AwaitingFinalDecision, 미달 시 다음 루프.</summary>
    public void AdvanceLoop()
    {
        LoopCount++;

        if (LoopCount >= MaxLoops)
            EnterAwaitingFinalDecision();
        else
        {
            CurrentState = LoopStateType.GameSetup;
            ChangeState(_gameSetup);
        }
    }

    public void EnterAwaitingFinalDecision()
    {
        CurrentState = LoopStateType.AwaitingFinalDecision;
        ChangeState(_awaitingFinalDecision);
    }

    /// <summary>기본모드 — PlayerAction 또는 AwaitingFinalDecision 상태에서만 진입.</summary>
    public void EnterFinalDecision()
    {
        bool fromPlayerAction = CurrentState == LoopStateType.RunningTurn
                             && _turnSM.CurrentState == TurnStateType.PlayerAction;
        bool fromAwaiting = CurrentState == LoopStateType.AwaitingFinalDecision;

        if (!fromPlayerAction && !fromAwaiting) return;

        var gfc = GameFlowController.Instance;
        GameLogger.Instance?.LogEvent("final_decision_enter", new Dictionary<string, object>
        {
            { "from",                fromPlayerAction ? "early" : "awaiting" },
            { "loop",                LoopCount + 1 },
            { "turn",                TurnCount + 1 },
            { "day",                 gfc?.CurrentDay ?? 0 },
            { "time_of_day",         gfc?.CurrentTimeOfDay ?? "" },
            { "seed",                CurrentSeed },
            { "session_elapsed_sec", GameLogger.Instance?.SessionElapsedSec ?? 0 },
        });

        CurrentState = LoopStateType.FinalDecision;
        ChangeState(_finalDecision);
    }

    public bool IsWin { get; private set; }

    public void EnterGameEnd(bool isWin)
    {
        _pendingIsWin = isWin;
        FireGameEndDialogueRequested(isWin);
    }

    public void FinishGameEndDialogue()
    {
        IsWin = _pendingIsWin;
        if (_pendingIsWin)
        {
            CurrentState = LoopStateType.WinState;
            ChangeState(_winState);
        }
        else
        {
            CurrentState = LoopStateType.LoseState;
            ChangeState(_loseState);
        }
    }
}