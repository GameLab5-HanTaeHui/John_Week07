using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 로비에서 사용하는 씬 이름 / 스테이지 ID / 시드를 한 곳에서 관리합니다.
    ///
    /// ─── 사용법 ──────────────────────────────────────────────────────────
    ///   Assets/HTH/Campaign/LobbyConfig.asset 을 생성합니다.
    ///   (우클릭 → Create → HTH/Campaign/LobbyConfig)
    ///   이후 모든 씬/시드 관련 값은 이 에셋에서만 수정합니다.
    ///
    ///   CampaignLobbyNavigator, LobbyUI, LobbyPresetSeedButton이
    ///   이 에셋을 Inspector에서 참조합니다.
    ///
    /// ─── 필드 ────────────────────────────────────────────────────────────
    ///   Campaign Scene Name     → 캠페인 인게임 씬 이름
    ///   Tutorial Scene Name     → 튜토리얼 씬 이름
    ///   Tutorial Retry Scene    → 튜토리얼 다시하기 씬 이름
    ///   Campaign Stage Id       → 캠페인 스테이지 ID (CampaignModeManager와 동일하게)
    ///   Tutorial Fixed Seed     → 튜토리얼 고정 시드
    ///   Default Game Seed       → 기본모드 시작 시드 (0 = 고정 시드 없음)
    /// </summary>
    [CreateAssetMenu(
        fileName = "LobbyConfig",
        menuName = "HTH/Campaign/LobbyConfig")]
    public class LobbyConfig : ScriptableObject
    {
        [Header("씬 이름")]
        [Tooltip("캠페인 인게임 씬 이름입니다.")]
        public string CampaignSceneName = "Stage_1";

        [Tooltip("튜토리얼 씬 이름입니다.")]
        public string TutorialSceneName = "TutorialScene";

        [Tooltip("튜토리얼 다시하기 씬 이름입니다.")]
        public string TutorialRetrySceneName = "TutorialRetryScene";

        [Tooltip("기본 인게임 씬 이름입니다. (캠페인 외 기본모드 사용)")]
        public string DefaultGameSceneName = "GameScene";

        [Header("스테이지 ID")]
        [Tooltip("캠페인 스테이지 ID입니다.\n" +
                 "CampaignModeManager._editorStageId와 동일하게 설정하세요.\n" +
                 "예: CampaignMode")]
        public string CampaignStageId = "CampaignMode";

        [Tooltip("기본모드 스테이지 ID입니다.\n" +
                 "LobbyUI의 시드 시작 / 랜덤 시작에서 사용합니다.")]
        public string DefaultStageId = "Stage_1";

        [Header("시드")]
        [Tooltip("튜토리얼 고정 시드입니다.")]
        public int TutorialFixedSeed = 0;

        [Tooltip("캠페인/기본모드 시작 시드입니다. 0이면 고정 시드 없이 시작합니다.")]
        public int DefaultGameSeed = 0;
    }
}