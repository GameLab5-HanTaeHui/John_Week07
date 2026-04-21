using UnityEngine;

/// <summary>
/// 변수 능력: 살인자와 같은 구역에 있으면 살인자를 사망 지정
/// - 살인자가 스테이지에 없거나 이미 사망했으면 발동 없음
/// - 다른 구역에 있으면 발동 없음
/// </summary>
[CreateAssetMenu(fileName = "AbilityConfig_Variable", menuName = "MafiaGame/Ability/Variable")]
public class VariableAbility : AbilityConfig
{
    public override void Execute(int ownerId, IGameState gameState)
    {
        var murderer = gameState.GetCharacterByRole(RoleType.Murderer);
        if (murderer == null || !murderer.IsAlive) return;

        if (gameState.GetZone(ownerId) != gameState.GetZone(murderer.CharacterId)) return;

        gameState.MarkForDeath(murderer.CharacterId, RoleType.Variable, ownerId);
    }
}
