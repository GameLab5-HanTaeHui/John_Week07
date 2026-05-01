using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 게임 흐름 컨트롤러입니다.
    /// 기본모드 GameFlowController와 씬을 완전히 분리합니다.
    ///
    /// ─── 턴 종료 흐름 ────────────────────────────────────────────────────────
    ///   [TurnEnd 진입]
    ///     → OnTurnEndEntered 발행
    ///     → CampaignInGameDialogueManager 가 턴종료 대사 재생
    ///     → FireTurnEndDialogueFinished 호출
    ///     → DialogueTriggerManager.OnTurnEndDialogueFinished
    ///         강제퇴고 → FinishTurnEnd() 즉시
    ///         일반퇴고 → Zone 대사 재생 완료 → FinishTurnEnd()
    ///
    /// ─── 캐릭터 위치 동기화 규칙 ─────────────────────────────────────────────
    ///   SyncViewsToGameState — 위치만 반영, 슬롯 맵 유지
    ///   ResetSlots           — 퇴고/강제퇴고 시에만 슬롯 맵 재초기화
    ///   ReviveDeadOnly       — 사망자만 부활 위치 스냅, 생존자 위치 유지
    ///   OnTurnEndEntered     — RefreshAllCharacterViews + CaptureDeadCharacters만 호출
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────────
    ///   OrderConfig         → RoleActivationOrderConfig 에셋
    ///   CharacterRegistry   → CharacterRegistry 에셋
    ///   StageRoleConfig     → StageRoleConfig 에셋 (캠페인에서는 선택 사항)
    ///   StageSetupConfig    → StageSetupConfig 에셋 (캠페인에서는 선택 사항)
    ///   CharacterSpawner    → 씬의 CampaignCharacterSpawner 컴포넌트
    ///   Anchor Character Id → 대화 구역 판별 기준 캐릭터 ID (기본 1 = 엔비)
    ///   Lobby Scene Name    → "LobbyScene"
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public class CampaignGameFlowController : SingletonMonobehaviour<CampaignGameFlowController>
    {
        [SerializeField] private RoleActivationOrderConfig _orderConfig;
        [SerializeField] private CharacterRegistry _characterRegistry;

        [Tooltip("캠페인 모드에서는 선택 사항입니다.\n" +
                 "연결하지 않으면 역할 배정 없이 캐릭터만 스폰합니다.")]
        [SerializeField] private StageRoleConfig _stageRoleConfig;

        [Tooltip("캠페인 모드에서는 선택 사항입니다.\n" +
                 "연결하지 않으면 시드 없이 기본 Zone 배치를 사용합니다.")]
        [SerializeField] private StageSetupConfig _setupConfig;

        [Tooltip("캠페인 전용 강제퇴고 조건입니다.\n" +
                 "CampaignLoopConditionConfig 에셋을 연결하세요.\n" +
                 "연결하지 않으면 StageRoleConfig.LoopCondition을 사용합니다.\n" +
                 "★ StageRoleConfig 없이 캠페인만 쓸 경우 반드시 연결해야 강제퇴고가 동작합니다.")]
        [SerializeField] private LoopConditionConfig _loopConditionConfig;

        [SerializeField] private CampaignCharacterSpawner _characterSpawner;
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Header("앵커 캐릭터")]
        [Tooltip("대화 구역 판별 기준이 되는 앵커 캐릭터 ID입니다.\n" +
                 "DialogueTriggerManager와 CampaignPlayerInputHandler의\n" +
                 "Anchor Character Id와 동일하게 설정하세요.\n" +
                 "기본값 1 = 엔비")]
        [SerializeField] private int _anchorCharacterId = 1;

        [SerializeField] private string _stageId;

        /// <summary>대화 구역 판별 기준 앵커 캐릭터 ID입니다.</summary>
        public int AnchorCharacterId => _anchorCharacterId;

        public string StageId => !string.IsNullOrEmpty(NewGameConfig.StageId)
            ? NewGameConfig.StageId : _stageId;

        private CampaignLoopStateMachine _loopSM;
        private Dictionary<int, CharacterView> _characterViews;

        /// <summary>
        /// 루프 리셋 시 부활 처리를 위해 TurnEnd 진입 시점에 캡처한 사망자 ID 집합입니다.
        /// OnLoopReset에서 ReviveDeadOnly()에 전달 후 즉시 null로 초기화합니다.
        /// </summary>
        private HashSet<int> _deadCharactersAtTurnEnd;

        public IReadOnlyDictionary<int, CharacterView> CharacterViews => _characterViews;
        public CampaignCharacterSpawner GetCharacterSpawner() => _characterSpawner;

        // ── 이벤트 ───────────────────────────────────────────────────────────

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

        // ── Unity ────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            ValidateInspectorRefs();
            _loopSM = new CampaignLoopStateMachine(
                _orderConfig, _characterRegistry, _stageRoleConfig, _setupConfig,
                _loopConditionConfig);
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
                // 턴 종료 진입 — 뷰 갱신 + 사망자 캡처 (루프 리셋 부활 처리용)
                turnSM.OnTurnEndEntered += (_, __) =>
                {
                    RefreshAllCharacterViews();
                    CaptureDeadCharacters();
                };
            }
        }

        private void Update() => _loopSM.Tick();

        private void OnDestroy()
        {
            if (_loopSM == null) return;
            _loopSM.OnLoopReset -= HandleLoopReset;
            _loopSM.OnGameEnded -= HandleGameEnded;
        }

        // ── 공개 API ─────────────────────────────────────────────────────────

        public void EnterFinalDecision() => _loopSM?.EnterFinalDecision();
        public void SubmitFinalDecision(bool isWin) => _loopSM?.GetFinalDecisionState()?.SubmitDecision(isWin);
        public RoleType GetActualRole(int charId) => _loopSM?.GameState?.GetRole(charId) ?? default;

        public bool CanEnterFinalDecision
        {
            get
            {
                var loop = CurrentLoopState;
                return loop == LoopStateType.AwaitingFinalDecision
                    || (loop == LoopStateType.RunningTurn
                        && CurrentTurnState == TurnStateType.PlayerAction);
            }
        }

        // ── HUD 정보 노출 ─────────────────────────────────────────────────────

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

        // ── 입력 라우팅 ───────────────────────────────────────────────────────

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

        // ── Private ──────────────────────────────────────────────────────────

        private void HandleGameEnded(bool isWin)
        {
            GameLogger.Instance?.LogEvent("game_end", new Dictionary<string, object>
            {
                { "result",      isWin ? "win" : "lose" },
                { "mode",        "campaign" },
                { "total_loops", _loopSM?.LoopCount ?? 0 },
                { "total_turns", _loopSM?.TurnCount ?? 0 },
            });

            // 승패 무관하게 로비로 이동
            SceneManager.LoadScene(_lobbySceneName);
        }

        /// <summary>
        /// 퇴고/강제퇴고 시 호출됩니다.
        /// ★ 캠페인 전용 — 사망자만 부활 위치로 스냅, 생존자 위치 유지
        /// </summary>
        private void HandleLoopReset()
        {
            if (_characterSpawner == null || _characterViews == null) return;
            var gameState = _loopSM.GameState;
            if (gameState == null) return;

            _characterSpawner.ApplyZoneRulesToGameState(gameState);

            // ★ ResetSlots — 슬롯 맵 재초기화 (Zone 배치 갱신)
            _characterSpawner.ResetSlots(gameState, _characterViews);

            // ★ ReviveDeadOnly — 사망자만 스냅, 생존자 위치 유지
            _characterSpawner.ReviveDeadOnly(gameState, _characterViews, _deadCharactersAtTurnEnd);

            // 사용 완료 후 초기화
            _deadCharactersAtTurnEnd = null;
        }

        /// <summary>
        /// TurnEnd 진입 시 호출됩니다.
        /// GameState가 갱신되기 전 시점의 사망자 ID를 캡처합니다.
        /// OnLoopReset에서 ReviveDeadOnly()에 전달하는 데 사용합니다.
        /// </summary>
        private void CaptureDeadCharacters()
        {
            var gameState = _loopSM?.GameState;
            if (gameState == null) return;

            _deadCharactersAtTurnEnd = new HashSet<int>();
            foreach (int id in gameState.GetAllCharacterIds())
            {
                var ch = gameState.GetCharacter(id);
                if (ch != null && !ch.IsAlive)
                    _deadCharactersAtTurnEnd.Add(id);
            }

            if (_deadCharactersAtTurnEnd.Count > 0)
                Debug.Log($"[GFC] 사망자 캡처 — [{string.Join(",", _deadCharactersAtTurnEnd)}]");
        }

        private void RefreshAllCharacterViews()
        {
            if (_characterViews == null) return;
            foreach (var view in _characterViews.Values)
                view.RefreshView();
        }

        private void SpawnCharacters()
        {
            if (_characterSpawner == null) return;
            var gameState = _loopSM.GameState;
            if (gameState == null)
            {
                Debug.LogError("[CampaignGameFlowController] GameState가 없습니다.");
                return;
            }
            _characterViews = _characterSpawner.SpawnAll(gameState);
            _characterSpawner.ApplyZoneRulesToGameState(gameState);
        }

        private void ValidateInspectorRefs()
        {
            if (_orderConfig == null) Debug.LogError("[CampaignGameFlowController] OrderConfig 미연결.");
            if (_characterRegistry == null) Debug.LogError("[CampaignGameFlowController] CharacterRegistry 미연결.");
            if (_characterSpawner == null) Debug.LogWarning("[CampaignGameFlowController] CharacterSpawner 미연결.");

            // 캠페인 모드에서는 선택 사항 — 없으면 경고만
            if (_stageRoleConfig == null)
                Debug.LogWarning("[CampaignGameFlowController] StageRoleConfig 미연결 — 역할 배정 없이 진행합니다.");
            if (_setupConfig == null)
                Debug.LogWarning("[CampaignGameFlowController] StageSetupConfig 미연결 — 기본 Zone 배치를 사용합니다.");

            // ★ 강제퇴고 조건 누락 경고
            bool hasCondition = _loopConditionConfig != null
                || (_stageRoleConfig != null && _stageRoleConfig.LoopCondition != null);
            if (!hasCondition)
                Debug.LogWarning("[CampaignGameFlowController] LoopConditionConfig 미연결 — " +
                                 "강제퇴고(주인공 사망) 조건이 동작하지 않습니다.\n" +
                                 "CampaignLoopConditionConfig 에셋을 Loop Condition Config 슬롯에 연결하세요.");
        }
    }
}