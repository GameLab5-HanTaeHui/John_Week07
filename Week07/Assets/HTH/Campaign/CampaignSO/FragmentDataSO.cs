using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 대화 조각 전체 데이터 ScriptableObject입니다.
    ///
    /// ─── 병합 이력 ───────────────────────────────────────────────────────
    ///   CampaignDialogueSO   → FragmentEntry.Lines (대사 텍스트)
    ///   ProfileClueDataSO    → FragmentEntry.조건 필드 + ClueText/HintText
    ///   FragmentHintDataSO   → FragmentEntry.HintDescription
    ///   3개 SO → FragmentDataSO 단일 SO로 통합
    ///
    /// ─── FragmentEntry 1개 구성 ─────────────────────────────────────────
    ///   식별     : ProfileClueId, CharacterId, ClueIndex
    ///   기록장   : ClueText (진실), HintText (거짓/유도)
    ///   힌트     : HintDescription (미획득 슬롯 힌트 — 인물기록장 표시)
    ///   획득조건 : Combo, PlaceName, DeadRequired, Prerequisites
    ///   특수규칙 : SimultaneousIds, IsForcedExitFragment
    ///   대사     : Lines (실제 출력 대사 목록 — 구 CampaignDialogueSO.Lines)
    ///
    /// ─── 획득 조건 (4가지 AND) ───────────────────────────────────────────
    ///   Combo        : 엔비(#1)와 같은 Zone에 생존한 캐릭터 ID 목록
    ///   PlaceName    : 특정 장소명 (훈련장/예배실/창고/여관), 빈값=무관
    ///   DeadRequired : 사망 상태여야 하는 캐릭터 ID 목록, 비어있으면 무관
    ///   Prerequisites: 사전 보유해야 하는 ProfileClueId 목록, 비어있으면 무관
    ///
    /// ─── 특수 규칙 ───────────────────────────────────────────────────────
    ///   SimultaneousIds      : 이 조각 획득 시 자동 동시 획득 조각 ID 목록
    ///   IsForcedExitFragment : true이면 강제퇴고 직전 특수 대화 후 퇴고 후 획득
    ///
    /// ─── 생성 방법 ───────────────────────────────────────────────────────
    ///   Project 우클릭 → Create → HTH → Campaign → FragmentData
    /// </summary>
    [CreateAssetMenu(fileName = "FragmentDataSO",
                     menuName = "HTH/Campaign/FragmentData")]
    public class FragmentDataSO : ScriptableObject
    {
        [SerializeField] private string _stageId;

        [Header("대화 조각 목록 (총 35개)")]
        [SerializeField] private List<FragmentEntry> _fragments = new();

        public string StageId => _stageId;
        public IReadOnlyList<FragmentEntry> Fragments => _fragments;

        // ── 검색 API ──────────────────────────────────────────────────────

        /// <summary>ProfileClueId로 조각을 검색합니다. 예: "P02_01"</summary>
        public FragmentEntry FindById(string profileClueId)
        {
            if (string.IsNullOrEmpty(profileClueId)) return null;
            foreach (var f in _fragments)
                if (f != null && f.ProfileClueId == profileClueId)
                    return f;
            return null;
        }

        /// <summary>캐릭터 ID로 조각 목록을 반환합니다. ClueIndex 순 정렬.</summary>
        public List<FragmentEntry> GetByCharacter(int characterId)
        {
            var result = new List<FragmentEntry>();
            foreach (var f in _fragments)
                if (f != null && f.CharacterId == characterId)
                    result.Add(f);
            result.Sort((a, b) => a.ClueIndex.CompareTo(b.ClueIndex));
            return result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_stageId))
                Debug.LogWarning($"[FragmentDataSO] {name}: StageId가 비어있습니다.");

            var idSet = new HashSet<string>();
            foreach (var f in _fragments)
            {
                if (f == null || string.IsNullOrEmpty(f.ProfileClueId)) continue;
                if (!idSet.Add(f.ProfileClueId))
                    Debug.LogWarning($"[FragmentDataSO] {name}: 중복 ID — {f.ProfileClueId}");
            }
        }
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 데이터 구조
    // ═══════════════════════════════════════════════════════════════════════

    [System.Serializable]
    public class FragmentEntry
    {
        // ── 식별 ─────────────────────────────────────────────────────────

        [Header("식별")]
        [Tooltip("P{CharId:00}_{ClueIndex:00} 형식. 예: P02_01")]
        public string ProfileClueId;

        [Tooltip("캐릭터 ID (1~7)")]
        public int CharacterId;

        [Tooltip("조각 인덱스 (1~5)")]
        public int ClueIndex;

        // ── 인물 기록장 ───────────────────────────────────────────────────

        [Header("인물 기록장")]
        [Tooltip("조각 획득 후 인물 기록장 진실 슬롯에 표시됩니다.")]
        [TextArea(2, 4)]
        public string ClueText;

        [Tooltip("조각 미획득 시 인물 기록장 거짓 슬롯에 표시됩니다. (글리치 처리)")]
        [TextArea(2, 4)]
        public string HintText;

        [Tooltip("인물 기록장 미획득 슬롯에 표시되는 힌트 설명입니다.\n" +
                 "예: 훈련장에서 용병단 신입과의 첫 인사")]
        [TextArea(1, 2)]
        public string HintDescription;

        // ── 획득 조건 ─────────────────────────────────────────────────────

        [Header("획득 조건 (AND — 모두 충족 시 획득)")]
        [Tooltip("엔비(#1)와 같은 Zone에 생존한 캐릭터 ID 목록.\n" +
                 "예: [1, 2] = 엔비+메이 같은 Zone")]
        public List<int> Combo = new();

        [Tooltip("조각 발생 장소명. 훈련장/예배실/창고/여관\n빈값이면 어느 Zone이든 무관.")]
        public string PlaceName;

        [Tooltip("사망 상태여야 하는 캐릭터 ID 목록.\n비어있으면 사망 조건 없음.")]
        public List<int> DeadRequired = new();

        [Tooltip("사전 보유해야 하는 ProfileClueId 목록.\n비어있으면 사전 획득 조건 없음.")]
        public List<string> Prerequisites = new();

        // ── 특수 규칙 ─────────────────────────────────────────────────────

        [Header("특수 규칙")]
        [Tooltip("이 조각 획득 시 자동으로 함께 획득되는 조각 ID 목록.\n" +
                 "예: P02_04 획득 → [\"P05_03\"] 자동 획득")]
        public List<string> SimultaneousIds = new();

        [Tooltip("true이면 강제퇴고 직전 특수 대화 → 퇴고 후 획득.\n" +
                 "해당 조각: P05_01, P05_04")]
        public bool IsForcedExitFragment;

        // ── 대사 ─────────────────────────────────────────────────────────

        [Header("대사 (실제 출력 순서대로)")]
        [Tooltip("이 조각 획득 시 출력될 대사 목록입니다.\n" +
                 "TextId = 화자 캐릭터 ID 문자열 (예: \"2\" = 메이)")]
        public List<FragmentDialogueLine> Lines = new();

        [Header("개발자 메모")]
        [TextArea(1, 3)]
        public string DesignerNote;
    }

    /// <summary>대화 조각 1줄 대사입니다.</summary>
    [System.Serializable]
    public class FragmentDialogueLine
    {
        [Tooltip("화자 캐릭터 ID 문자열.\n예: \"1\"=엔비, \"2\"=메이, \"3\"=루이스")]
        public string TextId;

        [Tooltip("대사 내용")]
        [TextArea(2, 5)]
        public string Text;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Zone 명칭 ↔ Zone ID 매핑
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 장소 명칭을 ZoneId(정수)로 변환하는 유틸리티입니다.
    /// Zone 번호 확정 후 아래 매핑을 업데이트하세요.
    /// </summary>
    public static class PlaceNameMapper
    {
        // ★ Zone 번호 확정 후 업데이트 필요
        private static readonly Dictionary<string, int> Map = new()
        {
            { "예비실",   0 },
            { "여관",     1 },
            { "훈련장",   2 },
            { "창고",     3 },
        };

        public static int ToZoneId(string placeName)
        {
            if (string.IsNullOrEmpty(placeName)) return -1;
            return Map.TryGetValue(placeName, out int id) ? id : -1;
        }

        public static string ToPlaceName(int zoneId)
        {
            foreach (var kv in Map)
                if (kv.Value == zoneId) return kv.Key;
            return "";
        }
    }
}