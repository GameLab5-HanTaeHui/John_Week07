using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 2회차 다이얼로그 데이터 ScriptableObject입니다.
    ///
    /// ─── 데이터 구조 ─────────────────────────────────────────────────────
    ///   GroupDialogue: 조합별 대사 (1~7명 조합, 총 216개)
    ///     - 일반 조합 대사 (생존 조합, 2인/3인 대화, 사망 반응 등)
    ///     - 프로파일 핵심문장 (IsProfileClue=True, fragmentId=P01_01 형식)
    ///
    ///   SoloDialogue: 단독 대사 (기능 구현만, 사용 여부 미확정)
    ///
    /// ─── FragmentId 형식 ────────────────────────────────────────────────
    ///   P{CharId:00}_{ClueIndex:00} — 예: P01_01, P07_05
    ///   FragmentId가 없는 일반 대사는 빈 문자열
    ///
    /// ─── SituationType 목록 ─────────────────────────────────────────────
    ///   생존 조합 대사  / 2인 대화 / 3인 대화 / 전체 파티 대화
    ///   개인 독백 / 사망 반응 / 사망 반응/연인 연쇄
    ///   사망 반응/배회자 / 사망 반응/살인자 / 사망 반응/복수자
    ///   사망 반응/희생양 / 프로파일 핵심문장 / 프로파일 유도대사
    ///
    /// ─── 생성 방법 ───────────────────────────────────────────────────────
    ///   Project 우클릭 → Create → HTH → Campaign → DialogueData
    /// </summary>
    [CreateAssetMenu(fileName = "CampaignDialogueSO",
                     menuName = "HTH/Campaign/DialogueData")]
    public class CampaignDialogueSO : ScriptableObject
    {
        [SerializeField] private string _stageId;

        [Header("단독 대사 (사용 여부 미확정)")]
        [SerializeField] private List<SoloDialogueEntry> _soloDialogues = new();

        [Header("조합별 대사 (1~7명 조합, 총 216개)")]
        [SerializeField] private List<GroupDialogueEntry> _groupDialogues = new();

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────

        /// <summary>스테이지 ID입니다.</summary>
        public string StageId => _stageId;

        /// <summary>단독 대사 목록입니다.</summary>
        public IReadOnlyList<SoloDialogueEntry> SoloDialogues => _soloDialogues;

        /// <summary>조합별 대사 목록입니다.</summary>
        public IReadOnlyList<GroupDialogueEntry> GroupDialogues => _groupDialogues;

        // ── 검색 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 참가자 조합으로 조우 대사를 검색합니다.
        /// 정확히 일치하는 조합이 없으면 null을 반환합니다.
        /// </summary>
        public GroupDialogueEntry FindGroupDialogue(HashSet<int> characterIds)
        {
            if (characterIds == null || characterIds.Count == 0) return null;

            foreach (var entry in _groupDialogues)
            {
                if (entry == null || entry.ParticipantIds == null) continue;
                if (entry.ParticipantIds.Count != characterIds.Count) continue;

                bool allMatch = true;
                foreach (int id in entry.ParticipantIds)
                {
                    if (!characterIds.Contains(id)) { allMatch = false; break; }
                }

                if (allMatch) return entry;
            }
            return null;
        }

        /// <summary>
        /// ComboId로 조우 대사를 검색합니다.
        /// 예: "#1", "C001", "COND_P01_01"
        /// </summary>
        public GroupDialogueEntry FindGroupDialogueByComboId(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return null;

            foreach (var entry in _groupDialogues)
                if (entry != null && entry.ComboId == comboId)
                    return entry;
            return null;
        }

        /// <summary>
        /// SituationType으로 조우 대사 목록을 검색합니다.
        /// 예: "사망 반응", "프로파일 핵심문장"
        /// </summary>
        public List<GroupDialogueEntry> FindGroupDialoguesBySituation(string situationType)
        {
            var result = new List<GroupDialogueEntry>();
            if (string.IsNullOrEmpty(situationType)) return result;

            foreach (var entry in _groupDialogues)
                if (entry != null && entry.SituationType == situationType)
                    result.Add(entry);
            return result;
        }

        /// <summary>
        /// FragmentId(P01_01 형식)로 조우 대사를 검색합니다.
        /// ProfileClue와 연결된 대사 조회에 사용합니다.
        /// </summary>
        public GroupDialogueEntry FindGroupDialogueByFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return null;

            foreach (var entry in _groupDialogues)
                if (entry != null && entry.FragmentId == fragmentId)
                    return entry;
            return null;
        }

        /// <summary>
        /// FragmentId로 단독 대사를 검색합니다.
        /// </summary>
        public SoloDialogueEntry FindSoloDialogueByFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return null;

            foreach (var entry in _soloDialogues)
                if (entry != null && entry.FragmentId == fragmentId)
                    return entry;
            return null;
        }

        /// <summary>
        /// 캐릭터 ID로 단독 대사를 검색합니다.
        /// </summary>
        public SoloDialogueEntry FindSoloDialogue(int characterId)
        {
            foreach (var entry in _soloDialogues)
                if (entry != null && entry.CharacterId == characterId)
                    return entry;
            return null;
        }

        // ── Editor 검증 ───────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_stageId))
                Debug.LogWarning($"[CampaignDialogueSO] {name}: StageId가 비어있습니다.");
        }
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 데이터 구조
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 조합별 대사 1개 단위입니다.
    /// 1~7명 조합에 해당하는 대사를 포함합니다.
    /// </summary>
    [System.Serializable]
    public class GroupDialogueEntry
    {
        [Header("식별")]
        [Tooltip("조합 ID입니다.\n" +
                 "단독: #1~#7\n" +
                 "일반 조합: C001~C215\n" +
                 "프로파일 조건: COND_P01_01~COND_P07_05")]
        public string ComboId;

        [Tooltip("조합 키입니다. 예: '#1|#2|#7', '#1|ANY'")]
        public string ComboKey;

        [Tooltip("참가 캐릭터 ID 목록 (파싱된 정수 목록)")]
        public List<int> ParticipantIds = new();

        [Header("상황")]
        [Tooltip("대사 상황 타입입니다.\n" +
                 "생존 조합 대사 / 2인 대화 / 3인 대화 / 전체 파티 대화\n" +
                 "개인 독백 / 사망 반응 / 프로파일 핵심문장 / 프로파일 유도대사 등")]
        public string SituationType;

        [Tooltip("트리거 조건입니다. 예: 'after_death', 'always'")]
        public string Trigger;

        [Tooltip("공개 범위입니다. 예: 'all', 'zone_only'")]
        public string Visibility;

        [Header("ProfileClue 연결")]
        [Tooltip("이 대사 수집 시 해금되는 ProfileClue ID입니다.\n" +
                 "P01_01 형식. 일반 대사는 빈 문자열.")]
        public string FragmentId;

        [Tooltip("사망 트리거 캐릭터 ID 목록입니다.\n" +
                 "SituationType이 '사망 반응' 계열일 때 사용합니다.")]
        public List<int> TriggerDeadIds = new();

        [Header("조건")]
        [Tooltip("해금 조건 ID입니다.")]
        public string UnlockConditionId;

        [Tooltip("힌트 조건 ID입니다.")]
        public string HintOfConditionId;

        [Header("대사")]
        [Tooltip("대사 줄 목록 (순서대로 재생)")]
        public List<DialogueLine> Lines = new();

        /// <summary>출력 조건 (하위 호환)입니다.</summary>
        public DialogueConditionData Condition;
    }

    /// <summary>
    /// 단독 대사 1개 단위입니다.
    /// </summary>
    [System.Serializable]
    public class SoloDialogueEntry
    {
        [Tooltip("대사를 출력할 캐릭터 ID")]
        public int CharacterId;

        [Tooltip("대사 줄 목록")]
        public List<DialogueLine> Lines = new();

        [Tooltip("해금되는 FragmentId (없으면 빈 문자열)")]
        public string FragmentId;

        [Tooltip("출력 조건")]
        public DialogueConditionData Condition;
    }

    /// <summary>
    /// 대사 한 줄입니다.
    /// </summary>
    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("화자 캐릭터 ID")]
        public int SpeakerId;

        [Tooltip("대사 내용")]
        [TextArea(2, 5)]
        public string Text;

        [Tooltip("이 대사 출력 시 공개할 캐릭터 ID. -1이면 공개 없음.")]
        public int RevealCharacterId = -1;

        [Tooltip("공개할 캐릭터 이름")]
        public string RevealCharacterName;

        [Tooltip("이 라인이 ProfileClue 핵심문장인지 여부")]
        public bool IsProfileClue;

        [Tooltip("연결된 ProfileClue ID (P01_01 형식)")]
        public string ProfileClueId;

        [Tooltip("프로파일 카테고리")]
        public string ProfileCategory;
    }

    /// <summary>
    /// 다이얼로그 출력 조건 데이터입니다.
    /// </summary>
    [System.Serializable]
    public class DialogueConditionData
    {
        [Tooltip("이 FragmentId 수집 후에만 출력 (비어있으면 조건 없음)")]
        public string RequiredFragmentId;

        [Tooltip("최소 루프 횟수 (0이면 항상)")]
        public int MinLoopCount;

        /// <summary>조건이 하나도 설정되지 않았는지 여부입니다.</summary>
        public bool IsEmpty =>
            string.IsNullOrEmpty(RequiredFragmentId) && MinLoopCount <= 0;
    }
}