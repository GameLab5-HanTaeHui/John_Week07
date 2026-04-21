using UnityEngine;

/// <summary>
/// 친구A 능력: 이번 턴에 사망이 확정되는 순간 친구B도 연쇄 사망합니다.
///
/// 구현 위치: GameState.ConfirmDeaths() — 능력 발동 단계가 아닌 사망 확정 단계에서 처리합니다.
///   - 주인공 면역 또는 대리자 치환으로 친구A의 사망이 취소된 경우 친구B는 안전합니다.
///   - 친구B가 이미 사망했거나 스테이지에 없으면 연쇄 없음.
///
/// 이 Execute()는 호출되지 않습니다.
/// RoleActivationOrderConfig에서 이 에셋을 제거하거나 AbilityConfig를 비워도 됩니다.
/// </summary>
[CreateAssetMenu(fileName = "AbilityConfig_FriendA", menuName = "MafiaGame/Ability/FriendA")]
public class FriendAAbility : AbilityConfig
{
    public override void Execute(int ownerId, IGameState gameState) { }
}
