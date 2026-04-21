using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파도 구역 효과: 턴 종료 후 이 구역에 있는 캐릭터를 지정 구역으로 강제 이동시킵니다.
/// 플레이어 이동 판정(MovedThisTurn)에 영향을 주지 않으므로 물귀신 능력에 걸립니다.
/// </summary>
[CreateAssetMenu(fileName = "Effect_WaveZone", menuName = "MafiaGame/ZoneEffect/WaveZone")]
public class WaveZoneEffect : ZoneEffectConfig
{
    [Tooltip("이 구역 캐릭터가 이동할 목적지 구역 ID")]
    [SerializeField] private int _targetZoneId;

    public int TargetZoneId => _targetZoneId;

    public override void Apply(int zoneId, GameState gameState)
    {
        var targets = new List<ICharacterStatus>(gameState.GetCharactersInZone(zoneId));
        foreach (var c in targets)
            gameState.ForceMoveCharacter(c.CharacterId, _targetZoneId);
    }
}
