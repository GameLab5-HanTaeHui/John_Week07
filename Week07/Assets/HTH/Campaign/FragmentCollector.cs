using System;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 대화 조각 수집 및 보상 해금을 관리합니다.
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   대사 재생 완료 후 DialogueTriggerManager로부터 조각 수집 요청을 받습니다.
    ///   수집된 조각 수를 추적하고, 조건을 충족하면 보상 해금 이벤트를 발생시킵니다.
    ///   프로파일 추리 완료 시 컨셉 카드와 시점 완결문 해금을 기록합니다.
    ///   모든 캐릭터의 시점 완결문이 해금되면 엔딩 이벤트를 발생시킵니다.
    ///
    /// ─── FragmentId 명명 규칙 ────────────────────────────────────────────
    ///   반드시 이 규칙을 따라야 캐릭터 ID 파싱이 정상 동작합니다.
    ///   형식: "{stageId}_char{characterId}_frag{index}"
    ///   예시: "Stage_1_Phase2_char1_frag0" → 캐릭터 #1의 첫 번째 조각
    ///         "Stage_1_Phase2_char3_frag2" → 캐릭터 #3의 세 번째 조각
    ///   CampaignDialogueSO의 GroupDialogueEntry.FragmentId 필드에 이 형식으로 입력합니다.
    ///
    /// ─── 보상 해금 흐름 ──────────────────────────────────────────────────
    ///   조각 수집 → 캐릭터별 카운트 증가
    ///   → _conceptCardMinFragments 이상 수집 시 OnConceptCardUnlockable 이벤트
    ///   → CharacterRecordBook, ProfileInquiryAllUI가 이를 수신해 추리 버튼 활성화
    ///
    ///   프로파일 추리 제출 → ProfileInquiryUI에서 판정
    ///   → 일부 정답 → UnlockConceptCard() 호출
    ///   → 전부 정답 → UnlockEpilogue() 호출
    ///   → 모든 캐릭터 완수 → OnAllCharactersCompleted 이벤트
    ///   → CampaignModeManager가 수신해 엔딩 씬 전환
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   Concept Card Min Fragments → 컨셉 카드 해금 가능 최소 조각 수 (기본값 3)
    ///   Reward Save Data           → RewardSaveData 에셋 (로비 보상 열람용)
    /// </summary>
    [DisallowMultipleComponent]
    public class FragmentCollector : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("보상 조건")]
        [Tooltip("이 수 이상의 대화 조각을 수집하면 프로파일 추리 버튼이 활성화됩니다.\n" +
                 "ProfileDataSO의 RequiredFragmentCount와 별개로 동작합니다.")]
        [SerializeField] private int _conceptCardMinFragments = 3;

        [Header("보상 저장")]
        [Tooltip("로비에서 보상을 열람할 때 사용하는 저장 데이터입니다.\n" +
                 "Project → Create → HTH → Campaign → RewardSaveData로 생성합니다.\n" +
                 "스테이지 씬과 로비 씬에서 같은 에셋을 공유합니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        // ── 런타임 데이터 ─────────────────────────────────────────────────

        // 수집된 모든 FragmentId 집합입니다.
        // 컨셉 카드("conceptcard_1")와 시점 완결문("epilogue_1") 해금 기록도 여기에 저장됩니다.
        private readonly HashSet<string> _collectedFragmentIds = new();

        // 캐릭터별 수집된 조각 수입니다.
        // key = characterId, value = 수집된 조각 수
        private readonly Dictionary<int, int> _fragmentCountPerChar = new();

        // 현재 초기화된 스테이지 ID입니다. Save/Load에 사용됩니다.
        private string _currentStageId;

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>
        /// 새 대화 조각이 수집될 때 발생합니다.
        /// string 파라미터: 수집된 FragmentId
        /// 구독: CharacterRecordBook (수집 현황 UI 갱신)
        /// </summary>
        public event Action<string> OnFragmentCollected;

        /// <summary>
        /// 특정 캐릭터의 조각이 _conceptCardMinFragments 이상 수집됐을 때 발생합니다.
        /// int 파라미터: characterId
        /// 구독: CharacterRecordBook (추리 버튼 활성화), ProfileInquiryAllUI (버튼 상태 갱신)
        /// </summary>
        public event Action<int> OnConceptCardUnlockable;

        /// <summary>
        /// 시점 완결문 해금 조건 충족 시 발생합니다.
        /// int 파라미터: characterId
        /// 현재 미사용 — 추후 연동 예정입니다.
        /// </summary>
        public event Action<int> OnEpilogueUnlockable;

        /// <summary>
        /// 모든 캐릭터(#1~#7)의 시점 완결문이 해금됐을 때 발생합니다.
        /// 구독: CampaignModeManager (엔딩 씬 전환 처리)
        /// </summary>
        public event Action OnAllCharactersCompleted;

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 스테이지 ID를 설정하고 저장된 수집 기록을 로드합니다.
        /// DialogueTriggerManager.OnPhase2Entered()에서 호출합니다.
        /// 이전 세션에서 수집한 조각 기록을 PlayerPrefs에서 복원합니다.
        /// </summary>
        public void Initialize(string stageId)
        {
            _currentStageId = stageId;
            Load(stageId);
            Debug.Log($"[FragmentCollector] 초기화 완료 — {stageId} ({_collectedFragmentIds.Count}개 수집됨)");
        }

        // ── 수집 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 대화 조각 수집을 시도합니다.
        /// DialogueTriggerManager.PlayGroupDialogue()의 onComplete에서 호출합니다.
        ///
        /// 이미 수집됐거나 fragmentId가 비어있으면 무시합니다.
        /// 수집 성공 시 PlayerPrefs에 저장하고 보상 조건을 체크합니다.
        /// </summary>
        public void TryCollectFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return;
            if (_collectedFragmentIds.Contains(fragmentId)) return;

            _collectedFragmentIds.Add(fragmentId);

            int charId = ParseCharacterIdFromFragment(fragmentId);
            if (charId >= 0)
            {
                _fragmentCountPerChar.TryGetValue(charId, out int count);
                _fragmentCountPerChar[charId] = count + 1;
            }

            Save();

            Debug.Log($"[FragmentCollector] 조각 수집 — {fragmentId} (캐릭터 {charId})");

            // 대화 조각 수집 로그
            GameLogger.Instance?.LogEvent("fragment_collected",
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "fragment_id",    fragmentId },
                    { "character_id",   charId },
                    { "total_count",    _collectedFragmentIds.Count },
                });

            OnFragmentCollected?.Invoke(fragmentId);

            if (charId >= 0)
                CheckRewards(charId);
        }

        /// <summary>해당 조각이 수집됐는지 확인합니다.</summary>
        public bool HasFragment(string fragmentId)
            => _collectedFragmentIds.Contains(fragmentId);

        /// <summary>특정 캐릭터의 수집된 조각 수를 반환합니다.</summary>
        public int GetFragmentCount(int characterId)
        {
            _fragmentCountPerChar.TryGetValue(characterId, out int count);
            return count;
        }

        /// <summary>수집된 전체 조각 수를 반환합니다.</summary>
        public int GetTotalFragmentCount() => _collectedFragmentIds.Count;

        // ── 보상 해금 API ─────────────────────────────────────────────────

        /// <summary>
        /// 컨셉 카드 해금을 기록합니다.
        /// ProfileInquiryUI.ShowResult()에서 일부 이상 정답일 때 호출합니다.
        /// </summary>
        public void UnlockConceptCard(int characterId)
        {
            string key = $"conceptcard_{characterId}";
            if (_collectedFragmentIds.Contains(key)) return; // 이미 해금됨

            _collectedFragmentIds.Add(key);
            Save();

            // 로비에서 보상 열람 시 사용할 데이터에도 저장합니다.
            _rewardSaveData?.SaveConceptCardUnlock(characterId);

            Debug.Log($"[FragmentCollector] 컨셉 카드 해금 — 캐릭터 {characterId}");
        }

        /// <summary>
        /// 시점 완결문 해금을 기록합니다.
        /// ProfileInquiryUI.ShowResult()에서 전부 정답일 때 호출합니다.
        /// 해금 후 모든 캐릭터 완수 여부를 체크합니다.
        /// </summary>
        public void UnlockEpilogue(int characterId)
        {
            string key = $"epilogue_{characterId}";
            if (_collectedFragmentIds.Contains(key)) return;

            _collectedFragmentIds.Add(key);
            Save();

            _rewardSaveData?.SaveEpilogueUnlock(characterId);

            Debug.Log($"[FragmentCollector] 시점 완결문 해금 — 캐릭터 {characterId}");

            // 모든 캐릭터의 시점 완결문이 해금됐는지 확인합니다.
            CheckAllCharactersCompleted();
        }

        /// <summary>컨셉 카드 해금 여부를 확인합니다.</summary>
        public bool IsConceptCardUnlocked(int characterId)
            => _collectedFragmentIds.Contains($"conceptcard_{characterId}");

        /// <summary>시점 완결문 해금 여부를 확인합니다.</summary>
        public bool IsEpilogueUnlocked(int characterId)
            => _collectedFragmentIds.Contains($"epilogue_{characterId}");

        // ── 저장/로드 ─────────────────────────────────────────────────────

        public void Save()
        {
            if (string.IsNullOrEmpty(_currentStageId)) return;
            Save(_currentStageId);
        }

        /// <summary>
        /// 수집 기록을 PlayerPrefs에 저장합니다.
        /// 모든 FragmentId를 '|'로 연결한 문자열로 저장합니다.
        /// 저장 키: "hth_frag_{stageId}"
        /// </summary>
        public void Save(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return;

            string data = string.Join("|", _collectedFragmentIds);
            string prefKey = $"hth_frag_{stageId}";
            PlayerPrefs.SetString(prefKey, data);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// PlayerPrefs에서 수집 기록을 로드합니다.
        /// Initialize()에서 호출됩니다.
        /// </summary>
        public void Load(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return;

            _collectedFragmentIds.Clear();
            _fragmentCountPerChar.Clear();

            string prefKey = $"hth_frag_{stageId}";
            string data = PlayerPrefs.GetString(prefKey, string.Empty);

            if (!string.IsNullOrEmpty(data))
            {
                foreach (var fragmentId in data.Split('|'))
                {
                    if (string.IsNullOrEmpty(fragmentId)) continue;

                    _collectedFragmentIds.Add(fragmentId);

                    // 캐릭터별 카운트도 복원합니다.
                    int charId = ParseCharacterIdFromFragment(fragmentId);
                    if (charId >= 0)
                    {
                        _fragmentCountPerChar.TryGetValue(charId, out int count);
                        _fragmentCountPerChar[charId] = count + 1;
                    }
                }
            }
        }

        public void Clear(string stageId)
        {
            _collectedFragmentIds.Clear();
            _fragmentCountPerChar.Clear();

            PlayerPrefs.DeleteKey($"hth_frag_{stageId}");
            PlayerPrefs.Save();

            Debug.Log($"[FragmentCollector] 수집 기록 초기화 — {stageId}");
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터별 보상 조건을 체크합니다.
        /// 조각이 _conceptCardMinFragments 이상이면 OnConceptCardUnlockable 이벤트를 발생시킵니다.
        /// </summary>
        private void CheckRewards(int characterId)
        {
            int count = GetFragmentCount(characterId);

            // RequiredFragmentCount와 동일하게 맞춰야 하므로
            // ProfileDataSO를 참조하는 대신 _conceptCardMinFragments를
            // Inspector에서 캐릭터별 RequiredFragmentCount와 동일하게 설정합니다.
            // 예: RequiredFragmentCount = 5 → _conceptCardMinFragments = 5
            if (count >= _conceptCardMinFragments)
            {
                OnConceptCardUnlockable?.Invoke(characterId);
            }
        }

        /// <summary>
        /// 캐릭터 #1~#7 전부의 시점 완결문이 해금됐는지 체크합니다.
        /// 전부 해금됐으면 OnAllCharactersCompleted 이벤트를 발생시킵니다.
        /// </summary>
        private void CheckAllCharactersCompleted()
        {
            for (int i = 1; i <= 7; i++)
            {
                if (!IsEpilogueUnlocked(i)) return;
            }

            Debug.Log("[FragmentCollector] 모든 캐릭터 기록 완수 — 엔딩 조건 달성");
            OnAllCharactersCompleted?.Invoke();
        }

        /// <summary>
        /// FragmentId에서 캐릭터 ID를 파싱합니다.
        /// 명명 규칙 "_char{id}_"을 기준으로 파싱합니다.
        /// 파싱 실패 시 -1을 반환합니다.
        ///
        /// 예: "Stage_1_Phase2_char3_frag0" → 3
        ///     "conceptcard_5"             → -1 (규칙 불일치)
        /// </summary>
        private int ParseCharacterIdFromFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return -1;

            const string marker = "_char";
            int startIdx = fragmentId.IndexOf(marker, StringComparison.Ordinal);
            if (startIdx < 0) return -1;

            startIdx += marker.Length;
            int endIdx = fragmentId.IndexOf('_', startIdx);
            if (endIdx < 0) endIdx = fragmentId.Length;

            string idStr = fragmentId.Substring(startIdx, endIdx - startIdx);
            return int.TryParse(idStr, out int id) ? id : -1;
        }
    }
}