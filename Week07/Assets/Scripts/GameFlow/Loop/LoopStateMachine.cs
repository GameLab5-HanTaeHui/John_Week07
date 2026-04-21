/// <summary>
/// 게임 전체 루프 흐름을 관리하는 최상위 상태머신입니다.
/// TurnStateMachine을 소유하며, RunningTurnState를 통해 턴 실행을 위임합니다.
///
/// 흐름: GameSetup → LoopStart → RunningTurn(턴×3) → LoopEnd → 반복(최대 5루프) or DeductionPhase → GameEnd
/// </summary>
public class LoopStateMachine : StateMachine
{
    public const int MaxLoops    = 5;
    public const int TurnsPerLoop = 3;

    public LoopStateType CurrentState { get; private set; }

    /// <summary>
    /// 2루프 이상에서 GameState의 위치·생사 초기화가 완료된 직후 발생합니다.
    /// GameFlowController에서 구독해 CharacterView를 초기 위치로 동기화하세요.
    /// </summary>
    public event System.Action OnLoopReset;

    /// <summary>
    /// 게임 최초 시작(첫 루프 첫 턴) 시 1회만 발생합니다.
    /// DialogueManager에서 구독해 인트로 다이어로그를 재생하세요.
    /// </summary>
    public event System.Action OnGameStarted;

    /// <summary>FinalDecision 상태 진입 시 발생합니다. FinalDecisionUI에서 구독하세요.</summary>
    public event System.Action OnFinalDecisionEntered;

    /// <summary>FinalDecision 상태 종료 시 발생합니다. FinalDecisionUI에서 구독하세요.</summary>
    public event System.Action OnFinalDecisionExited;

    /// <summary>게임이 종료(승/패)되었을 때 발생합니다. TutorialManager 등에서 구독하세요.</summary>
    public event System.Action<bool> OnGameEnded;

    /// <summary>
    /// FinalDecision 제출 직후 발생합니다. DialogueManager에서 구독해 승/패 다이얼로그를 재생하세요.
    /// 다이얼로그 완료 후 반드시 FinishGameEndDialogue()를 호출해야 WinState/LoseState로 전환됩니다.
    /// </summary>
    public event System.Action<bool> OnGameEndDialogueRequested;

    internal void FireFinalDecisionEntered()              => OnFinalDecisionEntered?.Invoke();
    internal void FireFinalDecisionExited()               => OnFinalDecisionExited?.Invoke();
    internal void FireLoopReset()                         => OnLoopReset?.Invoke();
    internal void FireGameEnded(bool isWin)               => OnGameEnded?.Invoke(isWin);
    internal void FireGameEndDialogueRequested(bool isWin) => OnGameEndDialogueRequested?.Invoke(isWin);

    /// <summary>로비에서 전달된 스테이지 ID입니다. GameSetupState에서 설정합니다.</summary>
    public string StageId { get; internal set; }

    /// <summary>현재까지 완료된 루프 수 (0-based)</summary>
    public int LoopCount { get; internal set; }

    /// <summary>현재 루프에서 완료된 턴 수 (0-based). EnterRunningTurn() 시 초기화됩니다.</summary>
    public int TurnCount { get; private set; }

    // 재개 시 EnterRunningTurn()에서 사용할 시작 턴 인덱스. 0이면 정상 시작.
    private int _resumeTurnCount;

    /// <summary>GameSetupState에서 재개 시 첫 턴 인덱스를 지정합니다.</summary>
    internal void SetResumeTurnCount(int turnCount) => _resumeTurnCount = turnCount;

    /// <summary>
    /// 런타임 게임 상태입니다. GameSetupState에서 생성 후 이 프로퍼티에 설정합니다.
    /// LoopStartState 등 GameState를 필요로 하는 State에서 참조합니다.
    /// </summary>
    public GameState GameState { get; set; }

    /// <summary>
    /// 현재 루프를 결정한 시드값입니다. GameSetupState에서 시드 결정 직후 설정합니다.
    /// RoleActivationState가 TurnRecord에 기록할 때 참조합니다.
    /// </summary>
    public int CurrentSeed { get; internal set; }

    private readonly TurnStateMachine          _turnSM;

    private readonly GameSetupState            _gameSetup;
    private readonly LoopStartState            _loopStart;
    private readonly RunningTurnState          _runningTurn;
    private readonly LoopEndState              _loopEnd;
    private readonly AwaitingFinalDecisionState _awaitingFinalDecision;
    private readonly FinalDecisionState        _finalDecision;
    private readonly WinState                  _winState;
    private readonly LoseState                 _loseState;
    private readonly GameEndState              _gameEnd;

    private bool _pendingIsWin;

    /// <param name="orderConfig">직업 능력 발동 순서 설정</param>
    /// <param name="characterRegistry">캐릭터 데이터 레지스트리</param>
    /// <param name="stageRoleConfig">현재 스테이지 역할 풀</param>
    /// <param name="setupConfig">시드 기반 배치 설정 (null 허용 — 없으면 순수 난수)</param>
    public LoopStateMachine(
        RoleActivationOrderConfig orderConfig,
        CharacterRegistry         characterRegistry,
        StageRoleConfig           stageRoleConfig,
        StageSetupConfig          setupConfig = null)
    {
        // () => GameState : GameState는 GameSetupState에서 생성되므로 람다로 지연 참조합니다.
        // () => stageRoleConfig.LoopCondition : 스테이지별 루프 종료 조건을 위임합니다.
        _turnSM = new TurnStateMachine(
            orderConfig,
            () => GameState,
            TurnHistoryRepository.Instance,
            () => CurrentSeed,
            () => LoopCount,
            () => TurnCount,
            () => stageRoleConfig != null ? stageRoleConfig.LoopCondition : null);

        _gameSetup             = new GameSetupState(this, characterRegistry, stageRoleConfig, setupConfig);
        _loopStart             = new LoopStartState(this);
        _runningTurn           = new RunningTurnState(this, _turnSM);
        _loopEnd               = new LoopEndState(this);
        _awaitingFinalDecision = new AwaitingFinalDecisionState(this);
        _finalDecision         = new FinalDecisionState(this);
        _winState              = new WinState(this);
        _loseState             = new LoseState(this);
        _gameEnd               = new GameEndState(this);

        // 루프 종료 조건 달성 시 ResultDisplay를 건너뛰고 다음 루프로 강제 진행
        _turnSM.OnLoopConditionTriggered += AdvanceLoop;
    }

