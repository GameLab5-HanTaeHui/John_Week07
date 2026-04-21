using System.Collections.Generic;

/// <summary>
/// 직업 능력이 게임 상태를 조회·수정하기 위한 인터페이스입니다.
/// 캐릭터/구역 시스템 구현 시 이 인터페이스를 구현하세요.
///
/// 사망 마크 처리 흐름:
///   능력 실행(2~7단계) → 마크 누적 → 8단계에서 마크된 캐릭터 일괄 사망 확정
/// </summary>
public interface IGameState
{
    // ── 캐릭터 조회 ─────────────────────────────────────────────────────

    ICharacterStatus GetCharacter(int characterId);

    /// <summary>전체 캐릭터 ID 목록을 반환합니다.</summary>
    IReadOnlyList<int> GetAllCharacterIds();

    /// <summary>해당 역할을 가진 캐릭터를 반환합니다. 현재 스테이지에 없으면 null을 반환합니다.</summary>
    ICharacterStatus GetCharacterByRole(RoleType roleType);

    /// <summary>특정 구역에 있는 모든 생존 캐릭터를 반환합니다.</summary>
    IReadOnlyList<ICharacterStatus> GetCharactersInZone(int zoneId);

    /// <summary>이번 턴 이동 후 현재 구역 ID를 반환합니다.</summary>
    int GetZone(int characterId);

    /// <summary>이번 턴 이동 전 구역 ID를 반환합니다. 이동하지 않았으면 현재 구역과 동일합니다.</summary>
    int GetPreviousZone(int characterId);

    RoleType GetRole(int characterId);

    // ── 사망 마크 시스템 ─────────────────────────────────────────────────

    bool IsMarkedForDeath(int characterId);

    IReadOnlyList<DeathRecord> GetAllDeathMarks();

    void MarkForDeath(int characterId, RoleType causeRole, int sourceCharacterId);

    /// <summary>사망 마크를 제거합니다. 주인공 면역 처리에 사용됩니다.</summary>
    void ClearDeathMark(int characterId);

    /// <summary>
    /// originalCharacterId의 사망 마크를 substituteCharacterId(대리자)로 이전합니다.
    /// CauseRole은 원본 마크를 유지합니다.
    /// </summary>
    void SubstituteDeathMark(int originalCharacterId, int substituteCharacterId);

    // ── 구역 규칙 ──────────────────────────────────────────────────────────

    /// <summary>해당 구역에서 특수 능력이 비활성화되어 있으면 true를 반환합니다.</summary>
    bool IsAbilityDisabledInZone(int zoneId);

    // ── 지연 사망 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 다음 턴 역할 발동 단계 시작 시 처리될 사망을 등록합니다.
    /// 연인D 패시브(ConfirmDeaths)에서 사용합니다.
    /// </summary>
    void RegisterDelayedDeath(int characterId, RoleType causeRole, int sourceCharacterId);

    // ── 이벤트 로그 ──────────────────────────────────────────────────────

    void AddLog(string message);
}
