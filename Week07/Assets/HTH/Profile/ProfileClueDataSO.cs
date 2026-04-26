using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 프로파일 단서 데이터 ScriptableObject입니다.
    ///
    /// ─── 구조 ────────────────────────────────────────────────────────────
    ///   각 캐릭터마다 5개의 ProfileClue
    ///   ProfileClue 1개 = CLUE(진실) + HINT(유도) 대사 쌍
    ///
    ///   캐릭터 7명 × 조각 5개 = 총 35개 ProfileClue
    ///   ProfileClueID 형식: P{CharId:00}_{ClueIndex:00}
    ///   예: P01_01 = 엔비의 첫 번째 조각
    ///
    /// ─── CLUE / HINT 의미 ────────────────────────────────────────────────
    ///   CLUE (진실 문장 조각)
    ///     조건 충족 시 출력되는 핵심 대사
    ///     캐릭터의 진실을 드러냄
    ///     예: "이유라도 알았다면 덜 오래 붙잡았을까."
    ///
    ///   HINT (거짓/유도 문장 조각)
    ///     조건 미충족 시 표시되는 유도 대사
    ///     플레이어가 조건을 만들도록 안내
    ///     예: "엔비가 혼자 남으면, 따뜻한 관계를 오래 곱씹을 것 같다."
    ///
    /// ─── 인물 기록장 표시 매핑 ───────────────────────────────────────────
    ///   진실 문장 조각 슬롯 [0~4] → CLUE 텍스트
    ///   거짓 문장 조각 슬롯 [0~4] → HINT 텍스트
    ///
    /// ─── 생성 방법 ───────────────────────────────────────────────────────
    ///   Project 우클릭 → Create → HTH → Campaign → ProfileClueData
    /// </summary>
    [CreateAssetMenu(fileName = "ProfileClueDataSO",
                     menuName = "HTH/Campaign/ProfileClueData")]
    public class ProfileClueDataSO : ScriptableObject
    {
        [SerializeField] private string _stageId;

        [Header("프로파일 단서 목록")]
        [Tooltip("각 캐릭터당 5개씩, 총 35개의 ProfileClue를 입력합니다.")]
        [SerializeField] private List<ProfileClueEntry> _clues = new();

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────

        /// <summary>스테이지 ID입니다. 예: "Stage_1_Phase2"</summary>
        public string StageId => _stageId;

        /// <summary>전체 ProfileClue 목록을 반환합니다.</summary>
        public IReadOnlyList<ProfileClueEntry> Clues => _clues;

        // ── 검색 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 특정 캐릭터의 ProfileClue 5개를 반환합니다.
        /// 조각 인덱스 순서대로 정렬됩니다.
        /// </summary>
        /// <param name="characterId">캐릭터 ID (1~7)</param>
        /// <returns>해당 캐릭터의 ProfileClue 목록</returns>
        public List<ProfileClueEntry> GetCluesByCharacter(int characterId)
        {
            var result = new List<ProfileClueEntry>();
            foreach (var clue in _clues)
            {
                if (clue == null) continue;
                if (clue.CharacterId == characterId)
                    result.Add(clue);
            }

            // ClueIndex 순서로 정렬
            result.Sort((a, b) => a.ClueIndex.CompareTo(b.ClueIndex));
            return result;
        }

        /// <summary>
        /// ProfileClueID로 조각을 검색합니다.
        /// </summary>
        /// <param name="profileClueId">예: "P01_01"</param>
        /// <returns>찾은 ProfileClueEntry 또는 null</returns>
        public ProfileClueEntry FindById(string profileClueId)
        {
            if (string.IsNullOrEmpty(profileClueId)) return null;

            foreach (var clue in _clues)
                if (clue != null && clue.ProfileClueId == profileClueId)
                    return clue;
            return null;
        }

        /// <summary>
        /// 캐릭터 ID와 조각 인덱스로 ProfileClue를 검색합니다.
        /// </summary>
        /// <param name="characterId">캐릭터 ID (1~7)</param>
        /// <param name="clueIndex">조각 인덱스 (1~5)</param>
        public ProfileClueEntry Find(int characterId, int clueIndex)
        {
            foreach (var clue in _clues)
            {
                if (clue == null) continue;
                if (clue.CharacterId == characterId && clue.ClueIndex == clueIndex)
                    return clue;
            }
            return null;
        }

        // ── Editor 검증 ───────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_stageId))
                Debug.LogWarning($"[ProfileClueDataSO] {name}: StageId가 비어있습니다.");

            // 중복 ProfileClueID 체크
            var idSet = new HashSet<string>();
            foreach (var clue in _clues)
            {
                if (clue == null) continue;
                if (string.IsNullOrEmpty(clue.ProfileClueId)) continue;

                if (!idSet.Add(clue.ProfileClueId))
                    Debug.LogWarning($"[ProfileClueDataSO] {name}: 중복 ProfileClueID — {clue.ProfileClueId}");
            }
        }
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 데이터 구조
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ProfileClue 1개의 데이터입니다.
    /// CLUE(진실) + HINT(유도) 대사 쌍을 가집니다.
    /// </summary>
    [System.Serializable]
    public class ProfileClueEntry
    {
        [Header("식별")]
        [Tooltip("ProfileClueID — P{CharId:00}_{ClueIndex:00} 형식\n" +
                 "예: P01_01 (엔비의 첫 번째 조각)")]
        public string ProfileClueId;

        [Tooltip("캐릭터 ID (1~7)")]
        public int CharacterId;

        [Tooltip("조각 인덱스 (1~5)")]
        public int ClueIndex;

        [Header("프로파일 정보")]
        [Tooltip("프로파일 키워드\n예: 유기 / 결핍, 스파이 / 임무")]
        public string ProfileKeyword;

        [Tooltip("이 조각이 가리키는 진실 문장")]
        [TextArea(2, 4)]
        public string ProfileTruth;

        [Header("CLUE — 진실 문장 조각")]
        [Tooltip("조건 충족 시 출력되는 핵심 대사입니다.\n" +
                 "인물 기록장의 진실 문장 조각 슬롯에 표시됩니다.")]
        [TextArea(2, 4)]
        public string ClueText;

        [Tooltip("CLUE를 말하는 캐릭터 ID (보통 본인)")]
        public int ClueSpeakerId;

        [Header("HINT — 거짓/유도 문장 조각")]
        [Tooltip("조건 미충족 시 표시되는 유도 대사입니다.\n" +
                 "인물 기록장의 거짓 문장 조각 슬롯에 표시됩니다.\n" +
                 "플레이어가 CLUE 조건을 만들도록 유도합니다.")]
        [TextArea(2, 4)]
        public string HintText;

        [Header("조건 (참고용)")]
        [Tooltip("이 조각의 획득 조건 설명입니다.\n" +
                 "현재는 텍스트 메모로만 사용합니다.\n" +
                 "추후 조건 시스템 구현 시 ConditionType과 연동됩니다.")]
        [TextArea(1, 3)]
        public string AcquireCondition;

        [Tooltip("조건 타입 — 추후 조건 시스템에서 사용")]
        public string ConditionType;

        [Tooltip("추천 조합 — 예: #1|#5, no_death")]
        public string RecommendedCombo;

        [Header("디자이너 메모")]
        [TextArea(1, 3)]
        public string DesignerNote;
    }
}