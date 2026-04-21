using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 시작 시 1회 실행되는 초기화 단계입니다.
///
/// 시드 결정 규칙 (세션 내 모든 루프에서 동일한 시드 사용):
///   1. 로비(NewGameConfig) — 최우선. 첫 루프에서 _sessionSeed에 저장.
///   2. StageSetupConfig   — 에디터 직접 실행 시에만. 마찬가지로 _sessionSeed에 저장.
///   3. 순수 난수           — 폴백.
/// </summary>
public class GameSetupState : IState
{
    private readonly LoopStateMachine  _loopSM;
    private readonly CharacterRegistry _characterRegistry;
    private readonly StageRoleConfig   _stageRoleConfig;
    private readonly StageSetupConfig  _setupConfig;

    public GameSetupState(
        LoopStateMachine  loopSM,
        CharacterRegistry characterRegistry,
        StageRoleConfig   stageRoleConfig,
        StageSetupConfig  setupConfig)
    {
        _loopSM            = loopSM;
        _characterRegistry = characterRegistry;
        _stageRoleConfig   = stageRoleConfig;
        _setupConfig       = setupConfig;
    }

    public void Enter()
    {
        // ── 1. CharacterState 목록 생성 ──────────────────────────────────
        var characterStates = new List<CharacterState>();
        foreach (var data in _characterRegistry.Characters)
            characterStates.Add(new CharacterState(data));

        var roles = _stageRoleConfig.Roles;
        if (roles.Count != characterStates.Count)
        {
            Debug.LogError(
                $"[GameSetupState] 역할 수({roles.Count})와 캐릭터 수({characterStates.Count})가 일치하지 않습니다. " +
                "StageRoleConfig와 CharacterRegistry를 확인하세요.");
            return;
        }

        // ── 2. 시드 획득 및 디코딩 ─────────────────────────────────────────
        int seed = GetSeed(characterStates.Count, roles.Count);
        _loopSM.CurrentSeed = seed;

        SeedEncoder.Decode(seed, characterStates.Count, roles.Count, GameState.ZoneCount,
            out var rolePerm, out var zones);

        Debug.Log($"[GameSetupState] 사용 시드: {seed}");

        // ── 3. 역할 배정 + GameState 생성 ───────────────────────────────
        var roleTable = new RoleAssignmentTable();
        for (int i = 0; i < characterStates.Count; i++)
            roleTable.Assign(characterStates[i].CharacterId, roles[rolePerm[i]]);

        var gameState = new GameState(characterStates, roleTable);
        _loopSM.GameState = gameState;

        // ── 4. 구역 배치 적용 ──────────────────────────────────────────
        for (int i = 0; i < characterStates.Count; i++)
            gameState.SetCharacterInitialZone(characterStates[i].CharacterId, zones[i]);

        // ── 5. 뷰 동기화 (2루프 이상) + 다음 단계로 전환 ────────────────
        if (_loopSM.LoopCount > 0)
            _loopSM.FireLoopReset();

        _loopSM.EnterLoopStart();
    }

    public void Tick() { }
    public void Exit() { }

    // ── Private ──────────────────────────────────────────────────────────────

    // 게임 세션 전체에서 사용할 고정 시드. 첫 루프에서 결정된 뒤 모든 루프에서 재사용됩니다.
    private int  _sessionSeed     = -1;
    private bool _startedFromLobby;

    private int GetSeed(int characterCount, int roleCount)
    {
        int maxSeed = SeedEncoder.GetMaxSeed(characterCount, roleCount, GameState.ZoneCount);

        // 로비에서 새 게임 설정이 전달된 경우 최우선 적용 (최초 1회)
        if (NewGameConfig.IsSet)
        {
            _startedFromLobby = true;
            if (!string.IsNullOrEmpty(NewGameConfig.StageId))
                _loopSM.StageId = NewGameConfig.StageId;
            _sessionSeed = NewGameConfig.UseRandom
                ? Random.Range(0, maxSeed)
                : Mathf.Clamp(NewGameConfig.Seed, 0, maxSeed - 1);
            NewGameConfig.Clear();
            return _sessionSeed;
        }

        // 세션 시드가 이미 결정됐으면 루프가 바뀌어도 동일 시드 반환
        if (_sessionSeed >= 0)
            return _sessionSeed;

        // 에디터 직접 실행 등 로비를 거치지 않은 경우 StageSetupConfig로 시드 결정 (1회)
        if (_setupConfig != null)
        {
            int configSeed = _setupConfig.GetOrGenerateSeed(characterCount, roleCount);
            if (configSeed < 0 || configSeed >= maxSeed)
            {
                Debug.LogWarning($"[GameSetupState] StageSetupConfig 시드({configSeed})가 유효 범위(0~{maxSeed - 1})를 벗어났습니다. 랜덤 시드로 대체합니다.");
                configSeed = Random.Range(0, maxSeed);
            }
            _sessionSeed = configSeed;
            return _sessionSeed;
        }

        _sessionSeed = Random.Range(0, maxSeed);
        return _sessionSeed;
    }
}
