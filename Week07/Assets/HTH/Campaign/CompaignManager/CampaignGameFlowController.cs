using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 게임 흐름 컨트롤러입니다.
    /// 기본모드 GameFlowController와 씬을 완전히 분리합니다.
    ///
    /// ─── 기본모드와의 차이 ───────────────────────────────────────────────────
    ///   WinState에서도 캐릭터 이동/클릭 허용 (Phase2 조각 수집)
    ///   ReassignRolesForPhase2() 지원
    ///   HandleGameEnded — 로비 이동 없이 Phase2 루프 유지
    ///   ForceEndTurn — WinState에서도 호출 가능
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────────
    ///   OrderConfig         → RoleActivationOrderConfig 에셋
    ///   CharacterRegistry   → CharacterRegistry 에셋
    ///   StageRoleConfig     → StageRoleConfig 에셋
    ///   CharacterSpawner    → 씬의 CampaignCharacterSpawner 컴포넌트
    ///   Lobby Scene Name    → "LobbyScene"
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public class CampaignGameFlowController : SingletonMonobehaviour<CampaignGameFlowController>
    {
        [SerializeField] private RoleActivationOrderConfig _orderConfig;
        [SerializeField] private CharacterRegistry _characterRegistry;
        [SerializeField] private StageRoleConfig _stageRoleConfig;
        [SerializeField] private StageSetupConfig _setupConfig;
        [SerializeField] private CampaignCharacterSpawner _characterSpawner;
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [SerializeField] private string _stageId;
        public string StageId => !string.IsNullOrEmpty(NewGameConfig.StageId)
            ? NewGameConfig.StageId : _stageId;

        private CampaignLoopStateMachine _loopSM;
        private Dictionary<int, CharacterView> _characterViews;

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
            _loopSM = new CampaignLoopStateMachine(_orderConfig, _characterRegistry, _stageRoleConfig, _setupConfig);
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
                    || (loop == LoopStateType.RunningTurn && CurrentTurnState == TurnStateType.PlayerAction);
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

        /// <summary>
        /// ★ 캠페인 전용 — WinState에서도 ForceEndTurn 허용 (조각 수집 루프 유지)
        /// </summary>
        public void ForceEndTurn()
        {
            bool isRunning = CurrentLoopState == LoopStateType.RunningTurn;
            bool isWinLoop = CurrentLoopState == LoopStateType.WinState;
            if (!isRunning && !isWinLoop) return;
            _loopSM?.ForceEndPlayerAction();
        }

        // ── 입력 라우팅 ───────────────────────────────────────────────────────

        /// <summary>★ 캠페인 전용 — WinState에서도 캐릭터 클릭 허용</summary>
        public void NotifyCharacterClicked(int characterId)
        {
            bool isRunning = CurrentLoopState == LoopStateType.RunningTurn;
            bool isWinLoop = CurrentLoopState == LoopStateType.WinState;
            if (!isRunning && !isWinLoop) return;
            _loopSM?.NotifyCharacterClicked(characterId);
        }

        /// <summary>★ 캠페인 전용 — WinState에서도 Zone 클릭 허용</summary>
        public void NotifyZoneClicked(int zoneId)
        {
            bool isRunning = CurrentLoopState == LoopStateType.RunningTurn;
            bool isWinLoop = CurrentLoopState == LoopStateType.WinState;
            if (!isRunning && !isWinLoop) return;
            _loopSM?.NotifyZoneClicked(zoneId);
        }

        public void BeginDragSelect(int characterId) => _loopSM?.BeginDragSelect(characterId);
        public PlayerActionState GetPlayerActionState() => _loopSM?.GetPlayerActionState();

        // ── Phase2 역할 재배정 ────────────────────────────────────────────────

        /// <summary>
        /// Phase2 진입 시 역할 배정을 교체합니다.
        /// CampaignModeManager.EnterPhase2Direct()에서 호출합니다.
        /// </summary>
        public void ReassignRolesForPhase2(StageRoleConfig phase2RoleConfig)
        {
            if (phase2RoleConfig == null)
            {
                Debug.LogError("[CampaignGameFlowController] ReassignRolesForPhase2 — config가 null입니다.");
                return;
            }

            var gameState = _loopSM?.GameState;
            if (gameState == null)
            {
                Debug.LogError("[CampaignGameFlowController] ReassignRolesForPhase2 — GameState가 없습니다.");
                return;
            }

            var roles = phase2RoleConfig.Roles;
            var characterIds = gameState.GetAllCharacterIds();

            if (roles == null || roles.Count == 0 || roles.Count != characterIds.Count)
            {
                Debug.LogError($"[CampaignGameFlowController] ReassignRolesForPhase2 — " +
                               $"역할 수({roles?.Count ?? 0})와 캐릭터 수({characterIds.Count}) 불일치.");
                return;
            }

            var roleTable = gameState.GetRoleTable();
            roleTable.Clear();
            for (int i = 0; i < characterIds.Count; i++)
                roleTable.Assign(characterIds[i], roles[i]);

            RefreshAllCharacterViews();
            Debug.Log($"[CampaignGameFlowController] Phase2 역할 재배정 완료 — {roles.Count}개");
        }

        // ── Private ──────────────────────────────────────────────────────────

        /// <summary>
        /// ★ 캠페인 전용 HandleGameEnded — 승리해도 로비로 이동하지 않음.
        /// Phase2 루프는 CampaignModeManager가 종료를 관리합니다.
        /// </summary>
        private void HandleGameEnded(bool isWin)
        {
            GameLogger.Instance?.LogEvent("game_end", new Dictionary<string, object>
            {
                { "result",      isWin ? "win" : "lose" },
                { "mode",        "phase2_campaign" },
                { "total_loops", _loopSM?.LoopCount ?? 0 },
                { "total_turns", _loopSM?.TurnCount ?? 0 },
            });

            if (!isWin)
            {
                // 패배 시만 로비로 이동
                SceneManager.LoadScene(_lobbySceneName);
            }
            // 승리 시: WinState 유지 → 캐릭터 이동/조각 수집 계속
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
            if (_stageRoleConfig == null) Debug.LogError("[CampaignGameFlowController] StageRoleConfig 미연결.");
            if (_characterSpawner == null) Debug.LogWarning("[CampaignGameFlowController] CharacterSpawner 미연결.");
        }
    }
}