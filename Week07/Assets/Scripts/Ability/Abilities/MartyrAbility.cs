using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 순교자 능력: 같은 구역에서 이번 턴 사망 마크된 캐릭터가 있을 경우,
/// CharacterId가 가장 낮은 1명의 사망 마크를 제거하고 자신이 대신 사망합니다.
///
/// 스킵 조건: 순교자 자신이 이미 사망 마크된 경우 (RoleAbilityProcessor 공통 규칙)
/// </summary>
[CreateAssetMenu(fileName = "Ability_Martyr", menuName = "MafiaGame/Ability/Martyr")]
public class MartyrAbility : AbilityConfig
{
    public override void Execute(int ownerId, IGameState gameState)
    {
        // 자신이 이미 사망 마크된 경우: 아무도 구하지 않고 그냥 사망
        if (gameState.IsMarkedForDeath(ownerId)) return;

        int zone       = gameState.GetZone(ownerId);
        var candidates = new List<int>();

        foreach (var mark in gameState.GetAllDeathMarks())
        {
            if (mark.TargetCharacterId == ownerId) continue; // 자신은 구하지 않음
            var c = gameState.GetCharacter(mark.TargetCharacterId);
            if (c == null || !c.IsAlive) continue;
            if (gameState.GetZone(mark.TargetCharacterId) == zone)
                candidates.Add(mark.TargetCharacterId);
        }

        if (candidates.Count == 0) return;

        candidates.Sort();
        gameState.ClearDeathMark(candidates[0]);
        gameState.MarkForDeath(ownerId, RoleType.Martyr, ownerId);
    }
}
