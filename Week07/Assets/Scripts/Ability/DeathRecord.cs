/// <summary>
/// 사망 마크 한 건을 나타내는 불변 데이터 객체입니다.
/// 능력 처리 단계(2~7)에서 생성되며, 8단계(결과 확정)에서 실제 사망으로 반영됩니다.
/// CauseRole은 대리자가 대리 가능 여부(살인자/광신도만)를 판단하는 데 사용됩니다.
/// </summary>
public sealed class DeathRecord
{
    /// <summary>사망 대상 캐릭터 고정 ID</summary>
    public int      TargetCharacterId { get; }

    /// <summary>사망을 일으킨 직업 타입. 대리자 치환 조건 판단에 사용됩니다.</summary>
    public RoleType CauseRole         { get; }

    /// <summary>능력을 발동한 캐릭터 고정 ID</summary>
    public int      SourceCharacterId { get; }

    public DeathRecord(int targetId, RoleType causeRole, int sourceId)
    {
        TargetCharacterId = targetId;
        CauseRole         = causeRole;
        SourceCharacterId = sourceId;
    }
}
