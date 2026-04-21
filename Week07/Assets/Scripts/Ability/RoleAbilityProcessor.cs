/// <summary>
/// RoleActivationOrderConfig에 정의된 순서대로 직업 능력을 실행합니다.
/// RoleActivationState에서 생성 후 Process()를 호출하세요.
///
/// 스킵 조건 (자동 처리):
///   - 스테이지에 없는 직업 (GetCharacterByRole == null)
///   - AbilityConfig가 없는 직업 (친구B 등 수동 패시브)
///   - 이미 사망한 캐릭터 (대리자 대리 후 재발동 없음 포함)
/// </summary>
public class RoleAbilityProcessor
{
    private readonly RoleActivationOrderConfig _orderConfig;

    public RoleAbilityProcessor(RoleActivationOrderConfig orderConfig)
    {
        _orderConfig = orderConfig;
    }

    /// <summary>
    /// 설정된 순서에 따라 전체 직업 능력을 1회 실행합니다.
    /// RoleActivationState.Enter() 또는 완료 콜백에서 호출하세요.
    /// </summary>
    public void Process(IGameState gameState)
    {
        foreach (var roleData in _orderConfig.ExecutionOrder)
        {
            if (roleData == null || roleData.AbilityConfig == null) continue;

            var character = gameState.GetCharacterByRole(roleData.RoleType);

            // 스테이지에 없거나 이미 사망한 캐릭터는 능력 발동 없음
            if (character == null || !character.IsAlive) continue;

            // 이번 턴 앞선 능력에 의해 사망 마크된 캐릭터는 이후 능력 발동 없음 (우선순위 규칙)
            if (gameState.IsMarkedForDeath(character.CharacterId)) continue;

            // 능력이 봉인된 구역에 위치한 캐릭터는 능력 발동 없음
            int zone = gameState.GetZone(character.CharacterId);
            if (gameState.IsAbilityDisabledInZone(zone)) continue;

            roleData.AbilityConfig.Execute(character.CharacterId, gameState);
        }
    }
}
