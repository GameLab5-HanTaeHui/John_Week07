/// <summary>
/// 게임에 등장하는 직업 종류를 정의합니다.
/// 스테이지별로 등장 여부가 달라질 수 있으며, StageRoleConfig를 통해 관리됩니다.
/// 새 직업 추가 시 이 enum에만 항목을 추가하면 됩니다.
/// </summary>
public enum RoleType
{
    Murderer,    // 살인자
    Deputy,      // 대리자
    Fanatic,     // 광신도
    Protagonist, // 주인공
    FriendA,     // 친구A
    FriendB,     // 친구B
    Variable,    // 변수
    WaterGhost,  // 물귀신
    Martyr,      // 순교자
    LoverC,      // 연인C
    LoverD,      // 연인D
    Punisher     // 응징자
}
