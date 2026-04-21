using UnityEngine;

/// <summary>
/// 물귀신 능력: 이번 턴 플레이어가 이동시키지 않았다면 같은 구역 전원(자신 포함)을 사망 마킹합니다.
///
/// 발동 조건: CurrentZone == PreviousZone (플레이어가 Wait를 선택한 경우)
///   - 파도 구역 강제 이동은 PreviousZone을 갱신하지 않으므로 이동으로 간주되지 않습니다.
/// </summary>
[CreateAssetMenu(fileName = "Ability_WaterGhost", menuName = "MafiaGame/Ability/WaterGhost")]
public class WaterGhostAbility : AbilityConfig
{
    public override void Execute(int ownerId, IGameState gameState)
    {
        if (gameState.GetZone(ownerId) != gameState.GetPreviousZone(ownerId)) return;

        int zone = gameState.GetZone(ownerId);
        foreach (var c in gameState.GetCharactersInZone(zone))
            gameState.MarkForDeath(c.CharacterId, RoleType.WaterGhost, ownerId);
    }
}
