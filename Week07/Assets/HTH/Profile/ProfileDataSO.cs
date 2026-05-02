using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터별 프로파일 데이터 ScriptableObject입니다.
    ///
    /// ─── 구조 변경 이력 ──────────────────────────────────────────────────
    ///   ProfileItems(5단계 추리), ConceptCard, EpilogueLines 제거.
    ///   최종 대화(FinalTalk)로 완전 통합.
    ///
    /// ─── FinalTalk 구조 ──────────────────────────────────────────────────
    ///   도입 대사 → 질문 1개 + 선택지 4개 → 정답 인덱스 → 결과 대화 4개
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

                if (p.FinalTalk == null)
                {
                    Debug.LogWarning($"[ProfileDataSO] {name}: Profile[{i}] FinalTalk 미설정");
                    continue;
                }

                var ft = p.FinalTalk;

                if (string.IsNullOrEmpty(ft.Question))
                    Debug.LogWarning($"[ProfileDataSO] {name}: Profile[{i}] FinalTalk.Question 비어있음");

                if (ft.Choices == null || ft.Choices.Count != 4)
                    Debug.LogWarning($"[ProfileDataSO] {name}: Profile[{i}] FinalTalk.Choices가 4개가 아님 " +
                                     $"({ft.Choices?.Count ?? 0}개)");

                if (ft.Choices != null
                    && (ft.CorrectIndex < 0 || ft.CorrectIndex >= ft.Choices.Count))
                    Debug.LogWarning($"[ProfileDataSO] {name}: Profile[{i}] FinalTalk.CorrectIndex 범위 초과");
            }
        }
#endif
    }

    [System.Serializable]
    public class CharacterProfileData
    {
        [Tooltip("캐릭터 ID (1~7)")]
        public int CharacterId;

        [Header("표시 정보")]
        [Tooltip("캐릭터 이름  예: 메이")]
        public string CharacterFullName;

        [Tooltip("캐릭터 역할  예: (전위 돌격형)")]
        public string CharacterRole;

        [Tooltip("캐릭터 아이콘 스프라이트")]
        public Sprite CharacterIcon;

        [Header("최종 대화")]
        [Tooltip("최종 대화 데이터입니다.\n미구현 캐릭터는 비워두세요.")]
        public FinalTalkData FinalTalk;
    }

    /// <summary>
    /// 최종 대화 전체 데이터입니다.
    ///
    /// ─── 구성 ────────────────────────────────────────────────────────────
    ///   IntroLines    : 캐릭터 도입 대사 묶음
    ///   Question      : 엔비가 꺼내는 사건의 맥락 질문 (선택지 4개의 공통 전제)
    ///   Choices[4]    : 엔비의 선택지 4개 (선택지 텍스트)
    ///   CorrectIndex  : 정답 선택지 인덱스 (0-based)
    ///   ResultDialogues : 각 선택지 선택 후 결과 대화
    ///
    /// ─── 미구현 상태 처리 ────────────────────────────────────────────────
    ///   ResultDialogues가 비어있거나 해당 ChoiceIndex 항목이 없으면 공란 처리합니다.
    /// </summary>
    [System.Serializable]
    public class FinalTalkData
    {
        [Header("도입 대사")]
        [Tooltip("캐릭터 첫 대사 + 엔비의 도입 대사 순서로 입력합니다.")]
        public List<FinalTalkLine> IntroLines = new();

        [Header("질문")]
        [Tooltip("선택지 4개의 공통 전제가 되는 질문입니다.\n" +
                 "엔비가 '그날 일에 대해 들은 게 있어서요' 이후 제시합니다.")]
        [TextArea(1, 3)]
        public string Question;

        [Header("선택지 (4개 고정)")]
        [Tooltip("엔비의 선택지 4개입니다.\n인덱스 = 선택지 번호 (0-based)")]
        public List<string> Choices = new() { "", "", "", "" };

        [Tooltip("정답 선택지 인덱스 (0-based)")]
        public int CorrectIndex;

        [Header("결과 대화")]
        [Tooltip("각 선택지 선택 후 결과 대화입니다.\n" +
                 "ChoiceIndex = 선택지 번호 (0-based)\n" +
                 "미구현 시 빈 리스트로 두세요.")]
        public List<FinalTalkResultDialogue> ResultDialogues = new();
    }

    /// <summary>최종 대화 1줄 데이터입니다.</summary>
    [System.Serializable]
    public class FinalTalkLine
    {
        [Tooltip("화자 캐릭터 ID\n1=엔비, 2=메이, 3=루이스, 4=데우스, 5=토니, 6=프리드, 7=새턴")]
        public int SpeakerId;

        [Tooltip("대사 내용")]
        [TextArea(1, 4)]
        public string Text;
    }

    /// <summary>선택지 1개의 결과 대화 묶음입니다.</summary>
    [System.Serializable]
    public class FinalTalkResultDialogue
    {
        [Tooltip("선택지 인덱스 (0-based)")]
        public int ChoiceIndex;

        [Tooltip("결과 대화 라인 목록입니다.\n미구현이면 비워두세요.")]
        public List<FinalTalkLine> Lines = new();

        [Tooltip("성공 여부 (CorrectIndex와 일치하는 선택지면 true)")]
        public bool IsSuccess;
    }
}