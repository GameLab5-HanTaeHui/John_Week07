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
    ///   수집된 대화 조각 ID 목록    (FragmentCollector)
    ///   재생된 ComboId 목록         (DialogueProgressTracker)
    ///   수집된 캐릭터 이름 목록     (CharacterRecordPanelManager)
    /// </summary>
    [Serializable]
    public class CampaignSaveData
    {
        /// <summary>스테이지 ID입니다.</summary>
        public string stageId;

        /// <summary>저장 시각입니다. (ISO 8601 형식)</summary>
        public string savedAt;

        /// <summary>
        /// 수집된 FragmentId 목록입니다.
        /// 예: ["P01_01", "P01_02", "P03_05"]
        /// </summary>
        public List<string> collectedFragmentIds = new();

        /// <summary>
        /// 이번 씬에서 재생된 ComboId 목록입니다.
        /// 씬 재진입 시 이어하기에 사용합니다.
        /// 예: ["COND_P01_01", "C026", "C001"]
        /// </summary>
        public List<string> playedComboIds = new();

        /// <summary>
        /// 수집된 캐릭터 이름 목록입니다.
        /// </summary>
        public List<CollectedNameEntry> collectedNames = new();

        /// <summary>진행 데이터가 존재하는지 여부입니다.</summary>
        public bool HasProgress =>
            collectedFragmentIds.Count > 0 ||
            playedComboIds.Count > 0 ||
            collectedNames.Count > 0;
    }

    /// <summary>수집된 캐릭터 이름 1개 항목입니다.</summary>
    [Serializable]
    public class CollectedNameEntry
    {
        public int characterId;
        public string name;
    }
}