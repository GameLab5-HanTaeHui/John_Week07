using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터별 프로파일 데이터 ScriptableObject입니다.
    ///
    /// ─── 프로파일 구조 ───────────────────────────────────────────────────
    ///   Step 5개 × 선택지 3개
    ///   Step 1: 과거 핵심 사건
    ///   Step 2: 현재 정체/행동 이유
    ///   Step 3: 관계 인식
    ///   Step 4: 숨겨진 왜곡
    ///   Step 5: 최종 기전
    ///
    /// ─── 선택지 구성 ─────────────────────────────────────────────────────
    ///   [0] 1순위 정답   (correctChoiceIndex = 0)
    ///   [1] 2순위 후보
    ///   [2] 3순위 오답
    ///
    /// ─── 보상 구조 ───────────────────────────────────────────────────────
    ///   정답 1개 이상 → 컨셉 카드 해금
    ///   5개 전부 정답  → 컨셉 카드 + 시점 완결문 해금
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

        public string StageId => _stageId;
        public IReadOnlyList<CharacterProfileData> CharacterProfiles => _characterProfiles;

        /// <summary>캐릭터 ID로 프로파일 데이터를 검색합니다.</summary>
        public CharacterProfileData FindProfile(int characterId)
        {
            foreach (var profile in _characterProfiles)
                if (profile != null && profile.CharacterId == characterId)
                    return profile;
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_stageId))
                Debug.LogWarning($"[ProfileDataSO] {name}: StageId가 비어있습니다.");

            for (int i = 0; i < _characterProfiles.Count; i++)
            {
                var p = _characterProfiles[i];
                if (p == null) continue;

                if (p.ProfileItems == null || p.ProfileItems.Count == 0)
                    Debug.LogWarning($"[ProfileDataSO] {name}: Profile[{i}] 항목 없음");

                if (p.ProfileItems != null && p.ProfileItems.Count != 5)
                    Debug.LogWarning($"[ProfileDataSO] {name}: Profile[{i}] 항목이 5개가 아님 " +
                                     $"({p.ProfileItems.Count}개)");

                if (p.ProfileItems != null)
                {
                    for (int j = 0; j < p.ProfileItems.Count; j++)
                    {
                        var item = p.ProfileItems[j];
                        if (item == null) continue;

                        if (item.Choices == null || item.Choices.Count < 2)
                            Debug.LogWarning($"[ProfileDataSO] {name}: " +
                                             $"Profile[{i}].Item[{j}] 선택지 2개 미만");

                        if (item.Choices != null
                            && (item.CorrectChoiceIndex < 0
                                || item.CorrectChoiceIndex >= item.Choices.Count))
                            Debug.LogWarning($"[ProfileDataSO] {name}: " +
                                             $"Profile[{i}].Item[{j}] 정답 인덱스 범위 초과");
                    }
                }
            }
        }
#endif
    }

    [System.Serializable]
    public class CharacterProfileData
    {
        [Tooltip("캐릭터 ID (1~7)")]
        public int CharacterId;

        [Tooltip("프로파일 추리 가능 최소 대화 조각 수")]
        public int RequiredFragmentCount = 5;

        [Header("표시 정보")]
        [Tooltip("캐릭터 이름  예: 엔비")]
        public string CharacterFullName;

        [Tooltip("캐릭터 역할  예: (주인공)")]
        public string CharacterRole;

        [Tooltip("캐릭터 아이콘 스프라이트")]
        public Sprite CharacterIcon;

        [Header("프로파일 항목 (5개 고정)")]
        public List<ProfileItem> ProfileItems = new();

        [Header("컨셉 카드")]
        public ConceptCardData ConceptCard;

        [Header("시점 완결문 (문단 단위 배열)")]
        [Tooltip("ProfileDataImporter로 JSON에서 자동 임포트됩니다.")]
        public List<string> EpilogueLines = new();

        /// <summary>EpilogueLines를 빈 줄로 연결한 전체 텍스트입니다. (도감 표시용)</summary>
        public string EpilogueText => string.Join("\n\n", EpilogueLines);

        /// <summary>제출된 답안 배열이 전부 정답인지 확인합니다.</summary>
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

        /// <summary>제출된 답안 중 정답 수를 반환합니다.</summary>
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
    /// 프로파일 항목 1개 (질문 + 3개 선택지 + 정답 인덱스).
    ///
    /// 선택지 배치 규칙
    ///   [0] 1순위 정답   → correctChoiceIndex = 0
    ///   [1] 2순위 후보
    ///   [2] 3순위 완전 오답
    /// </summary>
    [System.Serializable]
    public class ProfileItem
    {
        [Tooltip("프로파일 질문\n" +
                 "Step1: 과거 핵심 사건\n" +
                 "Step2: 현재 정체/행동 이유\n" +
                 "Step3: 관계 인식\n" +
                 "Step4: 숨겨진 왜곡\n" +
                 "Step5: 최종 기전")]
        [TextArea(1, 2)]
        public string Question;

        [Tooltip("선택지 3개\n[0] 1순위 정답\n[1] 2순위 후보\n[2] 3순위 완전 오답")]
        public List<string> Choices = new();

        [Tooltip("정답 선택지 인덱스 (기본 0 = 첫 번째가 정답)")]
        public int CorrectChoiceIndex;

        [Tooltip("추리 가능 조건 FragmentId (없으면 조건 없음)")]
        public string RequiredFragmentId;
    }

    [System.Serializable]
    public class ConceptCardData
    {
        [Tooltip("캐치프레이즈")]
        [TextArea(1, 3)]
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