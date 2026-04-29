using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 흐름의 진입점입니다. LoopStateMachine을 소유하고 매 프레임 Tick을 전달합니다.
/// 기본모드 전용입니다. 캠페인 모드는 CampaignGameFlowController를 사용하세요.
///
/// Inspector 필수 연결:
///   OrderConfig         → RoleActivationOrderConfig 에셋
///   CharacterRegistry   → CharacterRegistry 에셋
///   StageRoleConfig     → StageRoleConfig 에셋
///   CharacterSpawner    → 씬의 CharacterSpawner 컴포넌트
/// </summary>
[DefaultExecutionOrder(-10)]
[DisallowMultipleComponent]
public class GameFlowController : SingletonMonobehaviour<GameFlowController>
{
    [SerializeField] private RoleActivationOrderConfig _orderConfig;
    [SerializeField] private CharacterRegistry _characterRegistry;
    [SerializeField] private StageRoleConfig _stageRoleConfig;
    [SerializeField] private StageSetupConfig _setupConfig;
    [SerializeField] private CharacterSpawner _characterSpawner;
    [SerializeField] private string _lobbySceneName = "LobbyScene";

    [SerializeField] private string _stageId;
    public string StageId => !string.IsNullOrEmpty(NewGameConfig.StageId)
        ? NewGameConfig.StageId : _stageId;

    [Tooltip("이 스테이지를 클리어하면 로비에서 엔딩 다이얼로그를 재생합니다.")]
    [SerializeField] private bool _triggerEndingDialogueOnWin;

    private LoopStateMachine _loopSM;
    private Dictionary<int, CharacterView> _characterViews;

    public IReadOnlyDictionary<int, CharacterView> CharacterViews => _characterViews;
    public CharacterSpawner GetCharacterSpawner() => _characterSpawner;

    // ── 이벤트 ────────────────────────────────────────────────────────────────

    public event System.Action OnLoopReset
    {
        add => _loopSM.OnLoopReset += value;
        remove => _loopSM.OnLoopReset -= value;
    }

    public event System.Action OnGameStarted
    {
        add => _loopSM.OnGameStarted += value;
        remove => _loopSM.OnGameStarted -= value;
    }

    public event System.Action OnFinalDecisionEntered
    {
        add => _loopSM.OnFinalDecisionEntered += value;
        remove => _loopSM.OnFinalDecisionEntered -= value;
    }

    public event System.Action OnFinalDecisionExited
    {
        add => _loopSM.OnFinalDecisionExited += value;
        remove => _loopSM.OnFinalDecisionExited -= value;
    }

    public event System.Action<bool> OnGameEnded
    {
        add => _loopSM.OnGameEnded += value;
        remove => _loopSM.OnGameEnded -= value;
    }

    public event System.Action<bool> OnGameEndDialogueRequested
    {
        add => _loopSM.OnGameEndDialogueRequested += value;
        remove => _loopSM.OnGameEndDialogueRequested -= value;
    }

    public event System.Action<bool> OnGameEndDialogueComplete;
    public void NotifyGameEndDialogueComplete(bool isWin) => OnGameEndDialogueComplete?.Invoke(isWin);

    // ── Unity ────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        ValidateInspectorRefs();
        _loopSM = new LoopStateMachine(_orderConfig, _characterRegistry, _stageRoleConfig, _setupConfig);
    }

    private void Start()
    {
        _loopSM.StartGame();
        SpawnCharacters();
        _loopSM.OnLoopReset += HandleLoopReset;
        _loopSM.OnGameEnded += HandleGameEnded;

        var turnSM = GetTurnSM();
        if (turnSM != null)
        {
            turnSM.OnTurnEndEntered += (_, __) => RefreshAllCharacterViews();
            turnSM.OnPlayerActionStarted += SyncViewsAfterZoneEffects;
        }
    }

    private void Update() => _loopSM.Tick();

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

    // ── 공개 API ─────────────────────────────────────────────────────────────

    public void EnterFinalDecision() => _loopSM?.EnterFinalDecision();
    public void SubmitFinalDecision(bool isWin) => _loopSM?.GetFinalDecisionState()?.SubmitDecision(isWin);
    public RoleType GetActualRole(int characterId) => _loopSM?.GameState?.GetRole(characterId) ?? default;

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

    public IGameState GameState => _loopSM?.GameState;
    public int LoopCount => (_loopSM?.LoopCount ?? 0) + 1;
    public int TurnCount => (_loopSM?.TurnCount ?? 0) + 1;
    public int CurrentDay => (_loopSM?.LoopCount ?? 0) + 1;
    public string CurrentTimeOfDay => (_loopSM?.TurnCount ?? 0) switch
    {
        0 => "morning",
        1 => "lunch",
        2 => "evening",
        _ => "unknown"
    };

    public LoopStateType CurrentLoopState => _loopSM?.CurrentState ?? default;
    public TurnStateType CurrentTurnState => _loopSM?.TurnSM?.CurrentState ?? default;

    public TurnStateMachine GetTurnSM() => _loopSM?.TurnSM;
    public void FinishTurnEnd() => _loopSM?.TurnSM?.FinishTurnEnd();
    public void FinishGameEndDialogue() => _loopSM?.FinishGameEndDialogue();

    public void ForceEndTurn()
    {
        if (CurrentLoopState != LoopStateType.RunningTurn) return;
        _loopSM?.ForceEndPlayerAction();
    }

    // ── 입력 라우팅 ───────────────────────────────────────────────────────────

    public void NotifyCharacterClicked(int characterId)
    {
        if (CurrentLoopState != LoopStateType.RunningTurn) return;
        _loopSM?.NotifyCharacterClicked(characterId);
    }

    public void NotifyZoneClicked(int zoneId)
    {
        if (CurrentLoopState != LoopStateType.RunningTurn) return;
        _loopSM?.NotifyZoneClicked(zoneId);
    }

    public void BeginDragSelect(int characterId) => _loopSM?.BeginDragSelect(characterId);
    public PlayerActionState GetPlayerActionState() => _loopSM?.GetPlayerActionState();

    // ── Private ──────────────────────────────────────────────────────────────

    private void HandleGameEnded(bool isWin)
    {
        GameLogger.Instance?.LogEvent("game_end", new Dictionary<string, object>
        {
            { "result",      isWin ? "win" : "lose" },
            { "mode",        "phase1_normal" },
            { "total_loops", _loopSM?.LoopCount ?? 0 },
            { "total_turns", _loopSM?.TurnCount ?? 0 },
        });

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
            Debug.LogError("[GameFlowController] GameState가 없습니다.");
            return;
        }
        _characterViews = _characterSpawner.SpawnAll(gameState);
        _characterSpawner.ApplyZoneRulesToGameState(gameState);
    }

    private void ValidateInspectorRefs()
    {
        if (_orderConfig == null) Debug.LogError("[GameFlowController] OrderConfig가 연결되지 않았습니다.");
        if (_characterRegistry == null) Debug.LogError("[GameFlowController] CharacterRegistry가 연결되지 않았습니다.");
        if (_stageRoleConfig == null) Debug.LogError("[GameFlowController] StageRoleConfig가 연결되지 않았습니다.");
        if (_characterSpawner == null) Debug.LogWarning("[GameFlowController] CharacterSpawner가 연결되지 않았습니다.");
    }
}