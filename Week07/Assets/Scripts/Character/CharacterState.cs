/// <summary>
/// 캐릭터 1명의 런타임 상태를 관리하는 클래스입니다.
/// ICharacterStatus를 구현해 능력 시스템(IGameState)에서 읽기 전용으로 사용됩니다.
///
/// 상태 변경 흐름:
///   GameSetupState  → SetInitialZone()            초기 배치
///   PlayerAction    → Move() or Wait()            행동 적용
///   RoleActivation  → (IGameState 경유로 처리)    능력 발동
///   Step 8 확정     → Die()                       사망 확정
///   LoopStartState  → ResetPositionToInitial()    루프 초기화
/// </summary>
public class CharacterState : ICharacterStatus
{
    // ── ICharacterStatus ─────────────────────────────────────────────────
    public int    CharacterId   => _data.CharacterId;
    public string CharacterName => _data.CharacterName;
    public bool   IsAlive       { get; private set; }

    // ── 위치 데이터 ──────────────────────────────────────────────────────
    /// <summary>GameSetupState에서 배정된 초기 구역 ID. 루프 초기화 시 이 값으로 복원합니다.</summary>
    public int InitialZone  { get; private set; }

    /// <summary>이번 턴 이동 후 현재 구역 ID.</summary>
    public int CurrentZone  { get; private set; }

    /// <summary>이번 턴 이동 전 구역 ID. 대기 시에도 갱신됩니다. 광신도 능력 판정에 사용됩니다.</summary>
    public int PreviousZone { get; private set; }

    public CharacterData Data => _data;

    private readonly CharacterData _data;

    public CharacterState(CharacterData data)
    {
        _data   = data;
        IsAlive = true;
    }

    // ── 초기화 ───────────────────────────────────────────────────────────

    /// <summary>
    /// GameSetupState에서 초기 구역을 배정합니다.
    /// Initial/Current/Previous 모두 동일한 값으로 설정됩니다.
    /// </summary>
    public void SetInitialZone(int zoneId)
    {
        InitialZone  = zoneId;
        CurrentZone  = zoneId;
        PreviousZone = zoneId;
    }

    // ── 턴 행동 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 이동 행동을 적용합니다.
    /// PreviousZone에 현재 구역을 저장한 뒤 CurrentZone을 갱신합니다.
    /// </summary>
    public void Move(int targetZoneId)
    {
        PreviousZone = CurrentZone;
        CurrentZone  = targetZoneId;
    }

    /// <summary>
    /// 대기 행동을 적용합니다.
    /// PreviousZone을 갱신해 "이동하지 않았음"을 광신도 판정이 감지할 수 있게 합니다.
    /// </summary>
    public void Wait()
    {
        PreviousZone = CurrentZone;
    }

    /// <summary>
    /// 턴 시작 시 PreviousZone을 현재 위치로 동기화합니다.
    /// 이번 턴에 행동이 확정되지 않은 캐릭터도 "이동 없음" 상태가 됩니다.
    /// </summary>
    public void SyncPreviousZone()
    {
        PreviousZone = CurrentZone;
    }

    // ── 구역 강제 이동 ────────────────────────────────────────────────────

    /// <summary>
    /// 파도 구역 등 외부 효과에 의한 강제 이동입니다.
    /// PreviousZone을 갱신하지 않으므로 물귀신 능력 판정에 영향을 주지 않습니다.
    /// </summary>
    public void ForceMove(int targetZoneId)
    {
        CurrentZone = targetZoneId;
    }

    // ── 생사 관리 ────────────────────────────────────────────────────────

    /// <summary>사망을 확정합니다. RoleActivation 8단계(결과 확정)에서 호출하세요.</summary>
    public void Die()
    {
        IsAlive = false;
    }

    /// <summary>캐릭터를 부활시킵니다. 루프 초기화 시 필요하면 LoopStartState에서 호출하세요.</summary>
    public void Revive()
    {
        IsAlive = true;
    }

    // ── 루프 초기화 ──────────────────────────────────────────────────────

    /// <summary>
    /// 위치를 초기 배치 구역으로 복원합니다.
    /// 사망 상태는 변경하지 않습니다. 부활이 필요하면 Revive()를 별도 호출하세요.
    /// </summary>
    public void ResetPositionToInitial()
    {
        CurrentZone  = InitialZone;
        PreviousZone = InitialZone;
    }
}
