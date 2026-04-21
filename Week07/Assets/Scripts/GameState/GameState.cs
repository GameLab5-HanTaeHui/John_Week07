using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IGameState의 구체 구현체입니다.
/// 캐릭터 상태 목록, 역할 배정 테이블, 사망 마크, 이벤트 로그를 통합 관리합니다.
///
/// 사용 계층:
///   능력 시스템 (AbilityConfig)  → IGameState 인터페이스로만 접근
///   State 클래스 (GameSetupState 등) → GameState 직접 참조로 관리 메서드 호출
///
/// 턴 처리 흐름:
///   1. ResetForNewTurn()          사망 마크 · 로그 초기화
///   2. ApplyMove() / ApplyWait()  플레이어 행동 적용
///   3. RoleAbilityProcessor.Process(this)  능력 순서 처리
///   4. ConfirmDeaths()            마크된 캐릭터 사망 확정
/// </summary>
public class GameState : IGameState
{
    public const int ZoneCount = 4;

    private readonly List<CharacterState> _characters;
    private readonly RoleAssignmentTable  _roleTable;
    private readonly List<DeathRecord>    _deathMarks = new List<DeathRecord>();
    private readonly List<string>         _eventLog   = new List<string>();

    public int TotalDeathsThisLoop { get; private set; }

    /// <summary>이번 턴에 확정된 사망자 수입니다. NoDeathOrMassDeathCondition에서 사용합니다.</summary>
    public int DeathsThisTurn { get; private set; }

    /// <summary>
    /// 이 루프의 첫 번째 PlayerAction 진입 여부입니다.
    /// true인 동안 파도 구역 효과를 건너뜁니다. 첫 턴 Enter() 직후 false로 전환됩니다.
    /// </summary>
    public bool IsFirstTurnOfLoop { get; set; } = true;

    // zoneId 인덱스로 접근. InitZoneRules()로 초기화됩니다.
    private bool[] _abilityDisabledZones = new bool[ZoneCount];

    // 구역 효과 배열. zoneId 인덱스로 접근. CharacterSpawner.ApplyZoneRulesToGameState()로 초기화됩니다.
    private ZoneEffectConfig[] _zoneEffects;

    // 다음 턴 역할 발동 단계에서 처리될 지연 사망 목록.
    // 연인D 사망 시 연인C의 사망을 1턴 뒤로 등록할 때 사용합니다.
    // GameState는 루프마다 재생성되므로 루프 간 오염은 구조적으로 차단됩니다.
    private readonly List<DeathRecord> _delayedDeaths = new List<DeathRecord>();

