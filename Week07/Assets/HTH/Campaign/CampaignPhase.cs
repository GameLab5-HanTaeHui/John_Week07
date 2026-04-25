namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 진행 회차입니다.
    ///
    /// ─── 회차 설명 ───────────────────────────────────────────────────────
    ///   None   : 캠페인 모드 비활성 (일반 스테이지 플레이)
    ///   Phase1 : 1회차 — 기존 역할 추리 시스템 (기존 코드 그대로 사용)
    ///   Phase2 : 2회차 — 캠페인 모드 (인물 추리, 탐문 다이얼로그, 대화 조각 수집)
    ///
    /// ─── 진입 조건 ───────────────────────────────────────────────────────
    ///   Phase1 → Phase2: 최종 추리에서 모든 역할을 정확히 맞춘 경우
    ///                    FinalDecisionUI → CampaignModeManager.OnFirstRunCleared()
    /// </summary>
    public enum CampaignPhase
    {
        None = 0,
        Phase1 = 1,
        Phase2 = 2,
    }
}