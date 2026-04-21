using System.Linq;
using UnityEngine;

/// <summary>
/// 광신도 능력: 이동 시 이동 전 구역 캐릭터 1명 사망 (알파벳 역순 우선)
/// - 이동하지 않았으면 발동 없음
/// - 이동 전 구역에 다른 생존 캐릭터가 없으면 발동 없음
/// </summary>
[CreateAssetMenu(fileName = "AbilityConfig_Fanatic", menuName = "MafiaGame/Ability/Fanatic")]
public class FanaticAbility : AbilityConfig
{
    public override void Execute(int ownerId, IGameState gameState)
    {
        int currentZone  = gameState.GetZone(ownerId);
        int previousZone = gameState.GetPreviousZone(ownerId);

        // 이동하지 않았으면 발동 없음
        if (currentZone == previousZone) return;

        var candidates = gameState.GetCharactersInZone(previousZone)
            .Where(c => c.CharacterId != ownerId && c.IsAlive)
            .OrderByDescending(c => c.CharacterName) // 알파벳 역순 → 마지막 알파벳이 가장 앞
            .ToList();

        if (candidates.Count == 0) return;

        var target = candidates[0];
        gameState.MarkForDeath(target.CharacterId, RoleType.Fanatic, ownerId);
    }
}
