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
    ///   대화 조각 수집 목록       (FragmentCollector)
    ///   재생된 DialogueId 목록    (DialogueProgressTracker)
    ///   수집된 캐릭터 이름 목록   (CharacterRecordPanelManager)
    ///   해금된 컨셉 카드 목록     (RewardSaveData)
    ///   해금된 시점 완결문 목록   (RewardSaveData)
    ///
    /// ─── 변경 이력 ───────────────────────────────────────────────────────
    ///   playedComboIds → playedDialogueIds
    ///   ComboId / SoloId 이중 구조를 DialogueId 단일 기준으로 통합.
    ///   (DialogueProgressTracker 리팩토링에 따른 변경)
    /// </summary>
    [Serializable]
    public class CampaignSaveData
    {
        /// <summary>스테이지 ID입니다.</summary>
        public string stageId;

        /// <summary>저장 시각입니다. (ISO 8601 형식)</summary>
        public string savedAt;

        /// <summary>이 저장 파일을 생성한 게임 버전입니다.</summary>
        public string gameVersion;

        // ── 대화 조각 ─────────────────────────────────────────────────────

        /// <summary>
        /// 수집된 FragmentId 목록입니다.
        /// 예: ["P01_01", "P01_02", "P03_05"]
        /// </summary>
        public List<string> collectedFragmentIds = new();

        // ── 대사 기록 ─────────────────────────────────────────────────────

        /// <summary>
        /// 재생된 DialogueId 목록입니다.
        /// 씬 재진입 시 이어하기에 사용합니다.
        /// 예: ["CORE_P01_01", "HINT_P01_02", "NORMAL_C001"]
        /// </summary>
        public List<string> playedDialogueIds = new();

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

        // ── 튜토리얼 ──────────────────────────────────────────────────────

        /// <summary>튜토리얼 클리어 여부입니다.</summary>
        public bool isTutorialCleared = false;

        /// <summary>
        /// 튜토리얼 클리어 알림 팝업을 이미 표시했는지 여부입니다.
        /// true이면 로비 재진입 시 팝업을 다시 표시하지 않습니다.
        /// </summary>
        public bool isTutorialPopupShown = false;

        // ── 슬롯 색상 상태 ────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터별 슬롯 색상 상태 목록입니다.
        /// CharacterRecordPanel CyclicColorText 상태(0/1/2)를 저장합니다.
        /// 슬롯 키 형식: "true_0" ~ "true_4" / "false_0" ~ "false_4"
        /// </summary>
        public List<SlotColorStateEntry> slotColorStates = new();

        // ── 최종 대화 ─────────────────────────────────────────────────────

        /// <summary>캐릭터별 최종 대화 결과 목록입니다.</summary>
        public List<FinalTalkRecord> finalTalkRecords = new();

        // ── 유틸 ──────────────────────────────────────────────────────────

        /// <summary>진행 데이터가 존재하는지 여부입니다.</summary>
        public bool HasProgress =>
            collectedFragmentIds.Count > 0 ||
            playedDialogueIds.Count > 0 ||
            collectedNames.Count > 0 ||
            unlockedConceptCards.Count > 0 ||
            unlockedEpilogues.Count > 0 ||
            finalTalkRecords.Count > 0;
    }

    // ── 보조 클래스 (네임스페이스 레벨 — 외부에서 직접 접근 가능) ─────────

    /// <summary>수집된 캐릭터 이름 1개 항목입니다.</summary>
    [Serializable]
    public class CollectedNameEntry
    {
        public int characterId;
        public string name;
    }

    /// <summary>
    /// 캐릭터 슬롯 1개의 CyclicColorText 색상 상태입니다.
    /// CharacterRecordPanel에서 저장/복원에 사용합니다.
    /// </summary>
    [Serializable]
    public class SlotColorStateEntry
    {
        /// <summary>캐릭터 ID (1~7)</summary>
        public int characterId;

        /// <summary>슬롯 키 — "true_0"~"true_4" / "false_0"~"false_4"</summary>
        public string slotKey;

        /// <summary>CyclicColorText 상태 (0=기본 / 1=초록 / 2=빨강+취소선)</summary>
        public int state;
    }

    /// <summary>캐릭터 1명의 최종 대화 결과입니다.</summary>
    [Serializable]
    public class FinalTalkRecord
    {
        /// <summary>캐릭터 ID (2~7, 엔비 제외)</summary>
        public int characterId;

        /// <summary>최종 대화 완료 여부입니다.</summary>
        public bool completed;

        /// <summary>정답 선택 여부입니다.</summary>
        public bool success;

        /// <summary>선택한 선택지 인덱스 (0-based)입니다.</summary>
        public int selectedChoiceIndex;

        /// <summary>결과 대화 확인 여부입니다.</summary>
        public bool resultSeen;
    }
}