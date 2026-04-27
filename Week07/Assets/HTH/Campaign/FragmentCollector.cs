using System;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 대화 조각 수집 및 보상 해금을 관리합니다.
    ///
    /// ─── FragmentId 명명 규칙 ────────────────────────────────────────────
    ///   형식: "P{characterId:00}_{clueIndex:00}"
    ///   예시: "P01_01" → 캐릭터 #1의 첫 번째 조각
    ///         "P07_05" → 캐릭터 #7의 다섯 번째 조각
    ///
    /// ─── 조각 수 기준 ────────────────────────────────────────────────────
    ///   GetFragmentCount(characterId)      → 해당 캐릭터의 수집 조각 수
    ///   GetTotalFragmentCount(characterId) → 동일 (진실/거짓 구분 없이 합산)
    ///   ProfileInquiryUI에서 10개 이상일 때 추리 가능
    ///
    /// ─── 보상 해금 흐름 ──────────────────────────────────────────────────
    ///   TryCollectFragment() → OnFragmentCollected 이벤트
    ///   UnlockConceptCard()  → RewardSaveData 저장
    ///   UnlockEpilogue()     → RewardSaveData 저장 → 전체 완수 체크
    ///   전체 완수             → OnAllCharactersCompleted 이벤트
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Concept Card Min Fragments → 프로파일 추리 활성화 최소 조각 수 (기본 10)
    ///   Reward Save Data           → RewardSaveData 에셋
    /// </summary>
    [DisallowMultipleComponent]
    public class FragmentCollector : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("보상 조건")]
        [Tooltip("이 수 이상 수집 시 프로파일 추리 버튼이 활성화됩니다.\n" +
                 "진실 + 거짓 조각 합산 기준입니다. (기본값 10)")]
        [SerializeField] private int _conceptCardMinFragments = 10;

        [Header("보상 저장")]
        [Tooltip("로비에서 보상 열람 시 사용하는 저장 데이터 에셋입니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        // ── 런타임 데이터 ─────────────────────────────────────────────────

        /// <summary>수집된 모든 FragmentId 집합입니다.</summary>
        private readonly HashSet<string> _collectedFragmentIds = new();

        /// <summary>캐릭터별 수집된 조각 수입니다. key=characterId, value=count</summary>
        private readonly Dictionary<int, int> _fragmentCountPerChar = new();

        /// <summary>현재 스테이지 ID입니다.</summary>
        private string _currentStageId;

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>새 대화 조각이 수집될 때 발생합니다. (string = FragmentId)</summary>
        public event Action<string> OnFragmentCollected;

        /// <summary>특정 캐릭터의 조각이 기준 수 이상 수집됐을 때 발생합니다. (int = characterId)</summary>
        public event Action<int> OnConceptCardUnlockable;

        /// <summary>모든 캐릭터(#1~#7)의 시점 완결문이 해금됐을 때 발생합니다.</summary>
        public event Action OnAllCharactersCompleted;

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 스테이지 ID를 설정하고 JSON 저장에서 수집 기록을 로드합니다.
        /// DialogueTriggerManager.OnPhase2Entered()에서 호출합니다.
        /// </summary>
        public void Initialize(string stageId)
        {
            _currentStageId = stageId;
            _collectedFragmentIds.Clear();
            _fragmentCountPerChar.Clear();

            var saveData = CampaignSaveManager.Instance?.Load(stageId);
            if (saveData != null)
            {
                foreach (var fragmentId in saveData.collectedFragmentIds)
                {
                    if (string.IsNullOrEmpty(fragmentId)) continue;
                    _collectedFragmentIds.Add(fragmentId);

                    int charId = ParseCharacterIdFromFragment(fragmentId);
                    if (charId >= 0)
                    {
                        _fragmentCountPerChar.TryGetValue(charId, out int count);
                        _fragmentCountPerChar[charId] = count + 1;
                    }
                }

                // 보상 해금 기록도 복원
                foreach (int id in saveData.unlockedConceptCards)
                    _collectedFragmentIds.Add($"conceptcard_{id}");

                foreach (int id in saveData.unlockedEpilogues)
                    _collectedFragmentIds.Add($"epilogue_{id}");
            }

            Debug.Log($"[FragmentCollector] 초기화 완료 — {stageId} " +
                      $"({_collectedFragmentIds.Count}개 수집됨)");
        }

        // ── 수집 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 대화 조각 수집을 시도합니다.
        /// 이미 수집됐거나 fragmentId가 비어있으면 무시합니다.
        /// DialogueTriggerManager에서 대사 재생 완료 후 호출합니다.
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

            Debug.Log($"[FragmentCollector] 조각 수집 — {fragmentId} (캐릭터 #{charId})");

            GameLogger.Instance?.LogEvent("fragment_collected",
                new Dictionary<string, object>
                {
                    { "fragment_id",  fragmentId },
                    { "character_id", charId },
                    { "total_count",  _collectedFragmentIds.Count },
                });

            OnFragmentCollected?.Invoke(fragmentId);

            if (charId >= 0)
                CheckInquiryUnlock(charId);
        }

        /// <summary>해당 조각이 수집됐는지 확인합니다.</summary>
        public bool HasFragment(string fragmentId)
            => _collectedFragmentIds.Contains(fragmentId);

        /// <summary>
        /// 특정 캐릭터의 수집된 조각 수를 반환합니다.
        /// P 형식의 FragmentId만 카운트합니다.
        /// </summary>
        public int GetFragmentCount(int characterId)
        {
            _fragmentCountPerChar.TryGetValue(characterId, out int count);
            return count;
        }

        /// <summary>
        /// 특정 캐릭터의 총 조각 수를 반환합니다.
        /// 진실/거짓 구분 없이 합산합니다.
        /// ProfileInquiryUI에서 10개 기준 추리 활성화에 사용합니다.
        /// </summary>
        public int GetTotalFragmentCount(int characterId)
            => GetFragmentCount(characterId);

        /// <summary>추리 가능 여부를 확인합니다.</summary>
        public bool CanInquire(int characterId)
            => GetTotalFragmentCount(characterId) >= _conceptCardMinFragments;

        // ── 보상 해금 API ─────────────────────────────────────────────────

        /// <summary>
        /// 컨셉 카드 해금을 기록합니다.
        /// ProfileInquiryUI에서 정답 시 호출합니다.
        /// </summary>
        public void UnlockConceptCard(int characterId)
        {
            string key = $"conceptcard_{characterId}";
            if (_collectedFragmentIds.Contains(key)) return;

            _collectedFragmentIds.Add(key);
            _rewardSaveData?.SaveConceptCardUnlock(characterId);
            Save();

            Debug.Log($"[FragmentCollector] 컨셉 카드 해금 — #{characterId}");
        }

        /// <summary>
        /// 시점 완결문 해금을 기록합니다.
        /// ProfileInquiryUI에서 전부 정답 시 호출합니다.
        /// </summary>
        public void UnlockEpilogue(int characterId)
        {
            string key = $"epilogue_{characterId}";
            if (_collectedFragmentIds.Contains(key)) return;

            _collectedFragmentIds.Add(key);
            _rewardSaveData?.SaveEpilogueUnlock(characterId);
            Save();

            Debug.Log($"[FragmentCollector] 시점 완결문 해금 — #{characterId}");

            CheckAllCharactersCompleted();
        }

        /// <summary>컨셉 카드 해금 여부를 확인합니다.</summary>
        public bool IsConceptCardUnlocked(int characterId)
            => _collectedFragmentIds.Contains($"conceptcard_{characterId}");

        /// <summary>시점 완결문 해금 여부를 확인합니다.</summary>
        public bool IsEpilogueUnlocked(int characterId)
            => _collectedFragmentIds.Contains($"epilogue_{characterId}");

        // ── 저장 ─────────────────────────────────────────────────────────

        public void Save()
        {
            if (string.IsNullOrEmpty(_currentStageId)) return;

            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            // P 형식 조각만 저장 (conceptcard_, epilogue_ 제외)
            saveData.collectedFragmentIds.Clear();
            foreach (var id in _collectedFragmentIds)
                if (id.StartsWith("P"))
                    saveData.collectedFragmentIds.Add(id);

            CampaignSaveManager.Instance.Save(saveData);
        }

        public void Clear(string stageId)
        {
            _collectedFragmentIds.Clear();
            _fragmentCountPerChar.Clear();
            CampaignSaveManager.Instance?.Delete(stageId);
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>조각 수가 기준 이상이면 추리 활성화 이벤트를 발생시킵니다.</summary>
        private void CheckInquiryUnlock(int characterId)
        {
            if (GetFragmentCount(characterId) >= _conceptCardMinFragments)
                OnConceptCardUnlockable?.Invoke(characterId);
        }

        /// <summary>모든 캐릭터(#1~#7)의 에필로그가 해금됐는지 확인합니다.</summary>
        private void CheckAllCharactersCompleted()
        {
            for (int i = 1; i <= 7; i++)
                if (!IsEpilogueUnlocked(i)) return;

            Debug.Log("[FragmentCollector] 모든 캐릭터 완수 — 엔딩 조건 달성");
            OnAllCharactersCompleted?.Invoke();
        }

        /// <summary>
        /// FragmentId에서 캐릭터 ID를 파싱합니다.
        /// "P01_01" → 1 / "P07_05" → 7 / 그 외 → -1
        /// </summary>
        private int ParseCharacterIdFromFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return -1;
            if (fragmentId[0] != 'P') return -1;

            int underscoreIdx = fragmentId.IndexOf('_');
            if (underscoreIdx <= 1) return -1;

            string charPart = fragmentId.Substring(1, underscoreIdx - 1);
            return int.TryParse(charPart, out int charId) ? charId : -1;
        }
    }
}