using UnityEngine;

/// <summary>
/// 모든 직업 능력의 베이스 ScriptableObject입니다.
/// 직업마다 이 클래스를 상속한 구체 능력 에셋을 1개 생성하고 RoleData에 연결하세요.
///
/// 확장 방법:
///   1. AbilityConfig를 상속한 클래스 생성
///   2. Execute()에 능력 로직 구현
///   3. 에셋 생성 후 해당 RoleData.AbilityConfig에 연결
///   4. RoleActivationOrderConfig의 실행 순서 목록에 추가
///
/// 능력이 없는 직업(친구B 등)은 RoleData의 AbilityConfig 필드를 비워두세요.
/// </summary>
public abstract class AbilityConfig : ScriptableObject
{
    /// <summary>
    /// 직업 능력을 실행합니다.
    /// ownerId   : 이 능력을 소유한 캐릭터의 고정 ID
    /// gameState : 게임 상태 조회 및 사망 마크 조작 인터페이스
    /// </summary>
    public abstract void Execute(int ownerId, IGameState gameState);
}
