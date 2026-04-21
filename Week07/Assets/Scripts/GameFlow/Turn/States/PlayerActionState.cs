using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 캐릭터 행동(이동/선택)을 자유롭게 결정하는 단계입니다.
/// 같은 캐릭터를 여러 번 이동해도 마지막 위치만 커밋됩니다.
/// 턴 종료 버튼(ForceEnd) 호출 시 모든 이동을 GameState에 일괄 적용하고 RoleActivation으로 넘어갑니다.
///
/// 입력 흐름:
///   캐릭터 드래그/클릭 → 선택
///   구역 드롭/클릭 → 해당 캐릭터 이동 예약 (반복 가능, 마지막 위치가 최종)
///   턴 종료 버튼 → ForceEnd() → CommitPendingMoves() → RoleActivation
///
/// 외부 연결:
///   PlayerTurnInputHandler가 BeginDragSelect / NotifyCharacterClicked / NotifyZoneClicked를 호출합니다.
///   OnCharacterSelected / OnActionConfirmed 이벤트로 뷰를 갱신합니다.
/// </summary>
public class PlayerActionState : IState
{
    private readonly TurnStateMachine _turnSM;
    private readonly Func<GameState>  _getGameState;

    /// <summary>캐릭터가 선택됐을 때 발생합니다. characterId=-1이면 선택 해제.</summary>
    public event Action<int> OnCharacterSelected;

    /// <summary>
    /// 캐릭터 이동이 예약됐을 때 발생합니다. (characterId, targetZoneId)
    /// targetZoneId == -1이면 현재 위치 유지. 뷰 스냅에 사용하고 GameState는 ForceEnd 시에 커밋됩니다.
    /// </summary>
    public event Action<int, int> OnActionConfirmed;

    private int                  _selectedId    = -1;
    private Dictionary<int, int> _pendingMoves  = new();

    /// <summary>
    /// 턴 시작 위치(PreviousZone)와 예약 목적지가 다른 캐릭터가 한 명 이상 있으면 true.
    /// 같은 구역으로 되돌아온 경우는 이동으로 인정하지 않습니다.
    /// </summary>
    public bool HasAnyMove
    {
        get
        {
            var gs = _getGameState();
            if (gs == null) return false;
            foreach (var kv in _pendingMoves)
            {
                if (kv.Value != gs.GetPreviousZone(kv.Key))
                    return true;
            }
            return false;
        }
    }

    public PlayerActionState(TurnStateMachine turnSM, Func<GameState> getGameState)
    {
        _turnSM       = turnSM;
        _getGameState = getGameState;
    }

    public void Enter()
    {
        _selectedId = -1;
        _pendingMoves.Clear();

        var gameState = _getGameState();

        // 루프 첫 턴에는 파도 구역 효과를 건너뜁니다 (루프 리셋 직후 강제 이동 방지).
        if (gameState != null && !gameState.IsFirstTurnOfLoop)
            gameState.ApplyWaveZoneEffects();

        if (gameState != null)
            gameState.IsFirstTurnOfLoop = false;

        gameState?.SyncAllPreviousZones();
    }

    public void Tick() { }

    public void Exit()
    {
        _selectedId = -1;
    }

    // ── 외부 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 모든 예약 이동을 GameState에 적용하고 능력 발동 단계로 넘어갑니다.
    /// 이동하지 않은 캐릭터는 현재 위치 유지, 특수능력은 정상 발동됩니다.
    /// 턴 종료 버튼(GameHUD)에서 호출합니다.
    /// </summary>
    public void ForceEnd()
    {
        _selectedId = -1;
        OnCharacterSelected?.Invoke(-1);
        CommitPendingMoves();
        _turnSM.EnterRoleActivation();
    }

    // ── 외부 입력 (PlayerTurnInputHandler에서 호출) ──────────────────────────

    /// <summary>
    /// 드래그 시작 시 호출합니다. 재클릭 대기 판정 없이 강제 선택합니다.
    /// characterId=-1이면 선택 해제.
    /// </summary>
    public void BeginDragSelect(int characterId)
    {
        if (characterId >= 0)
        {
            var gs = _getGameState();
            if (gs != null)
            {
                var status = gs.GetCharacter(characterId);
                if (status != null && !status.IsAlive)
                {
                    Debug.LogWarning($"[PlayerActionState] BeginDragSelect({characterId}) 무시 — 사망 캐릭터");
                    return;
                }
            }
        }

        _selectedId = characterId;
        Debug.Log($"[PlayerActionState] BeginDragSelect → _selectedId={_selectedId}");
        OnCharacterSelected?.Invoke(characterId);
    }

    /// <summary>
    /// 캐릭터 클릭 시 호출합니다.
    /// 미선택 → 선택 / 같은 캐릭터 재클릭 → 선택 해제
    /// </summary>
    public void NotifyCharacterClicked(int characterId)
    {
        // 사망 캐릭터는 선택 불가
        var gs = _getGameState();
        if (gs != null)
        {
            var status = gs.GetCharacter(characterId);
            if (status != null && !status.IsAlive) return;
        }

        if (_selectedId == characterId)
        {
            // 같은 캐릭터 재클릭 → 선택 해제
            _selectedId = -1;
            OnCharacterSelected?.Invoke(-1);
        }
        else
        {
            _selectedId = characterId;
            OnCharacterSelected?.Invoke(characterId);
        }
    }

    /// <summary>
    /// 구역 클릭/드롭 시 호출합니다. 캐릭터가 선택된 상태에서만 유효합니다.
    /// 같은 캐릭터를 여러 번 호출하면 마지막 값으로 덮어씁니다.
    /// </summary>
    public void NotifyZoneClicked(int zoneId)
    {
        if (_selectedId < 0)
        {
            Debug.LogWarning($"[PlayerActionState] NotifyZoneClicked({zoneId}) 무시 — 선택된 캐릭터 없음");
            return;
        }
        Debug.Log($"[PlayerActionState] Zone {zoneId} 이동 예약 — ID:{_selectedId}");
        _pendingMoves[_selectedId] = zoneId;

        int movedId = _selectedId;
        _selectedId = -1;
        OnCharacterSelected?.Invoke(-1);
        OnActionConfirmed?.Invoke(movedId, zoneId);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 버퍼에 쌓인 모든 이동을 GameState에 한꺼번에 적용합니다.
    /// ForceEnd() 시 호출합니다.
    /// </summary>
    private void CommitPendingMoves()
    {
        var gs = _getGameState();
        if (gs == null) return;

        Debug.Log($"[PlayerActionState] CommitPendingMoves — {_pendingMoves.Count}건 적용");
        foreach (var kv in _pendingMoves)
        {
            gs.ApplyMove(kv.Key, kv.Value);
            Debug.Log($"  ID:{kv.Key} → Zone {kv.Value} 이동");
        }
    }
}
