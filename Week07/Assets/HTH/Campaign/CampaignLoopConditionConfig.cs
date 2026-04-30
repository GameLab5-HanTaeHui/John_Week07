using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 모드 전용 강제퇴고 조건입니다.
    ///
    /// ─── 기본모드와의 차이 ───────────────────────────────────────────────
    ///   기본모드: 살인자 사망 / 3명 이상 사망 → 강제퇴고
    ///   캠페인:   주인공(#1 엔비) 사망 시만 → 강제퇴고
    ///
    /// ─── 사용법 ──────────────────────────────────────────────────────────
    ///   캠페인 전용 StageRoleConfig의 Loop Condition 슬롯에
    ///   이 에셋(CampaignLoopConditionConfig)을 연결하세요.
    ///
    /// ─── 연동 ────────────────────────────────────────────────────────────
    ///   RoleActivationState.CheckLoopEndCondition() → ShouldLoop() 호출
    ///   true 반환 시 → CampaignLoopStateMachine.AdvanceLoop()
    ///               → DialogueTriggerManager 대사 출력 차단
    /// </summary>
    [CreateAssetMenu(
        fileName = "LoopCondition_Campaign",
        menuName = "HTH/Campaign/LoopCondition/CampaignLoopCondition")]
    public class CampaignLoopConditionConfig : LoopConditionConfig
    {
        [Header("강제퇴고 활성화")]
        [Tooltip("true  — 주인공 사망 시 강제퇴고 발동 (캠페인 기본값)\n" +
             "false — 강제퇴고 조건 완전 비활성화 (주인공 사망해도 루프 유지)\n\n" +
             "※ 기본모드의 살인자 사망 / 3명 이상 사망 조건은\n" +
             "   캠페인에서 이 설정과 무관하게 항상 비활성화됩니다.")]
        [SerializeField] private bool _enableForcedLoop = true;

        [Header("주인공 캐릭터 ID")]
        [Tooltip("사망 시 강제퇴고를 유발할 캐릭터 ID입니다.\n" +
                 "기본값 1 = 엔비 (주인공)")]
        [SerializeField] private int _protagonistId = 1;

        /// <summary>
        /// 주인공(_protagonistId)이 이번 턴에 사망했으면 true를 반환합니다.
        /// _enableForcedLoop = false이면 항상 false를 반환합니다.
        /// RoleActivationState.ConfirmDeaths() 이후 호출됩니다.
        /// </summary>
        public override bool ShouldLoop(GameState gameState)
        {
            // ★ 강제퇴고 비활성화 시 조건 무시
            if (!_enableForcedLoop) return false;
            if (gameState == null) return false;

            var protagonist = gameState.GetCharacter(_protagonistId);

            // 주인공이 이번 턴에 사망 확정됐으면 강제퇴고
            bool isDead = protagonist != null && !protagonist.IsAlive;

            if (isDead)
                Debug.Log($"[CampaignLoopCondition] 주인공 #{_protagonistId} 사망 — 강제퇴고 발동");

            return isDead;
        }
    }
}