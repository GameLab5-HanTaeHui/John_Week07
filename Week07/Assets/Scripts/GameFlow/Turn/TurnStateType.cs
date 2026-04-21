/// <summary>
/// 턴 레벨 상태 목록입니다.
/// PlayerAction → RoleActivation → ResultDisplay → TurnEnd
/// </summary>
public enum TurnStateType
{
    PlayerAction,    // 플레이어가 7개 캐릭터 행동 선택 (추리 선언 가능)
    RoleActivation,  // 직업 능력 순서대로 발동
    ResultDisplay,   // 이번 턴 결과 표시
    TurnEnd          // 턴 정리 및 완료 신호
}