    // ── 입력 전달 (GameFlowController → 여기 → TurnStateMachine) ─────────────

    /// <summary>TurnStateMachine에 직접 접근합니다. GameFlowController에서 HUD 이벤트 구독에 사용합니다.</summary>
    public TurnStateMachine TurnSM => _turnSM;

    /// <summary>PlayerTurnInputHandler가 사용할 PlayerActionState를 반환합니다.</summary>
    public PlayerActionState GetPlayerActionState() => _turnSM.PlayerAction;

    /// <summary>FinalDecisionUI에서 제출 시 호출할 FinalDecisionState를 반환합니다.</summary>
    public FinalDecisionState GetFinalDecisionState() => _finalDecision;

    public void NotifyCharacterClicked(int characterId) => _turnSM.NotifyCharacterClicked(characterId);
    public void NotifyZoneClicked(int zoneId)           => _turnSM.NotifyZoneClicked(zoneId);
    public void ForceEndPlayerAction()                  => _turnSM.ForceEndPlayerAction();
    public void BeginDragSelect(int characterId)        => _turnSM.BeginDragSelect(characterId);

    // ── 외부 공개 API ────────────────────────────────────────────

    /// <summary>게임을 시작합니다. GameFlowController.Start()에서 호출합니다.</summary>
    public void StartGame()
    {
        LoopCount    = 0;
        TurnCount    = 0;
        CurrentState = LoopStateType.GameSetup;
        ChangeState(_gameSetup);
    }

    /// <summary>
    /// 추리 선언을 요청합니다. UI 버튼 → GameFlowController → 여기로 연결하세요.
    /// RunningTurn 상태이고 PlayerAction 단계일 때만 유효하며, 그 외 호출은 무시됩니다.
    /// </summary>
    public void RequestFinalDecision()
    {
        if (CurrentState != LoopStateType.RunningTurn) return;
        _turnSM.DeclareDeduction();
    }

    // ── 상태 전환 메서드 (Loop State 클래스에서 호출) ────────────

    /// <summary>GameSetupState 완료 후 호출합니다.</summary>
    public void EnterLoopStart()
    {
        CurrentState = LoopStateType.LoopStart;
        ChangeState(_loopStart);
    }

    /// <summary>LoopStartState 완료 후 호출합니다.</summary>
    public void EnterRunningTurn()
    {
        bool isFirstEver = LoopCount == 0 && _resumeTurnCount == 0;
        TurnCount        = _resumeTurnCount;
        _resumeTurnCount = 0;
        CurrentState     = LoopStateType.RunningTurn;
        ChangeState(_runningTurn);
        if (isFirstEver)
            OnGameStarted?.Invoke();
    }

    /// <summary>
    /// RunningTurnState가 OnTurnCompleted 이벤트를 받으면 호출합니다.
    /// 다음 턴 시작 또는 LoopEnd 전환 여부를 결정합니다.
    /// </summary>
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

    /// <summary>
    /// LoopEndState 완료 후 호출합니다.
    /// 다음 루프 시작 또는 DeductionPhase 전환 여부를 결정합니다.
    /// </summary>
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

    /// <summary>
    /// 루프 소진 후 대기 상태로 진입합니다.
    /// 플레이어는 이동·턴 진행 불가, HoldToEnterFinalDecision으로만 FinalDecision 진입 가능합니다.
    /// </summary>
    public void EnterAwaitingFinalDecision()
    {
        CurrentState = LoopStateType.AwaitingFinalDecision;
        ChangeState(_awaitingFinalDecision);
    }

    /// <summary>
    /// 최종 결정 단계로 진입합니다.
    /// PlayerAction(조기 선언) 또는 AwaitingFinalDecision(루프 소진)에서 진입 가능합니다.
    /// 그 외 상태에서의 호출은 무시됩니다.
    /// </summary>
    public void EnterFinalDecision()
    {
        bool fromPlayerAction = CurrentState == LoopStateType.RunningTurn
                             && _turnSM.CurrentState == TurnStateType.PlayerAction;
        bool fromAwaiting     = CurrentState == LoopStateType.AwaitingFinalDecision;

        if (!fromPlayerAction && !fromAwaiting) return;

        CurrentState = LoopStateType.FinalDecision;
        ChangeState(_finalDecision);
    }

    /// <summary>플레이어의 최종 추리 결과입니다. FinalDecisionState에서 설정됩니다.</summary>
    public bool IsWin { get; private set; }

    /// <summary>
    /// FinalDecisionState 완료 후 호출합니다.
    /// 승/패 다이얼로그 요청 이벤트를 발생시키며, 상태 전환은 FinishGameEndDialogue() 호출 시 일어납니다.
    /// </summary>
    public void EnterGameEnd(bool isWin)
    {
        _pendingIsWin = isWin;
        FireGameEndDialogueRequested(isWin);
    }

    /// <summary>
    /// DialogueManager가 승/패 다이얼로그를 완료한 뒤 호출합니다.
    /// WinState 또는 LoseState로 전환합니다.
    /// </summary>
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
