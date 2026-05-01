using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 전용 루프 상태머신입니다.
    /// 기본모드는 LoopStateMachine을 사용하세요.
    ///
    /// ─── 기본모드 LoopStateMachine과의 차이 ──────────────────────────────
    ///
    ///   AdvanceLoop():
    ///     MaxLoops 도달 시 AwaitingFinalDecision 진입 없음.
    ///     LoopCount=1로 리셋 후 GameSetup으로 돌아가 루프를 무한 반복합니다.
    ///     (플레이어가 모든 캐릭터 기록을 완성할 때까지 계속 플레이)
    ///
    ///   NotifyZoneClicked() / NotifyCharacterClicked():
    ///     WinState에서도 TurnSM에 전달합니다.
    ///     (승리 후에도 캐릭터 이동 / 조각 수집 루프 유지)
    ///
    ///   EnterFinalDecision():
    ///     CampaignGameFlowController를 참조합니다.
    ///     (기본모드: GameFlowController)
    ///
    ///   AdvanceTurn() 로그 / EnterFinalDecision() 로그:
    ///     gfc 참조를 CampaignGameFlowController로 교체.
    /// </summary>
    public class CampaignLoopStateMachine : StateMachine
    {
        // ★ 캠페인은 마감일 없음 — MaxLoops 제거, 무한 루프
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
        private readonly CampaignGameSetupState _gameSetup;
        private readonly CampaignLoopStartState _loopStart;
        private readonly CampaignRunningTurnState _runningTurn;
        private readonly LoopEndState _loopEnd;
        private readonly AwaitingFinalDecisionState _awaitingFinalDecision;
        private readonly FinalDecisionState _finalDecision;
        private readonly WinState _winState;
        private readonly LoseState _loseState;
        private readonly GameEndState _gameEnd;

        private bool _pendingIsWin;

        public CampaignLoopStateMachine(
            RoleActivationOrderConfig orderConfig,
            CharacterRegistry characterRegistry,
            StageRoleConfig stageRoleConfig,
            StageSetupConfig setupConfig = null,
            LoopConditionConfig loopCondition = null)
        {
            // ★ loopCondition이 직접 주입되면 우선 사용
            // 없으면 StageRoleConfig.LoopCondition 폴백
            var resolvedCondition = loopCondition
                ?? (stageRoleConfig != null ? stageRoleConfig.LoopCondition : null);

            _turnSM = new TurnStateMachine(orderConfig,
                () => GameState,
                TurnHistoryRepository.Instance,
                () => CurrentSeed,
                () => LoopCount,
                () => TurnCount,
                () => resolvedCondition);


            _gameSetup = new CampaignGameSetupState(this, characterRegistry, stageRoleConfig, setupConfig);
            _loopStart = new CampaignLoopStartState(this);
            _runningTurn = new CampaignRunningTurnState(this, _turnSM);
            _loopEnd = new LoopEndState(this);
            _awaitingFinalDecision = new AwaitingFinalDecisionState(this);
            _finalDecision = new FinalDecisionState(this);
            _winState = new WinState(this);
            _loseState = new LoseState(this);
            _gameEnd = new GameEndState(this);

            _turnSM.OnLoopConditionTriggered += AdvanceLoop;
        }

        // ── 공개 API ─────────────────────────────────────────────────────────

        public TurnStateMachine TurnSM => _turnSM;
        public PlayerActionState GetPlayerActionState() => _turnSM.PlayerAction;
        public FinalDecisionState GetFinalDecisionState() => _finalDecision;

        /// <summary>★ 캠페인 전용 — WinState에서도 ZoneClicked 허용.</summary>
        public void NotifyZoneClicked(int zoneId)
        {
            if (CurrentState != LoopStateType.RunningTurn &&
                CurrentState != LoopStateType.WinState) return;
            _turnSM.NotifyZoneClicked(zoneId);
        }

        /// <summary>★ 캠페인 전용 — WinState에서도 CharacterClicked 허용.</summary>
        public void NotifyCharacterClicked(int characterId)
        {
            if (CurrentState != LoopStateType.RunningTurn &&
                CurrentState != LoopStateType.WinState) return;
            _turnSM.NotifyCharacterClicked(characterId);
        }

        public void BeginDragSelect(int characterId) => _turnSM.BeginDragSelect(characterId);
        public void ForceEndPlayerAction() => _turnSM.ForceEndPlayerAction();

        // ── 상태 전환 ────────────────────────────────────────────────────────

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
        /// ★ 캠페인 전용 — 마감일 없음, 무한 루프.
        /// LoopCount를 계속 증가시키며 GameSetup을 반복합니다.
        /// </summary>
        public void AdvanceLoop()
        {
            LoopCount++;

            // ★ 캠페인은 MaxLoops 없음 — 무한 반복
            Debug.Log($"[CampaignLoopSM] Loop {LoopCount} 시작 — GameSetup 진입");

            CurrentState = LoopStateType.GameSetup;
            ChangeState(_gameSetup);
        }

        public void EnterAwaitingFinalDecision()
        {
            CurrentState = LoopStateType.AwaitingFinalDecision;
            ChangeState(_awaitingFinalDecision);
        }

        /// <summary>
        /// ★ 캠페인 전용 — CampaignGameFlowController를 참조합니다.
        /// PlayerAction 또는 AwaitingFinalDecision 상태에서만 진입.
        /// </summary>
        public void EnterFinalDecision()
        {
            bool fromPlayerAction = CurrentState == LoopStateType.RunningTurn
                                 && _turnSM.CurrentState == TurnStateType.PlayerAction;
            bool fromAwaiting = CurrentState == LoopStateType.AwaitingFinalDecision;

            if (!fromPlayerAction && !fromAwaiting) return;

            // ★ CampaignGameFlowController 참조
            var gfc = CampaignGameFlowController.Instance;
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

        /// <summary>
        /// ★ 캠페인 전용 — 승리 시 WinState 유지 (로비 이동 없음).
        /// 패배 시 LoseState → CampaignGameFlowController.HandleGameEnded(false) → 로비.
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
}