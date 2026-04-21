using UnityEngine;


/// <summary>
/// 대리자 능력: ConfirmDeaths()에서 패시브로 처리됩니다.
/// 이번 턴 같은 구역에서 가장 먼저 사망 확정되는 캐릭터를 대신해 사망합니다.
/// Execute()는 사용하지 않습니다.
/// </summary>
[CreateAssetMenu(fileName = "AbilityConfig_Deputy", menuName = "MafiaGame/Ability/Deputy")]
public class DeputyAbility : AbilityConfig
{
    public override void Execute(int ownerId, IGameState gameState) { }
}
