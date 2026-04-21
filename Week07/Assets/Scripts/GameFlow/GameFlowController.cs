using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 흐름의 진입점입니다. LoopStateMachine을 소유하고 매 프레임 Tick을 전달합니다.
/// UI의 추리 선언 버튼은 이 클래스의 RequestDeduction()에 연결하세요.
///
/// Inspector 필수 연결:
///   OrderConfig         → RoleActivationOrderConfig 에셋 (직업 능력 발동 순서)
///   CharacterRegistry   → CharacterRegistry 에셋 (7개 캐릭터 데이터)
///   StageRoleConfig     → StageRoleConfig 에셋 (현재 스테이지 역할 풀)
///   CharacterSpawner    → 씬의 CharacterSpawner 컴포넌트
///
/// 실행 순서:
///   DefaultExecutionOrder(-10)으로 PlayerTurnInputHandler보다 먼저 Start()가 실행됩니다.
///   PlayerTurnInputHandler.Start()에서 CharacterViews / PlayerActionState를 안전하게 참조할 수 있습니다.
/// </summary>
[DefaultExecutionOrder(-10)]
[DisallowMultipleComponent]
public class GameFlowController : SingletonMonobehaviour<GameFlowController>
{
    [SerializeField] private RoleActivationOrderConfig _orderConfig;
    [SerializeField] private CharacterRegistry         _characterRegistry;
    [SerializeField] private StageRoleConfig           _stageRoleConfig;
    [SerializeField] private StageSetupConfig          _setupConfig;
    [SerializeField] private CharacterSpawner          _characterSpawner;
    [SerializeField] private string                    _lobbySceneName = "LobbyScene";

    /// <summary>이 씬의 스테이지 식별자입니다. 클리어 기록 저장 및 다음 스테이지 해금에 사용됩니다.</summary>
    [SerializeField] private string _stageId;

    [Tooltip("이 스테이지를 클리어하면 로비에서 엔딩 다이얼로그를 재생합니다.")]
    [SerializeField] private bool _triggerEndingDialogueOnWin;

    private LoopStateMachine            _loopSM;
    private Dictionary<int, CharacterView> _characterViews;

    /// <summary>characterId → CharacterView. SpawnAll 이후 유효합니다.</summary>
    public IReadOnlyDictionary<int, CharacterView> CharacterViews => _characterViews;

    /// <summary>
    /// 루프 리셋(GameState 재생성 완료) 시 발생합니다.
    /// PlayerTurnInputHandler 등 외부 컴포넌트가 구독해 내부 상태를 동기화할 수 있습니다.
    /// </summary>
    public event System.Action OnLoopReset
    {
        add    => _loopSM.OnLoopReset += value;
        remove => _loopSM.OnLoopReset -= value;
    }

    public event System.Action OnGameStarted
    {
        add    => _loopSM.OnGameStarted += value;
        remove => _loopSM.OnGameStarted -= value;
    }

    public event System.Action OnFinalDecisionEntered
    {
        add    => _loopSM.OnFinalDecisionEntered += value;
        remove => _loopSM.OnFinalDecisionEntered -= value;
    }

    public event System.Action OnFinalDecisionExited
    {
        add    => _loopSM.OnFinalDecisionExited += value;
        remove => _loopSM.OnFinalDecisionExited -= value;
    }

    /// <summary>게임 종료(승/패) 시 발생합니다. isWin = true 이면 클리어, false 이면 실패.</summary>
    public event System.Action<bool> OnGameEnded
    {
        add    => _loopSM.OnGameEnded += value;
        remove => _loopSM.OnGameEnded -= value;
    }

    /// <summary>
    /// 최종 결정 제출 직후 발생합니다. DialogueManager에서 구독해 승/패 다이얼로그를 재생하세요.
    /// 다이얼로그 완료 후 FinishGameEndDialogue()를 호출해야 WinState/LoseState로 전환됩니다.
    /// </summary>
    public event System.Action<bool> OnGameEndDialogueRequested
    {
        add    => _loopSM.OnGameEndDialogueRequested += value;
        remove => _loopSM.OnGameEndDialogueRequested -= value;
    }

