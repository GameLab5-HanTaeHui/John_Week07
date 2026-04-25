using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 2회차 다이얼로그 데이터 ScriptableObject입니다.
    ///
    /// ─── 데이터 구조 ─────────────────────────────────────────────────────
    ///   단독 대사   : 캐릭터 1명 (구현해두되 사용 여부 미확정)
    ///   조우 대사   : 2~7명 조합 (참여 캐릭터 ID 집합으로 식별)
    ///   대화 조각   : 조우/단독 대사 수집 시 해금되는 프래그먼트 연결
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   StageId        → 이 SO가 속한 스테이지 ID (예: Stage_1_Phase2)
    ///   SoloDialogues  → 단독 대사 목록 (사용 여부 미확정)
    ///   GroupDialogues → 조우 대사 목록 (2~7명 조합)
    ///
    /// ─── 조합 키 규칙 ────────────────────────────────────────────────────
    ///   ParticipantIds를 오름차순 정렬 후 쉼표로 연결
    ///   예: {1, 3, 5} → "1,3,5"
    ///   DialogueTriggerManager에서 구역 내 캐릭터 조합과 매칭합니다.
    ///
    /// ─── 생성 방법 ───────────────────────────────────────────────────────
    ///   Project 우클릭 → Create → HTH → Campaign → DialogueData
    /// </summary>
    [CreateAssetMenu(fileName = "CampaignDialogueSO",
                     menuName = "HTH/Campaign/DialogueData")]
    public class CampaignDialogueSO : ScriptableObject
    {
        [SerializeField] private string _stageId;

        [Header("단독 대사 (사용 여부 미확정 — 기능만 구현)")]
        [SerializeField] private List<SoloDialogueEntry> _soloDialogues = new();

        [Header("조우 대사 (2~7명 조합)")]
        [SerializeField] private List<GroupDialogueEntry> _groupDialogues = new();

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────

        public string StageId => _stageId;

        /// <summary>단독 대사 목록입니다. (읽기 전용)</summary>
        public IReadOnlyList<SoloDialogueEntry> SoloDialogues => _soloDialogues;

        /// <summary>조우 대사 목록입니다. (읽기 전용)</summary>
        public IReadOnlyList<GroupDialogueEntry> GroupDialogues => _groupDialogues;

        // ── 검색 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터 조합으로 조우 대사를 검색합니다.
        /// 정확히 일치하는 조합이 없으면 null을 반환합니다.
        /// </summary>
        /// <param name="characterIds">구역 내 캐릭터 ID 집합</param>
        public GroupDialogueEntry FindGroupDialogue(HashSet<int> characterIds)
        {
            if (characterIds == null || characterIds.Count < 2) return null;

            foreach (var entry in _groupDialogues)
            {
                if (entry == null || entry.ParticipantIds == null) continue;
                if (entry.ParticipantIds.Count != characterIds.Count) continue;

                bool allMatch = true;
                foreach (int id in entry.ParticipantIds)
                {
                    if (!characterIds.Contains(id))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch) return entry;
            }

            return null;
        }

        /// <summary>
        /// 캐릭터 ID로 단독 대사를 검색합니다.
        /// 없으면 null을 반환합니다.
        /// </summary>
        /// <param name="characterId">캐릭터 ID</param>
        public SoloDialogueEntry FindSoloDialogue(int characterId)
        {
            foreach (var entry in _soloDialogues)
            {
                if (entry != null && entry.CharacterId == characterId)
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// 특정 대화 조각 ID를 포함하는 조우 대사를 검색합니다.
        /// </summary>
        public GroupDialogueEntry FindGroupDialogueByFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return null;

            foreach (var entry in _groupDialogues)
            {
                if (entry != null && entry.FragmentId == fragmentId)
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// 특정 대화 조각 ID를 포함하는 단독 대사를 검색합니다.
        /// </summary>
        public SoloDialogueEntry FindSoloDialogueByFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return null;

            foreach (var entry in _soloDialogues)
            {
                if (entry != null && entry.FragmentId == fragmentId)
                    return entry;
            }
            return null;
        }

        // ── Editor 검증 ───────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_stageId))
                Debug.LogWarning($"[CampaignDialogueSO] {name}: StageId가 비어있습니다.");

            for (int i = 0; i < _groupDialogues.Count; i++)
            {
                var entry = _groupDialogues[i];
                if (entry == null) continue;

                if (entry.ParticipantIds == null || entry.ParticipantIds.Count < 2)
                    Debug.LogWarning($"[CampaignDialogueSO] {name}: GroupDialogue[{i}]의 참여 캐릭터가 2명 미만입니다.");

                if (entry.ParticipantIds != null && entry.ParticipantIds.Count > 7)
                    Debug.LogWarning($"[CampaignDialogueSO] {name}: GroupDialogue[{i}]의 참여 캐릭터가 7명을 초과합니다.");

                if (entry.Lines == null || entry.Lines.Count == 0)
                    Debug.LogWarning($"[CampaignDialogueSO] {name}: GroupDialogue[{i}]의 대사가 없습니다.");
            }
        }
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 데이터 구조
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 단독 대사 1개 단위입니다.
    /// 캐릭터 1명이 특정 상황에서 출력하는 대사입니다.
    /// 사용 여부 미확정 — 기능만 구현합니다.
    /// </summary>
    [System.Serializable]
    public class SoloDialogueEntry
    {
        [Tooltip("대사를 출력할 캐릭터 ID")]
        public int CharacterId;

        [Tooltip("대사 줄 목록 (순서대로 재생)")]
        public List<DialogueLine> Lines = new();

        [Tooltip("이 대사 수집 시 해금되는 대화 조각 ID (없으면 빈 문자열)")]
        public string FragmentId;

        [Tooltip("출력 조건 (없으면 항상 출력 가능)")]
        public DialogueConditionData Condition;
    }

    /// <summary>
    /// 조우 대사 1개 단위입니다.
    /// 2~7명의 캐릭터가 같은 구역에 모였을 때 출력되는 대사입니다.
    /// </summary>
    [System.Serializable]
    public class GroupDialogueEntry
    {
        [Tooltip("이 대사에 참여하는 캐릭터 ID 목록 (2~7명)")]
        public List<int> ParticipantIds = new();

        [Tooltip("대사 줄 목록 (순서대로 재생)")]
        public List<DialogueLine> Lines = new();

        [Tooltip("이 대사 수집 시 해금되는 대화 조각 ID (없으면 빈 문자열)")]
        public string FragmentId;

        [Tooltip("출력 조건 (없으면 항상 출력 가능)")]
        public DialogueConditionData Condition;
    }

    /// <summary>
    /// 대사 한 줄입니다.
    /// 화자 ID와 텍스트로 구성됩니다.
    /// </summary>
    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("대사를 출력할 캐릭터 ID (화자)")]
        public int SpeakerId;

        [Tooltip("대사 내용")]
        [TextArea(2, 5)]
        public string Text;

        [Header("이름 공개 (선택)")]
        [Tooltip("이 대사 출력 시 공개할 캐릭터 ID. -1이면 공개 없음.")]
        public int RevealCharacterId = -1;

        [Tooltip("공개할 캐릭터 이름. RevealCharacterId >= 0일 때 유효.")]
        public string RevealCharacterName;
    }

    /// <summary>
    /// 다이얼로그 출력 조건 데이터입니다.
    /// DialogueConditionEvaluator에서 사용합니다.
    /// </summary>
    [System.Serializable]
    public class DialogueConditionData
    {
        [Tooltip("이 대화 조각이 수집된 이후에만 출력 (비어있으면 조건 없음)")]
        public string RequiredFragmentId;

        [Tooltip("최소 루프 횟수 조건 (0이면 항상)")]
        public int MinLoopCount;

        /// <summary>조건이 하나도 설정되지 않았는지 여부입니다.</summary>
        public bool IsEmpty =>
            string.IsNullOrEmpty(RequiredFragmentId) && MinLoopCount <= 0;
    }
}