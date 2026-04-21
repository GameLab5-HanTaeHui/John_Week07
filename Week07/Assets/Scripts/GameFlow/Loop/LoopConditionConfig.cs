using UnityEngine;

/// <summary>
/// 스테이지별 루프 강제 종료 조건을 정의하는 추상 ScriptableObject입니다.
/// 새 루프 조건 추가 시 이 클래스를 상속하고 ShouldLoop()만 구현하세요.
/// StageRoleConfig에 연결해 스테이지마다 다른 조건을 사용할 수 있습니다.
/// </summary>
public abstract class LoopConditionConfig : ScriptableObject
{
    /// <summary>
    /// 이번 턴 능력 처리 완료 후 루프를 강제 종료해야 하면 true를 반환합니다.
    /// RoleActivationState에서 ConfirmDeaths() 이후 호출됩니다.
    /// </summary>
    public abstract bool ShouldLoop(GameState gameState);
}