    /// <summary>
    /// 게임 종료 다이얼로그가 끝난 직후 발생합니다. FinalDecisionUI에서 구독해 결과 패널을 표시하세요.
    /// </summary>
    public event System.Action<bool> OnGameEndDialogueComplete;

    /// <summary>DialogueManager가 게임 종료 다이얼로그를 완료한 후 호출합니다.</summary>
    public void NotifyGameEndDialogueComplete(bool isWin) => OnGameEndDialogueComplete?.Invoke(isWin);

    protected override void Awake()
    {
        base.Awake();
        ValidateInspectorRefs();
        _loopSM = new LoopStateMachine(_orderConfig, _characterRegistry, _stageRoleConfig, _setupConfig);
    }

    private void Start()
    {
        // StartGame()은 동기 실행 — 완료 시점에 GameState가 준비되어 있습니다.
        _loopSM.StartGame();
        SpawnCharacters();
        _loopSM.OnLoopReset += HandleLoopReset;
        _loopSM.OnGameEnded += HandleGameEnded;

        var turnSM = GetTurnSM();
        if (turnSM != null)
        {
            turnSM.OnTurnEndEntered    += (_, __) => RefreshAllCharacterViews();
            // 파도 구역 효과가 PlayerActionState.Enter()에서 적용된 뒤 뷰를 재동기화합니다.
            turnSM.OnPlayerActionStarted += SyncViewsAfterZoneEffects;
        }
    }

    private void Update()
    {
        _loopSM.Tick();
    }

    /// <summary>
    /// HoldToEnterFinalDecision이 완료됐을 때 호출합니다.
    /// PlayerAction 단계 또는 AwaitingFinalDecision 상태에서만 유효합니다.
    /// </summary>
    public void EnterFinalDecision() => _loopSM?.EnterFinalDecision();

    /// <summary>FinalDecisionUI 판정 완료 후 호출합니다.</summary>
    public void SubmitFinalDecision(bool isWin) => _loopSM?.GetFinalDecisionState()?.SubmitDecision(isWin);

    /// <summary>현재 GameState의 특정 캐릭터 실제 역할을 반환합니다.</summary>
    public RoleType GetActualRole(int characterId) => _loopSM?.GameState?.GetRole(characterId) ?? default;

    /// <summary>HoldToEnterFinalDecision 활성화 가능 여부입니다.</summary>
    public bool CanEnterFinalDecision
    {
        get
        {
            var loop = CurrentLoopState;
            return loop == LoopStateType.AwaitingFinalDecision
                || (loop == LoopStateType.RunningTurn && CurrentTurnState == TurnStateType.PlayerAction);
        }
    }

    // ── HUD 정보 노출 ─────────────────────────────────────────────────────────

    /// <summary>현재 GameState입니다. PlayerTurnInputHandler에서 구역 조회에 사용합니다.</summary>
    public IGameState    GameState        => _loopSM?.GameState;

    /// <summary>현재 루프 번호 (1-based). GameHUD 표시용.</summary>
    public int           LoopCount        => (_loopSM?.LoopCount ?? 0) + 1;
    /// <summary>현재 턴 번호 (1-based). GameHUD 표시용.</summary>
    public int           TurnCount        => (_loopSM?.TurnCount ?? 0) + 1;
    public LoopStateType CurrentLoopState => _loopSM?.CurrentState ?? default;
    public TurnStateType CurrentTurnState => _loopSM?.TurnSM?.CurrentState ?? default;

    /// <summary>GameHUD에서 TurnSM 이벤트 구독에 사용합니다.</summary>
    public TurnStateMachine GetTurnSM() => _loopSM?.TurnSM;

    /// <summary>DialogueManager가 다이어로그 재생을 마친 후 호출합니다.</summary>
    public void FinishTurnEnd() => _loopSM?.TurnSM?.FinishTurnEnd();

    /// <summary>DialogueManager가 승/패 다이얼로그를 완료한 후 호출합니다.</summary>
    public void FinishGameEndDialogue() => _loopSM?.FinishGameEndDialogue();

    /// <summary>턴 종료 버튼에서 호출합니다. 미확정 캐릭터는 현 위치 유지, 특수능력 정상 발동.</summary>
    public void ForceEndTurn()
    {
        if (CurrentLoopState != LoopStateType.RunningTurn) return;
        _loopSM?.ForceEndPlayerAction();
    }

