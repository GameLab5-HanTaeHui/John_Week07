using System;
using System.Collections.Generic;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 진행 데이터를 JSON으로 직렬화하기 위한 구조체입니다.
    ///
    /// ─── 저장 파일 경로 ──────────────────────────────────────────────────
    ///   Application.persistentDataPath/HTH/campaign_save_{stageId}.json
    ///
    /// ─── 저장 내용 ───────────────────────────────────────────────────────
    ///   대화 조각 수집 목록      (FragmentCollector)
    ///   재생된 ComboId 목록      (DialogueProgressTracker)
    ///   수집된 캐릭터 이름 목록  (CharacterRecordPanelManager)
    ///   해금된 컨셉 카드 목록    (RewardSaveData)
    ///   해금된 시점 완결문 목록  (RewardSaveData)
    /// </summary>
    [Serializable]
    public class CampaignSaveData
    {
        /// <summary>스테이지 ID입니다.</summary>
        public string stageId;

        /// <summary>저장 시각입니다. (ISO 8601 형식)</summary>
        public string savedAt;

        // ── 대화 조각 ─────────────────────────────────────────────────────

        /// <summary>
        /// 수집된 FragmentId 목록입니다.
        /// 예: ["P01_01", "P01_02", "P03_05"]
        /// </summary>
        public List<string> collectedFragmentIds = new();

        // ── 대사 기록 ─────────────────────────────────────────────────────

        /// <summary>
        /// 이번 씬에서 재생된 ComboId 목록입니다.
        /// 씬 재진입 시 이어하기에 사용합니다.
        /// 예: ["COND_P01_01", "C026", "C001"]
        /// </summary>
        public List<string> playedComboIds = new();

        // ── 캐릭터 이름 ───────────────────────────────────────────────────

        /// <summary>수집된 캐릭터 이름 목록입니다.</summary>
        public List<CollectedNameEntry> collectedNames = new();

        // ── 보상 해금 ─────────────────────────────────────────────────────

        /// <summary>
        /// 해금된 컨셉 카드 캐릭터 ID 목록입니다.
        /// 예: [1, 3, 5]
        /// </summary>
        public List<int> unlockedConceptCards = new();

        /// <summary>
        /// 해금된 시점 완결문 캐릭터 ID 목록입니다.
        /// 예: [1, 2, 7]
        /// </summary>
        public List<int> unlockedEpilogues = new();

        // ── 유틸 ──────────────────────────────────────────────────────────

        /// <summary>진행 데이터가 존재하는지 여부입니다.</summary>
        public bool HasProgress =>
            collectedFragmentIds.Count > 0 ||
            playedComboIds.Count > 0 ||
            collectedNames.Count > 0 ||
            unlockedConceptCards.Count > 0 ||
            unlockedEpilogues.Count > 0;
    }

    /// <summary>수집된 캐릭터 이름 1개 항목입니다.</summary>
    [Serializable]
    public class CollectedNameEntry
    {
        public int characterId;
        public string name;
    }
}