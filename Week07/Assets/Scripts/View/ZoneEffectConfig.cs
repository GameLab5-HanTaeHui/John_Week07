using UnityEngine;

/// <summary>
/// 구역 효과를 정의하는 추상 ScriptableObject입니다.
/// 새 구역 효과 추가 시 이 클래스를 상속하고 Apply()만 구현하세요.
/// ZonePoint의 Inspector에서 연결합니다.
///
/// 발동 타이밍: 결과창 출력 후, 다음 턴 PlayerAction 시작 직전 (ApplyWaveZoneEffects 호출 시)
/// </summary>
public abstract class ZoneEffectConfig : ScriptableObject
{
    /// <summary>이 구역 효과를 GameState에 적용합니다.</summary>
    /// <param name="zoneId">효과가 발동하는 구역 ID</param>
    /// <param name="gameState">현재 게임 상태</param>
    public abstract void Apply(int zoneId, GameState gameState);
}