    // ── 입력 라우팅 (PlayerTurnInputHandler → 여기 → LoopSM → TurnSM → PlayerActionState) ──

    /// <summary>PlayerTurnInputHandler에서 캐릭터 클릭 시 호출합니다.</summary>
    public void NotifyCharacterClicked(int characterId)
    {
        if (CurrentLoopState != LoopStateType.RunningTurn) return;
        _loopSM?.NotifyCharacterClicked(characterId);
    }

    /// <summary>PlayerTurnInputHandler에서 구역 클릭 시 호출합니다.</summary>
    public void NotifyZoneClicked(int zoneId)
    {
        if (CurrentLoopState != LoopStateType.RunningTurn) return;
        _loopSM?.NotifyZoneClicked(zoneId);
    }

    /// <summary>
    /// 드래그 시작/취소 시 호출합니다. 재클릭=대기 로직을 우회해 강제 선택합니다.
    /// characterId=-1이면 선택 해제.
    /// </summary>
    public void BeginDragSelect(int characterId) => _loopSM?.BeginDragSelect(characterId);

    /// <summary>
    /// PlayerTurnInputHandler에서 이벤트 구독 대상인 PlayerActionState를 가져옵니다.
    /// GameFlowController.Start() 이후에 호출하세요.
    /// </summary>
    public PlayerActionState GetPlayerActionState() => _loopSM?.GetPlayerActionState();

    // ── Private ──────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (_loopSM != null)
        {
            _loopSM.OnLoopReset -= HandleLoopReset;
            _loopSM.OnGameEnded -= HandleGameEnded;
        }

        var turnSM = GetTurnSM();
        if (turnSM != null)
            turnSM.OnPlayerActionStarted -= SyncViewsAfterZoneEffects;
    }

    private void HandleGameEnded(bool isWin)
    {
        if (isWin)
        {
            string idToRecord = !string.IsNullOrEmpty(NewGameConfig.StageId) ? NewGameConfig.StageId : _stageId;
            if (!string.IsNullOrEmpty(idToRecord))
                StageClearRepository.Instance.RecordClear(idToRecord);

            if (_triggerEndingDialogueOnWin)
                LobbyDialogueManager.PendingEndingDialogue = true;
        }
        SceneManager.LoadScene(_lobbySceneName);
    }

    private void HandleLoopReset()
    {
        if (_characterSpawner == null || _characterViews == null) return;
        var gameState = _loopSM.GameState;
        if (gameState == null) return;
        _characterSpawner.ApplyZoneRulesToGameState(gameState);
        _characterSpawner.SyncViewsToGameState(gameState, _characterViews);
    }

    private void RefreshAllCharacterViews()
    {
        if (_characterViews == null) return;
        foreach (var view in _characterViews.Values)
            view.RefreshView();
    }

    private void SyncViewsAfterZoneEffects()
    {
        if (_characterSpawner == null || _characterViews == null) return;
        var gameState = _loopSM?.GameState;
        if (gameState == null) return;
        _characterSpawner.SyncViewsToGameState(gameState, _characterViews);
    }

    private void SpawnCharacters()
    {
        if (_characterSpawner == null) return;

        var gameState = _loopSM.GameState;
        if (gameState == null)
        {
            Debug.LogError("[GameFlowController] GameState가 없습니다. CharacterSpawner를 건너뜁니다.");
            return;
        }

        _characterViews = _characterSpawner.SpawnAll(gameState);
        _characterSpawner.ApplyZoneRulesToGameState(gameState);
    }

    private void ValidateInspectorRefs()
    {
        if (_orderConfig == null)
            Debug.LogError("[GameFlowController] OrderConfig가 연결되지 않았습니다.");
        if (_characterRegistry == null)
            Debug.LogError("[GameFlowController] CharacterRegistry가 연결되지 않았습니다.");
        if (_stageRoleConfig == null)
            Debug.LogError("[GameFlowController] StageRoleConfig가 연결되지 않았습니다.");
        if (_characterSpawner == null)
            Debug.LogWarning("[GameFlowController] CharacterSpawner가 연결되지 않았습니다. 캐릭터가 스폰되지 않습니다.");
    }
}
