using UnityEngine;

/// <summary>
/// 주인공 능력: 사망 면역 (패시브)
///
/// 구현 위치: GameState.ConfirmDeaths() — 사망 확정 전 주인공의 마크를 제거합니다.
/// 모든 원인의 사망 마크를 무효화하며 발동 순서에 영향을 받지 않습니다.
///
/// 이 Execute()는 호출되지 않습니다.
/// RoleActivationOrderConfig에서 이 에셋을 제거하거나 AbilityConfig를 비워도 됩니다.
/// </summary>
[CreateAssetMenu(fileName = "AbilityConfig_Protagonist", menuName = "MafiaGame/Ability/Protagonist")]
public class ProtagonistAbility : AbilityConfig
{
    public override void Execute(int ownerId, IGameState gameState) { }
}
