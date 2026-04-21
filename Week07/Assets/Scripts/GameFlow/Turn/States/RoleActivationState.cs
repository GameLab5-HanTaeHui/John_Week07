using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 직업 능력을 순서대로 발동하고 결과를 확정하는 단계입니다.
///
/// 실행 순서 (RoleActivationOrderConfig 기준):
///   0. 이전 턴 지연 사망 마킹 (연인D 사망으로 등록된 연인C 등)
///   1. 스냅샷 캡처 (BeforeAction = PreviousZone, AfterAction = CurrentZone)
///   2. 이번 턴 사망 마크 · 로그 초기화
///   3~N. 직업 능력 발동 (RoleActivationOrderConfig 순서)
///   N+1. 사망 확정 (응징자·연인C/D 패시브 포함)
///   N+2. TurnRecord 커밋
///   N+3. 루프 종료 조건 확인 (스테이지별 LoopConditionConfig 위임)
/// </summary>
public class RoleActivationState : IState
{
    private readonly TurnStateMachine          _turnSM;
    private readonly RoleActivationOrderConfig _orderConfig;
    private readonly Func<GameState>           _getGameState;
    private readonly TurnHistoryRepository     _historyRepo;
    private readonly Func<int>                 _getSeed;
    private readonly Func<int>                 _getLoopIndex;
    private readonly Func<int>                 _getTurnIndex;
    private readonly Func<LoopConditionConfig> _getLoopCondition;

    public RoleActivationState(
        TurnStateMachine          turnSM,
        RoleActivationOrderConfig orderConfig,
        Func<GameState>           getGameState,
        TurnHistoryRepository     historyRepo,
        Func<int>                 getSeed,
        Func<int>                 getLoopIndex,
        Func<int>                 getTurnIndex,
        Func<LoopConditionConfig> getLoopCondition)
    {
        _turnSM           = turnSM;
        _orderConfig      = orderConfig;
        _getGameState     = getGameState;
        _historyRepo      = historyRepo;
        _getSeed          = getSeed;
        _getLoopIndex     = getLoopIndex;
        _getTurnIndex     = getTurnIndex;
        _getLoopCondition = getLoopCondition;
    }

    public void Enter()
    {
        var gameState = _getGameState();

        if (gameState == null)
        {
            Debug.LogError("[RoleActivationState] GameState가 null입니다.");
            _turnSM.EnterTurnEnd(null, false);
            return;
        }

        // ── 단계 0: 이전 턴에 등록된 지연 사망 수집 (ResetForNewTurn 전에 꺼냄) ──
        var delayedDeaths = gameState.ConsumeDelayedDeaths();

        // ── 단계 1: 스냅샷 캡처 (이동 완료, 능력 발동 전) ──────────────────
        var beforeAction = CapturePositions(gameState, usePreviousZone: true);
        var afterAction  = CapturePositions(gameState, usePreviousZone: false);

        // ── 단계 2: 이번 턴 사망 마크 · 로그 초기화 ──────────────────────────
        gameState.ResetForNewTurn();

        // ── 단계 0 후처리: 지연 사망을 이번 턴 사망 마크로 적용 ──────────────
        foreach (var delayed in delayedDeaths)
        {
            var target = gameState.GetCharacter(delayed.TargetCharacterId);
            if (target != null && target.IsAlive)
                gameState.MarkForDeath(delayed.TargetCharacterId, delayed.CauseRole, delayed.SourceCharacterId);
        }

        // ── 단계 3~N: 직업 능력 순서대로 발동 ──────────────────────────────
        var processor = new RoleAbilityProcessor(_orderConfig);
        processor.Process(gameState);

        // ── 단계 N+1: 사망 확정 (응징자·연인C/D 패시브 포함) ────────────────
        gameState.ConfirmDeaths();
        LogTurnSummary(gameState);

        // 사망 확정 후 최종 상태 스냅샷 (재개용)
        var finalStates = CapturePositions(gameState, usePreviousZone: false);

        // ── 단계 N+2~N+3: 루프 종료 조건 확인 후 커밋 ──────────────────────
        bool isLoopCondition = CheckLoopEndCondition(gameState);
        var  record          = BuildRecord(gameState, beforeAction, afterAction);
        record.IsLoopConditionTurn = isLoopCondition;
        _historyRepo.Commit(record, isLoopCondition, finalStates);

        _turnSM.EnterTurnEnd(isLoopCondition ? null : gameState.GetEventLog(), isLoopCondition);
    }

    public void Tick() { }
    public void Exit() { }

    // ── Private — 스냅샷 ─────────────────────────────────────────────────────

    private List<CharacterPositionSnapshot> CapturePositions(GameState gameState, bool usePreviousZone)
    {
        var ids  = gameState.GetAllCharacterIds();
        var list = new List<CharacterPositionSnapshot>(ids.Count);
        foreach (int id in ids)
        {
            list.Add(new CharacterPositionSnapshot
            {
                CharacterId = id,
                ZoneId      = usePreviousZone ? gameState.GetPreviousZone(id) : gameState.GetZone(id),
                IsAlive     = gameState.GetCharacter(id)?.IsAlive ?? false
            });
        }
        return list;
    }

    private TurnRecord BuildRecord(
        GameState                       gameState,
        List<CharacterPositionSnapshot> beforeAction,
        List<CharacterPositionSnapshot> afterAction)
    {
        var deathMarks = gameState.GetAllDeathMarks();
        var deaths     = new List<TurnDeathRecord>(deathMarks.Count);
        foreach (var dm in deathMarks)
        {
            // 실제로 사망한 캐릭터만 기록 (대리자 대신으로 생존한 원래 대상 제외)
            var character = gameState.GetCharacter(dm.TargetCharacterId);
            if (character == null || character.IsAlive) continue;

            deaths.Add(new TurnDeathRecord
            {
                CharacterId       = dm.TargetCharacterId,
                CauseRole         = dm.CauseRole,
                SourceCharacterId = dm.SourceCharacterId
            });
        }

        return new TurnRecord
        {
            LoopIndex    = _getLoopIndex(),
            TurnIndex    = _getTurnIndex(),
            Seed         = _getSeed(),
            BeforeAction = beforeAction,
            AfterAction  = afterAction,
            Deaths       = deaths
        };
    }

    // ── Private — 루프 종료 조건 ─────────────────────────────────────────────

    /// <summary>
    /// 스테이지별 LoopConditionConfig에 조건 판정을 위임합니다.
    /// null이면 루프 조건 없음 (false 반환).
    /// </summary>
    private bool CheckLoopEndCondition(GameState gameState)
    {
        var condition = _getLoopCondition?.Invoke();
        return condition != null && condition.ShouldLoop(gameState);
    }

    private void LogTurnSummary(GameState gameState) { }
}
