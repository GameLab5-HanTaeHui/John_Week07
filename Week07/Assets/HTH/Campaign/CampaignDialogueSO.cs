using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 2회차 다이얼로그 데이터 ScriptableObject입니다.
    ///
    /// ─── DialogueId 네이밍 규칙 ──────────────────────────────────────────
    ///   핵심  : CORE_P01_01
    ///   힌트  : HINT_P01_01
    ///   그 외 : SPECIAL_P01_01
    ///   일반  : NORMAL_C001
    ///
    /// ─── 우선순위 ────────────────────────────────────────────────────────
    ///   Core > Hint > Special > Normal
    ///
    /// ─── 상황 분류 (3개 필드 조합) ───────────────────────────────────────
    ///   SituationScope   : AllZone / InZone / OutZone
    ///   SituationAlive   : AllSurvived / SomeoneDied
    ///   SituationGimmick : None / Killer / Wanderer / Sacrifice / Avenger / LoverChain
    ///
    /// ─── 조합식 없는 대화 ────────────────────────────────────────────────
    ///   TimeOfDayDialogues : 루프 시간대별 대사 (아침/점심/저녁)
    ///   AlreadySeenDialogues : 이미 본 조합 대체 대사
    /// </summary>
    [CreateAssetMenu(fileName = "CampaignDialogueSO",
                     menuName = "HTH/Campaign/DialogueData")]
    public class CampaignDialogueSO : ScriptableObject
    {
        [SerializeField] private string _stageId;

        [Header("조합 대사 목록")]
        [SerializeField] private List<DialogueEntry> _dialogues = new();

        [Header("조합식 없는 대사 — 시간대별")]
        [Tooltip("루프 시간대(아침/점심/저녁)에 따라 출력되는 대사입니다.\n" +
                 "조합식과 무관하게 해당 시간대에 진입하면 출력됩니다.\n" +
                 "추후 변경될 수 있습니다.")]
        [SerializeField] private List<TimeOfDayDialogueEntry> _timeOfDayDialogues = new();

        [Header("조합식 없는 대사 — 이미 본 대화 대체")]
        [Tooltip("Normal 대사를 이미 출력한 조합에서 다시 같은 조합이 만들어졌을 때\n" +
                 "대체로 출력되는 공용 대사입니다.\n" +
                 "추후 변경될 수 있습니다.")]
        [SerializeField] private List<DialogueLine> _alreadySeenLines = new();

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────

        public string StageId => _stageId;
        public IReadOnlyList<DialogueEntry> Dialogues => _dialogues;
        public IReadOnlyList<TimeOfDayDialogueEntry> TimeOfDayDialogues => _timeOfDayDialogues;
        public IReadOnlyList<DialogueLine> AlreadySeenLines => _alreadySeenLines;

        // ── 검색 API ──────────────────────────────────────────────────────

        /// <summary>DialogueId로 단일 대사를 검색합니다.</summary>
        public DialogueEntry FindById(string dialogueId)
        {
            if (string.IsNullOrEmpty(dialogueId)) return null;
            foreach (var e in _dialogues)
                if (e != null && e.DialogueId == dialogueId) return e;
            return null;
        }

        /// <summary>RewardFragmentId로 핵심 대사를 검색합니다.</summary>
        public DialogueEntry FindByFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return null;
            foreach (var e in _dialogues)
                if (e != null && e.RewardFragmentId == fragmentId) return e;
            return null;
        }

        /// <summary>DialogueType으로 대사 목록을 검색합니다.</summary>
        public List<DialogueEntry> FindByType(DialogueType type)
        {
            var result = new List<DialogueEntry>();
            foreach (var e in _dialogues)
                if (e != null && e.Type == type) result.Add(e);
            return result;
        }

        /// <summary>시간대로 대사를 검색합니다.</summary>
        public TimeOfDayDialogueEntry FindByTimeOfDay(TimeOfDay timeOfDay)
        {
            foreach (var e in _timeOfDayDialogues)
                if (e != null && e.TimeOfDay == timeOfDay) return e;
            return null;
        }

        /// <summary>ParticipantsRaw 문자열을 파싱해 정수 리스트로 반환합니다.</summary>
        public static List<int> ParseParticipants(string raw)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(raw)) return result;
            foreach (var part in raw.Split(','))
                if (int.TryParse(part.Trim(), out int id))
                    result.Add(id);
            return result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_stageId))
                Debug.LogWarning($"[CampaignDialogueSO] {name}: StageId가 비어있습니다.");
        }
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 열거형
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>대사 우선순위 분류입니다.</summary>
    public enum DialogueType
    {
        /// <summary>핵심 대화 — RewardFragmentId 있음, 미수집 시 최우선 출력</summary>
        Core = 0,
        /// <summary>힌트 대화 — 핵심 조각 획득 조건을 암시하는 대사</summary>
        Hint = 1,
        /// <summary>그 외 대사 — Core, Hint로 분류되지 않는 특수 상황 대사</summary>
        Special = 2,
        /// <summary>일반 대화 — 단순 조합별 일상 대사. 한 번 출력 후 재출력 안 함</summary>
        Normal = 3,
    }

    /// <summary>상황 범위 — 어느 구역 기준으로 판단하는가입니다.</summary>
    public enum SituationScope
    {
        /// <summary>전체 구역 기준</summary>
        AllZone = 0,
        /// <summary>조사 구역(Zone2) 내 기준</summary>
        InZone = 1,
        /// <summary>조사 구역 외 기준 — Zone2는 무관, 다른 구역에서 발생</summary>
        OutZone = 2,
    }

    /// <summary>상황 생존 상태 — 사망 발생 여부입니다.</summary>
    public enum SituationAlive
    {
        /// <summary>전원 생존 — 해당 범위에서 사망 없음</summary>
        AllSurvived = 0,
        /// <summary>누군가 사망 — 해당 범위에서 사망 발생</summary>
        SomeoneDied = 1,
    }

    /// <summary>상황 역할 기믹 — 특수 역할 발동 여부입니다.</summary>
    public enum SituationGimmick
    {
        None = 0,
        Killer = 1,
        Wanderer = 2,
        Sacrifice = 3,
        Avenger = 4,
        LoverChain = 5,
    }

    /// <summary>루프 시간대입니다.</summary>
    public enum TimeOfDay
    {
        /// <summary>아침 — 루프 첫 번째 턴</summary>
        Morning = 0,
        /// <summary>점심 — 루프 두 번째 턴</summary>
        Lunch = 1,
        /// <summary>저녁 — 루프 세 번째 턴</summary>
        Evening = 2,
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 데이터 구조
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 대사 1개 단위입니다. 단독/조합 대사를 통합합니다.
    /// ParticipantsRaw = "1" → 단독 / "1,2,7" → 조합
    /// </summary>
    [System.Serializable]
    public class DialogueEntry
    {
        // ── 식별 ─────────────────────────────────────────────────────────

        [Header("식별")]
        [Tooltip("대사 고유 ID입니다.\n" +
                 "핵심  : CORE_P01_01\n" +
                 "힌트  : HINT_P01_01\n" +
                 "그 외 : SPECIAL_P01_01\n" +
                 "일반  : NORMAL_C001")]
        public string DialogueId;

        [Tooltip("참가 캐릭터 ID 목록입니다. 쉼표로 구분해 입력하세요.\n" +
                 "단독 : '1'       → 캐릭터 #1 혼자\n" +
                 "2인  : '1,2'     → 캐릭터 #1, #2\n" +
                 "3인  : '1,2,7'   → 캐릭터 #1, #2, #7\n" +
                 "4인  : '1,3,4,5' → 캐릭터 #1, #3, #4, #5")]
        public string ParticipantsRaw;

        // ── 분류 ─────────────────────────────────────────────────────────

        [Header("분류")]
        [Tooltip("대사 우선순위 분류입니다.\n" +
                 "Core    — 핵심 대화. RewardFragmentId 필수. 미수집 시 최우선 출력.\n" +
                 "Hint    — 힌트 대화. 핵심 조각 획득 조건을 암시하는 대사.\n" +
                 "Special — 그 외 대사. Core, Hint로 분류되지 않는 특수 상황 대사.\n" +
                 "Normal  — 일반 대화. 단순 조합별 일상 대사. 한 번 출력 후 재출력 안 함.")]
        public DialogueType Type = DialogueType.Normal;

        // ── 상황 ─────────────────────────────────────────────────────────

        [Header("상황")]
        [Tooltip("상황 범위입니다.\n" +
                 "AllZone — 전체 구역 기준.\n" +
                 "InZone  — 조사 구역(Zone2) 내 기준.\n" +
                 "OutZone — 조사 구역 외 기준. 다른 구역에서 사망이 발생했고 Zone2는 생존.")]
        public SituationScope Scope = SituationScope.InZone;

        [Tooltip("사망 발생 여부입니다.\n" +
                 "AllSurvived — 전원 생존. Scope 범위에서 사망 없음.\n" +
                 "SomeoneDied — 누군가 사망. Scope 범위에서 사망 발생.")]
        public SituationAlive Alive = SituationAlive.AllSurvived;

        [Tooltip("특수 역할 기믹 발동 여부입니다. SomeoneDied 상태에서만 의미가 있습니다.\n\n" +
                 "None\n" +
                 "  기믹 없음. 일반 생존/사망 상황.\n\n" +
                 "Killer (살인자 기믹)\n" +
                 "  토니(#5)가 조사 구역(Zone2)에서 다른 캐릭터를 살해했을 때.\n" +
                 "  SituationCharacters = 피해자 ID (비워두면 누구든 무관)\n\n" +
                 "Wanderer (배회자 기믹)\n" +
                 "  새턴(#7)이 이동 전 구역에서 사망을 발생시키고 Zone2에 진입했을 때.\n" +
                 "  SituationCharacters = 사망한 캐릭터 ID (비워두면 누구든 무관)\n\n" +
                 "Sacrifice (희생양 기믹)\n" +
                 "  프리드(#6)가 살인자&배회자 효과로 죽을 다른 캐릭터를 대신해 사망했을 때.\n" +
                 "  SituationCharacters = 원래 죽을 뻔했던 캐릭터 ID (비워두면 무관)\n\n" +
                 "Avenger (복수자 기믹)\n" +
                 "  메이(#2)가 같은 구역의 살인자(토니 #5)를 처치했을 때.\n" +
                 "  SituationCharacters = 처치된 살인자 ID\n\n" +
                 "LoverChain (연인 연쇄 기믹)\n" +
                 "  연인A가 사망하여 연인B도 같은 턴에 연쇄 사망했을 때.\n" +
                 "  주의: 연인B가 이미 사망한 상태에서 연인A를 죽이면 발동하지 않습니다.\n" +
                 "  SituationCharacters = 연인A ID, 연인B ID")]
        public SituationGimmick Gimmick = SituationGimmick.None;

        [Tooltip("상황 관련 캐릭터 ID 목록입니다. 쉼표로 구분해 입력하세요.\n" +
                 "Gimmick별 사용법은 위 Gimmick Tooltip을 참고하세요.\n" +
                 "비워두면 기믹 발동 여부만 체크합니다.")]
        public string SituationCharactersRaw;

        // ── 조건 ─────────────────────────────────────────────────────────

        [Header("조건")]
        [Tooltip("이 대사 해금 조건 ID입니다.\n" +
                 "지정한 조각을 수집해야 이 대사가 출력됩니다.\n" +
                 "OR 조건: 'P01_01 또는 P01_02' (둘 중 하나만 수집해도 됨)\n" +
                 "비워두면 조건 없음.")]
        public string UnlockConditionId;

        // ── 보상 ─────────────────────────────────────────────────────────

        [Header("보상")]
        [Tooltip("이 대사 수집 시 획득하는 핵심 대화 조각 ID입니다.\n" +
                 "형식: P01_01 (캐릭터 #1의 첫 번째 조각)\n" +
                 "Core 타입 대사에만 입력하세요. 일반 대사는 비워두세요.")]
        public string RewardFragmentId;

        // ── 대사 ─────────────────────────────────────────────────────────

        [Header("대사")]
        [Tooltip("대사 줄 목록입니다. 순서대로 재생됩니다.")]
        public List<DialogueLine> Lines = new();

        // ── 메모 ─────────────────────────────────────────────────────────

        [Header("메모 (개발자용)")]
        [Tooltip("이 대사의 내용 및 출력 조건을 간략히 적는 개발자 메모입니다.\n" +
                 "빌드에 포함되지 않습니다.")]
        [TextArea(2, 5)]
        public string DeveloperNote;

        // ── 파싱 헬퍼 ────────────────────────────────────────────────────

        /// <summary>ParticipantsRaw를 파싱해 정수 리스트로 반환합니다.</summary>
        public List<int> GetParticipantIds()
            => CampaignDialogueSO.ParseParticipants(ParticipantsRaw);

        /// <summary>SituationCharactersRaw를 파싱해 정수 리스트로 반환합니다.</summary>
        public List<int> GetSituationCharacterIds()
            => CampaignDialogueSO.ParseParticipants(SituationCharactersRaw);
    }

    /// <summary>
    /// 시간대별 대사 1개 단위입니다.
    /// 조합식과 무관하게 해당 시간대에 진입하면 출력됩니다.
    /// 추후 변경될 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class TimeOfDayDialogueEntry
    {
        [Tooltip("시간대입니다.\nMorning = 아침 / Lunch = 점심 / Evening = 저녁")]
        public TimeOfDay TimeOfDay;

        [Tooltip("대사 줄 목록입니다. 순서대로 재생됩니다.")]
        public List<DialogueLine> Lines = new();

        [Header("메모 (개발자용)")]
        [Tooltip("개발자 메모입니다. 빌드에 포함되지 않습니다.")]
        [TextArea(1, 3)]
        public string DeveloperNote;
    }

    /// <summary>대사 한 줄입니다.</summary>
    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("발화 캐릭터 ID입니다.\n예: #1 (캐릭터 1번 발화)")]
        public string TextId;

        [Tooltip("대사 내용입니다.")]
        [TextArea(2, 5)]
        public string Text;
    }
}