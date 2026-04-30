using UnityEngine;

/// <summary>
/// 캠페인 모드 전용 주인공 능력입니다.
///
/// ─── 기본모드와의 차이 ───────────────────────────────────────────────
///   기본모드 ProtagonistAbility:
///     GameState.ConfirmDeaths()에서 주인공 사망 마크를 제거 → 무적
///
///   캠페인 CampaignProtagonistAbility:
///     아무것도 하지 않음 → 주인공도 사망 가능
///     주인공 사망 = 강제퇴고 조건 (CampaignLoopConditionConfig에서 처리)
///
/// ─── 사용법 ──────────────────────────────────────────────────────────
///   RoleActivationOrderConfig에서 기존 ProtagonistAbility 에셋 대신
///   이 에셋(CampaignProtagonistAbility)을 연결하세요.
///   또는 캠페인 전용 StageRoleConfig에서 주인공 능력을 이 에셋으로 교체하세요.
/// </summary>
[CreateAssetMenu(
    fileName = "AbilityConfig_CampaignProtagonist",
    menuName = "HTH/Campaign/Ability/CampaignProtagonist")]
public class CampaignProtagonistAbility : AbilityConfig
{
    /// <summary>캠페인 주인공은 무적이 없습니다. 아무것도 처리하지 않습니다.</summary>
    public override void Execute(int ownerId, IGameState gameState) { }
}