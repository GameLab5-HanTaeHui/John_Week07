using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터별 프로파일 데이터 ScriptableObject입니다.
    ///
    /// ─── 데이터 구조 ─────────────────────────────────────────────────────
    ///   각 캐릭터마다 4개의 프로파일 항목 존재
    ///   각 항목마다 복수의 선택지 + 정답 인덱스
    ///   대화 조각 일정 수 이상 수집 시 추리 가능
    ///
    /// ─── 보상 구조 ───────────────────────────────────────────────────────
    ///   일부 정답 → 컨셉 카드 해금
    ///   전부 정답 → 컨셉 카드 + 시점 완결문 해금
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   StageId          → 이 SO가 속한 스테이지 ID (Stage_1_Phase2)
    ///   CharacterProfiles → 캐릭터별 프로파일 목록
    ///
    /// ─── 생성 방법 ───────────────────────────────────────────────────────
    ///   Project 우클릭 → Create → HTH → Campaign → ProfileData
    /// </summary>
    [CreateAssetMenu(fileName = "ProfileDataSO",
                     menuName = "HTH/Campaign/ProfileData")]
    public class ProfileDataSO : ScriptableObject
    {
        [SerializeField] private string _stageId;

        [Header("캐릭터별 프로파일 목록")]
        [SerializeField] private List<CharacterProfileData> _characterProfiles = new();

        // ── 공개 프로퍼티 ─────────────────────────────────────────────────

        public string StageId => _stageId;
        public IReadOnlyList<CharacterProfileData> CharacterProfiles => _characterProfiles;

        // ── 검색 API ──────────────────────────────────────────────────────

        /// <summary>캐릭터 ID로 프로파일 데이터를 검색합니다.</summary>
        public CharacterProfileData FindProfile(int characterId)
        {
            foreach (var profile in _characterProfiles)
                if (profile != null && profile.CharacterId == characterId)
                    return profile;
            return null;
        }

        // ── Editor 검증 ───────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_stageId))
                Debug.LogWarning($"[ProfileDataSO] {name}: StageId가 비어있습니다.");

            for (int i = 0; i < _characterProfiles.Count; i++)
            {
                var profile = _characterProfiles[i];
                if (profile == null) continue;

                if (profile.ProfileItems == null || profile.ProfileItems.Count == 0)
                    Debug.LogWarning($"[ProfileDataSO] {name}: CharacterProfile[{i}]의 프로파일 항목이 없습니다.");

                if (profile.ProfileItems != null && profile.ProfileItems.Count != 4)
                    Debug.LogWarning($"[ProfileDataSO] {name}: CharacterProfile[{i}]의 프로파일 항목이 4개가 아닙니다. (현재 {profile.ProfileItems.Count}개)");

                if (profile.ProfileItems != null)
                {
                    for (int j = 0; j < profile.ProfileItems.Count; j++)
                    {
                        var item = profile.ProfileItems[j];
                        if (item == null) continue;

                        if (item.Choices == null || item.Choices.Count < 2)
                            Debug.LogWarning($"[ProfileDataSO] {name}: CharacterProfile[{i}].ProfileItems[{j}]의 선택지가 2개 미만입니다.");

                        if (item.Choices != null
                            && (item.CorrectChoiceIndex < 0
                                || item.CorrectChoiceIndex >= item.Choices.Count))
                            Debug.LogWarning($"[ProfileDataSO] {name}: CharacterProfile[{i}].ProfileItems[{j}]의 정답 인덱스가 범위를 벗어났습니다.");
                    }
                }
            }
        }
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 데이터 구조
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 캐릭터 1명의 전체 프로파일 데이터입니다.
    /// </summary>
    [System.Serializable]
    public class CharacterProfileData
    {
        [Tooltip("캐릭터 ID (#1~#7)")]
        public int CharacterId;

        [Tooltip("프로파일 추리 가능 최소 대화 조각 수")]
        public int RequiredFragmentCount = 3;

        [Header("프로파일 항목 (4개 고정)")]
        [Tooltip("각 인물마다 4개의 프로파일 항목")]
        public List<ProfileItem> ProfileItems = new();

        [Header("컨셉 카드 데이터")]
        public ConceptCardData ConceptCard;

        [Header("시점 완결문")]
        [TextArea(3, 8)]
        [Tooltip("프로파일 완전 정답 시 해금되는 캐릭터 시점의 독백")]
        public string EpilogueText;

        // ── 유틸리티 ─────────────────────────────────────────────────────

        /// <summary>
        /// 제출된 답안 배열이 전부 정답인지 확인합니다.
        /// answers[i] = i번째 ProfileItem에 대한 선택 인덱스
        /// </summary>
        public bool IsAllCorrect(int[] answers)
        {
            if (answers == null || answers.Length != ProfileItems.Count) return false;

            for (int i = 0; i < ProfileItems.Count; i++)
            {
                if (ProfileItems[i] == null) return false;
                if (answers[i] != ProfileItems[i].CorrectChoiceIndex) return false;
            }
            return true;
        }

        /// <summary>
        /// 제출된 답안 중 정답 수를 반환합니다.
        /// </summary>
        public int CountCorrect(int[] answers)
        {
            if (answers == null) return 0;

            int count = 0;
            for (int i = 0; i < ProfileItems.Count && i < answers.Length; i++)
            {
                if (ProfileItems[i] == null) continue;
                if (answers[i] == ProfileItems[i].CorrectChoiceIndex) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// 프로파일 항목 1개입니다.
    /// 질문 + 복수의 선택지 + 정답 인덱스로 구성됩니다.
    /// </summary>
    [System.Serializable]
    public class ProfileItem
    {
        [Tooltip("프로파일 질문\n예: 가장 두려워하는 것")]
        [TextArea(1, 2)]
        public string Question;

        [Tooltip("선택지 목록 (2개 이상)")]
        public List<string> Choices = new();

        [Tooltip("정답 선택지 인덱스 (0-based)")]
        public int CorrectChoiceIndex;

        [Tooltip("이 항목 추리 가능 조건 — 필요한 대화 조각 ID (없으면 조건 없음)")]
        public string RequiredFragmentId;
    }

    /// <summary>
    /// 인물 컨셉 카드 데이터입니다.
    /// 프로파일 일부 정답 시 해금됩니다.
    /// </summary>
    [System.Serializable]
    public class ConceptCardData
    {
        [Tooltip("캐치프레이즈")]
        [TextArea(1, 2)]
        public string Catchphrase;

        [Tooltip("외형적 특징")]
        [TextArea(2, 4)]
        public string Appearance;

        [Tooltip("서사적 배경")]
        [TextArea(2, 6)]
        public string NarrativeBackground;

        [Tooltip("성격 및 행동 원리")]
        [TextArea(2, 4)]
        public string Personality;

        [Tooltip("기믹 연관성")]
        [TextArea(2, 4)]
        public string GimmickRelevance;

        [Tooltip("컨셉 카드 일러스트")]
        public Sprite CardIllustration;
    }
}