    /// <param name="characters">CharacterRegistry에서 생성된 CharacterState 목록</param>
    /// <param name="roleTable">GameSetupState에서 채워진 역할 배정 테이블</param>
    public GameState(List<CharacterState> characters, RoleAssignmentTable roleTable)
    {
        _characters = characters;
        _roleTable  = roleTable;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 관리 메서드 — State 클래스에서 직접 호출
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 턴 능력 처리 시작 전 호출합니다.
    /// 이전 턴의 사망 마크·로그·턴 사망 카운터를 초기화합니다.
    /// </summary>
    public void ResetForNewTurn()
    {
        _deathMarks.Clear();
        _eventLog.Clear();
        DeathsThisTurn = 0;
    }

    /// <summary>
    /// 모든 캐릭터의 PreviousZone을 CurrentZone으로 동기화합니다.
    /// PlayerActionState.Enter()에서 턴 시작 시 호출해 이동 전 위치를 기록합니다.
    /// </summary>
    public void SyncAllPreviousZones()
    {
        foreach (var c in _characters)
            c.SyncPreviousZone();
    }

    /// <summary>
    /// 이동 행동을 적용합니다. PlayerActionState에서 플레이어 입력 확정 시 호출합니다.
    /// </summary>
    public void ApplyMove(int characterId, int targetZoneId)
    {
        GetCharacterInternal(characterId)?.Move(targetZoneId);
    }

    /// <summary>
    /// 대기 행동을 적용합니다. PlayerActionState에서 플레이어 입력 확정 시 호출합니다.
    /// </summary>
    public void ApplyWait(int characterId)
    {
        GetCharacterInternal(characterId)?.Wait();
    }

    /// <summary>
    /// 사망 마크된 캐릭터를 모두 사망 처리합니다.
    /// RoleActivation 8단계(결과 확정) 시 호출합니다.
    /// </summary>
    public void ConfirmDeaths()
    {
        // 주인공 면역: 사망 확정 전 마크 제거 (발동 순서 무관)
        var protagonistStatus = GetCharacterByRole(RoleType.Protagonist);
        if (protagonistStatus != null)
            ClearDeathMark(protagonistStatus.CharacterId);

        // 대리자 ID 캐시 (살아있고 자신이 사망 마크되지 않은 경우에만 유효)
        int deputyId = -1;
        var deputyStatus = GetCharacterByRole(RoleType.Deputy);
        if (deputyStatus != null && !IsMarkedForDeath(deputyStatus.CharacterId))
            deputyId = deputyStatus.CharacterId;

        bool deputySubstituted = false;

        // 응징자 패시브: 자신을 타겟팅한 캐릭터 전원 반격 마킹 후 자신의 사망 마크 제거 (응징자 생존)
        var punisherStatus = GetCharacterByRole(RoleType.Punisher);
        if (punisherStatus != null && IsMarkedForDeath(punisherStatus.CharacterId))
        {
            int punisherId = punisherStatus.CharacterId;
            var snapshot = new List<DeathRecord>(_deathMarks);
            foreach (var mark in snapshot)
            {
                if (mark.TargetCharacterId != punisherId) continue;
                if (mark.SourceCharacterId >= 0)
                    MarkForDeath(mark.SourceCharacterId, RoleType.Punisher, punisherId);
            }
            ClearDeathMark(punisherId);
        }

        // 순교자 사망 지연: MartyrAbility가 자기희생 마크를 남긴 경우 Phase 1에서 즉시 죽이지 않고
        // 모든 처리가 끝난 뒤 맨 마지막에 사망 확정합니다 (Phase 2 체인에서도 스킬 사용 가능).
        bool martyrDeathDeferred = false;
        int  martyrDeferredId    = -1;
        var  martyrStatusCheck   = GetCharacterByRole(RoleType.Martyr);
        if (martyrStatusCheck != null && IsMarkedForDeath(martyrStatusCheck.CharacterId))
        {
            martyrDeferredId    = martyrStatusCheck.CharacterId;
            martyrDeathDeferred = true;
            ClearDeathMark(martyrDeferredId); // Phase 1에서 즉시 사망 방지
        }

        // Phase 1: 마크된 사망 확정, 연쇄·대리 대상 수집
        var chainDeaths = new List<(CharacterState character, RoleType causeRole, int sourceId)>();

        foreach (var record in _deathMarks)
        {
            var character = GetCharacterInternal(record.TargetCharacterId);
            if (character == null || !character.IsAlive) continue;

            // 대리자 패시브: 살인자/광신도에 의한 사망만 대리 가능
            bool isDeputyEligibleCause = record.CauseRole == RoleType.Murderer || record.CauseRole == RoleType.Fanatic;
            if (!deputySubstituted && deputyId != -1 && record.TargetCharacterId != deputyId && isDeputyEligibleCause)
            {
                var deputy = GetCharacterInternal(deputyId);
                if (deputy != null && deputy.IsAlive && deputy.CurrentZone == character.CurrentZone)
                {
                    deputySubstituted = true;
                    chainDeaths.Add((deputy, record.CauseRole, record.SourceCharacterId));
                    continue; // 원래 대상은 생존
                }
            }

            character.Die();
            TotalDeathsThisLoop++;
            DeathsThisTurn++;
            AddLog($"{character.CharacterName} 사망");

            RoleType role = GetRole(record.TargetCharacterId);

            // 친구A 사망 시 친구B를 연쇄 대상으로 수집
            if (role == RoleType.FriendA)
            {
                var friendBStatus = GetCharacterByRole(RoleType.FriendB);
                if (friendBStatus != null)
                {
                    var friendB = GetCharacterInternal(friendBStatus.CharacterId);
                    if (friendB != null && friendB.IsAlive)
                        chainDeaths.Add((friendB, RoleType.FriendA, record.TargetCharacterId));
                }
            }

            // 연인C 사망 시 연인D 즉시 연쇄 (단, 연인D가 이미 사망 마크된 경우 무시)
            if (role == RoleType.LoverC)
            {
                var loverDStatus = GetCharacterByRole(RoleType.LoverD);
                if (loverDStatus != null && !IsMarkedForDeath(loverDStatus.CharacterId))
                    chainDeaths.Add((GetCharacterInternal(loverDStatus.CharacterId), RoleType.LoverC, record.TargetCharacterId));
            }

            // 연인D 사망 시 연인C 1턴 뒤 사망 등록 (연인C가 이미 사망 마크된 경우 무시)
            if (role == RoleType.LoverD)
            {
                var loverCStatus = GetCharacterByRole(RoleType.LoverC);
                if (loverCStatus != null && !IsMarkedForDeath(loverCStatus.CharacterId))
                    RegisterDelayedDeath(loverCStatus.CharacterId, RoleType.LoverD, record.TargetCharacterId);
            }
        }

        // Phase 2: 연쇄·대리 사망 확정 + _deathMarks에 추가 (사망 카운트 포함)
        // 순교자가 Phase 1 희생을 아직 사용하지 않은 경우에만 체인 대상 구조 가능.
        foreach (var (character, causeRole, sourceId) in chainDeaths)
        {
            if (!character.IsAlive) continue;

            // 순교자 패시브: 이번 턴 아직 희생을 쓰지 않았고, 같은 구역이면 체인 대상 구조
            if (!martyrDeathDeferred && TryMartyrSubstituteChain(character, out int chainMartyrId))
            {
                martyrDeathDeferred = true;
                martyrDeferredId    = chainMartyrId;
                continue; // 원래 대상 생존
            }

            character.Die();
            TotalDeathsThisLoop++;
            DeathsThisTurn++;
            AddLog($"{character.CharacterName} 사망");
            _deathMarks.Add(new DeathRecord(character.CharacterId, causeRole, sourceId));

            // 연쇄로 연인D가 사망할 경우에도 연인C 1턴 뒤 사망 등록
            if (GetRole(character.CharacterId) == RoleType.LoverD)
            {
                var loverCStatus = GetCharacterByRole(RoleType.LoverC);
                if (loverCStatus != null && !IsMarkedForDeath(loverCStatus.CharacterId))
                    RegisterDelayedDeath(loverCStatus.CharacterId, RoleType.LoverD, character.CharacterId);
            }
        }

        // 최종: 순교자 사망 확정 (모든 사망 처리가 끝난 뒤 마지막으로 사망)
        if (martyrDeathDeferred && martyrDeferredId >= 0)
        {
            var martyr = GetCharacterInternal(martyrDeferredId);
            if (martyr != null && martyr.IsAlive)
            {
                martyr.Die();
                TotalDeathsThisLoop++;
                DeathsThisTurn++;
                AddLog($"{martyr.CharacterName} 사망");
                _deathMarks.Add(new DeathRecord(martyrDeferredId, RoleType.Martyr, martyrDeferredId));
            }
        }
    }

    /// <summary>
    /// 체인 사망(Phase 2) 대상과 같은 구역에 살아있는 순교자가 있으면 대상을 구하고
    /// 순교자 ID를 반환합니다. 순교자는 즉시 죽이지 않고 호출부에서 지연 사망 처리합니다.
    /// </summary>
    private bool TryMartyrSubstituteChain(CharacterState target, out int martyrId)
    {
        martyrId = -1;
        var martyrStatus = GetCharacterByRole(RoleType.Martyr);
        if (martyrStatus == null) return false;

        var martyr = GetCharacterInternal(martyrStatus.CharacterId);
        if (martyr == null || !martyr.IsAlive) return false;
        if (martyr.CharacterId == target.CharacterId) return false;
        if (martyr.CurrentZone != target.CurrentZone) return false;

        martyrId = martyr.CharacterId;
        return true;
    }

    /// <summary>
    /// 모든 캐릭터 위치를 초기 배치로 복원합니다.
    /// LoopStartState에서 호출합니다. 생사 상태는 변경하지 않습니다.
    /// </summary>
    public void ResetAllPositionsToInitial()
    {
        foreach (var c in _characters)
            c.ResetPositionToInitial();
    }

    /// <summary>
    /// 모든 캐릭터를 부활시킵니다.
    /// 루프 리셋 시 사망 캐릭터를 되살릴 필요가 있으면 LoopStartState에서 호출합니다.
    /// </summary>
    public void ReviveAll()
    {
        foreach (var c in _characters)
            c.Revive();
        TotalDeathsThisLoop = 0;
    }

    // ── 구역 효과 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 구역별 효과 배열을 설정합니다.
    /// CharacterSpawner.ApplyZoneRulesToGameState()에서 호출합니다.
    /// </summary>
    public void InitZoneEffects(ZoneEffectConfig[] effects)
    {
        _zoneEffects = effects;
    }

    /// <summary>
    /// 모든 구역 효과를 적용합니다 (예: 파도 구역 강제 이동).
    /// PlayerActionState.Enter() 에서 SyncAllPreviousZones() 전에 호출합니다.
    /// </summary>
    public void ApplyWaveZoneEffects()
    {
        if (_zoneEffects == null) return;
        for (int i = 0; i < _zoneEffects.Length && i < ZoneCount; i++)
            _zoneEffects[i]?.Apply(i, this);
    }

    /// <summary>
    /// 구역 효과(파도 구역 등)에 의한 강제 이동입니다.
    /// CharacterState.ForceMove()를 호출하며 PreviousZone에 영향을 주지 않습니다.
    /// </summary>
    public void ForceMoveCharacter(int characterId, int targetZoneId)
    {
        GetCharacterInternal(characterId)?.ForceMove(targetZoneId);
    }

    // ── 지연 사망 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 다음 턴 역할 발동 단계 시작 시 처리될 사망을 등록합니다.
    /// 연인D 사망 시 연인C의 1턴 뒤 사망에 사용합니다.
    /// </summary>
    public void RegisterDelayedDeath(int characterId, RoleType causeRole, int sourceCharacterId)
    {
        if (_delayedDeaths.Exists(d => d.TargetCharacterId == characterId)) return;
        _delayedDeaths.Add(new DeathRecord(characterId, causeRole, sourceCharacterId));
    }

    /// <summary>
    /// 지연 사망 목록을 꺼내고 내부 목록을 비웁니다.
    /// RoleActivationState.Enter() 시작 시 1회 호출합니다.
    /// </summary>
    public List<DeathRecord> ConsumeDelayedDeaths()
    {
        var result = new List<DeathRecord>(_delayedDeaths);
        _delayedDeaths.Clear();
        return result;
    }

    /// <summary>현재 턴 이벤트 로그를 반환합니다. ResultDisplayState에서 UI에 전달합니다.</summary>
    public IReadOnlyList<string> GetEventLog() => _eventLog;

    /// <summary>이 게임에 참여하는 캐릭터 수입니다. PlayerActionState의 전원 확정 판정에 사용합니다.</summary>
    public int CharacterCount => _characters.Count;

    /// <summary>전체 캐릭터 ID 목록을 반환합니다. PlayerActionState.ForceEnd()에서 미확정 캐릭터 처리에 사용합니다.</summary>
    public IReadOnlyList<int> GetAllCharacterIds()
    {
        var ids = new List<int>(_characters.Count);
        foreach (var c in _characters) ids.Add(c.CharacterId);
        return ids;
    }

    /// <summary>
    /// CharacterSpawner에서 Init() 호출 시 사용합니다.
    /// IGameState.GetCharacter()가 ICharacterStatus를 반환하므로 CharacterState가 필요할 때 이 메서드를 씁니다.
    /// </summary>
    public CharacterState GetCharacterState(int characterId)
        => GetCharacterInternal(characterId);

    /// <summary>
    /// 캐릭터 초기 구역을 배정합니다. GameSetupState에서 랜덤 배치 시 호출합니다.
    /// </summary>
    public void SetCharacterInitialZone(int characterId, int zoneId)
    {
        GetCharacterInternal(characterId)?.SetInitialZone(zoneId);
    }

    /// <summary>
    /// 재개 스냅샷을 적용합니다. 각 캐릭터의 위치와 생사 상태를 저장된 값으로 복원합니다.
    /// GameSetupState에서 세이브 파일로부터 게임을 재개할 때 호출합니다.
    /// </summary>
    public void ApplyResumeSnapshot(IReadOnlyList<CharacterPositionSnapshot> snapshots)
    {
        foreach (var snap in snapshots)
        {
            var c = GetCharacterInternal(snap.CharacterId);
            if (c == null) continue;
            c.SetInitialZone(snap.ZoneId);
            if (!snap.IsAlive) c.Die();
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // IGameState 구현 — 능력 시스템에서 인터페이스로 접근
    // ══════════════════════════════════════════════════════════════════════

    public ICharacterStatus GetCharacter(int characterId)
        => GetCharacterInternal(characterId);

    public ICharacterStatus GetCharacterByRole(RoleType roleType)
    {
        foreach (var entry in _roleTable.GetAll())
        {
            if (entry.Value != null && entry.Value.RoleType == roleType)
            {
                var character = GetCharacterInternal(entry.Key);
                // 사망 캐릭터는 능력 발동 대상에서 제외
                return (character != null && character.IsAlive) ? character : null;
            }
        }
        return null;
    }

    public IReadOnlyList<ICharacterStatus> GetCharactersInZone(int zoneId)
    {
        var result = new List<ICharacterStatus>();
        foreach (var c in _characters)
        {
            if (c.IsAlive && c.CurrentZone == zoneId)
                result.Add(c);
        }
        return result;
    }

    public int GetZone(int characterId)
        => GetCharacterInternal(characterId)?.CurrentZone ?? -1;

    public int GetPreviousZone(int characterId)
        => GetCharacterInternal(characterId)?.PreviousZone ?? -1;

    public RoleType GetRole(int characterId)
    {
        _roleTable.TryGetRole(characterId, out var roleData);
        return roleData != null ? roleData.RoleType : default;
    }

    public bool IsMarkedForDeath(int characterId)
        => _deathMarks.Exists(r => r.TargetCharacterId == characterId);

    public IReadOnlyList<DeathRecord> GetAllDeathMarks()
        => _deathMarks;

    /// <summary>
    /// 사망 마크를 추가합니다.
    /// 이미 마크된 캐릭터에 대한 중복 마크는 무시됩니다 (첫 번째 원인 유지).
    /// </summary>
    public void MarkForDeath(int characterId, RoleType causeRole, int sourceCharacterId)
    {
        if (IsMarkedForDeath(characterId)) return;
        _deathMarks.Add(new DeathRecord(characterId, causeRole, sourceCharacterId));
    }

    public void ClearDeathMark(int characterId)
    {
        _deathMarks.RemoveAll(r => r.TargetCharacterId == characterId);
    }

    public void SubstituteDeathMark(int originalCharacterId, int substituteCharacterId)
    {
        var original = _deathMarks.Find(r => r.TargetCharacterId == originalCharacterId);
        if (original == null) return;

        _deathMarks.Remove(original);

        // substituteCharacterId가 이미 마크되어 있지 않은 경우에만 추가
        if (!IsMarkedForDeath(substituteCharacterId))
            _deathMarks.Add(new DeathRecord(substituteCharacterId, original.CauseRole, original.SourceCharacterId));
    }

    /// <summary>
    /// 구역별 능력 비활성화 규칙을 설정합니다.
    /// CharacterSpawner.ApplyZoneRulesToGameState()에서 호출합니다.
    /// </summary>
    public void InitZoneRules(bool[] abilityDisabledZones)
    {
        for (int i = 0; i < ZoneCount && i < abilityDisabledZones.Length; i++)
            _abilityDisabledZones[i] = abilityDisabledZones[i];
    }

    public bool IsAbilityDisabledInZone(int zoneId)
        => zoneId >= 0 && zoneId < _abilityDisabledZones.Length && _abilityDisabledZones[zoneId];

    public void AddLog(string message)
    {
        _eventLog.Add(message);
        Debug.Log($"[GameState] {message}");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Private
    // ══════════════════════════════════════════════════════════════════════

    private CharacterState GetCharacterInternal(int characterId)
        => _characters.Find(c => c.CharacterId == characterId);
}
