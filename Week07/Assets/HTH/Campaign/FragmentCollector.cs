using System;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 대화 조각 수집 및 보상 해금을 관리합니다.
    ///
    /// ─── 수집 흐름 ───────────────────────────────────────────────────────
    ///   다이얼로그 출력 완료
    ///   → TryCollectFragment(fragmentId)
    ///   → 중복 수집 방지 체크
    ///   → 조각 해금 + 저장
    ///   → 캐릭터별 수집 수 갱신
    ///   → 보상 체크 (컨셉 카드 / 시점 완결문 해금 가능 여부)
    ///
    /// ─── 보상 단계 (기획서 기준) ─────────────────────────────────────────
    ///   1단계: 일정 조각 수집 + 프로파일 일부 정답 → 컨셉 카드
    ///   2단계: 프로파일 완전 정답 → 시점 완결문
    ///   3단계: 스테이지 모든 인물 카드 해금 → 엔딩
    ///
    /// ─── FragmentId 명명 규칙 ────────────────────────────────────────────
    ///   "{stageId}_char{characterId}_frag{index}"
    ///   예: "Stage_1_Phase2_char1_frag0"
    ///
    /// ─── PlayerPrefs 키 규칙 ─────────────────────────────────────────────
    ///   수집된 조각: "hth_frag_{stageId}_{fragmentId}"
    /// </summary>
    [DisallowMultipleComponent]
    public class FragmentCollector : MonoBehaviour
    {
        // ── 컨셉 카드 해금에 필요한 최소 조각 수 (Inspector 설정 가능) ──────
        [Header("보상 조건")]
        [Tooltip("컨셉 카드 해금에 필요한 캐릭터별 최소 대화 조각 수")]
        [SerializeField] private int _conceptCardMinFragments = 3;

        [Header("보상 저장")]
        [Tooltip("로비에서 보상 열람에 사용할 저장 데이터")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        // ── 상태 ─────────────────────────────────────────────────────────

        private readonly HashSet<string> _collectedFragmentIds = new();
        private readonly Dictionary<int, int> _fragmentCountPerChar = new();
        private string _currentStageId;

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>새 대화 조각 수집 시 발생합니다. string: fragmentId</summary>
        public event Action<string> OnFragmentCollected;

        /// <summary>
        /// 컨셉 카드 해금 조건 충족 시 발생합니다.
        /// int: characterId
        /// </summary>
        public event Action<int> OnConceptCardUnlockable;

        /// <summary>
        /// 시점 완결문 해금 조건 충족 시 발생합니다.
        /// int: characterId
        /// </summary>
        public event Action<int> OnEpilogueUnlockable;

        /// <summary>
        /// 스테이지 전체 기록 완수 시 발생합니다. (엔딩 조건)
        /// </summary>
        public event Action OnAllCharactersCompleted;

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 스테이지 ID를 설정하고 저장된 수집 기록을 로드합니다.
        /// CampaignModeManager.OnPhase2Entered 이벤트 수신 시 호출합니다.
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
        /// 이미 수집됐거나 fragmentId가 비어있으면 무시합니다.
        /// DialogueTriggerManager에서 다이얼로그 출력 완료 후 호출합니다.
        /// </summary>
        public void TryCollectFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return;
            if (_collectedFragmentIds.Contains(fragmentId)) return;

            _collectedFragmentIds.Add(fragmentId);

            // 캐릭터별 카운트 갱신
            int charId = ParseCharacterIdFromFragment(fragmentId);
            if (charId >= 0)
            {
                _fragmentCountPerChar.TryGetValue(charId, out int count);
                _fragmentCountPerChar[charId] = count + 1;
            }

            Save();

            Debug.Log($"[FragmentCollector] 조각 수집 — {fragmentId} (캐릭터 {charId})");
            OnFragmentCollected?.Invoke(fragmentId);

            // 보상 체크
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

        // ── 저장/로드 ─────────────────────────────────────────────────────

        /// <summary>현재 수집 기록을 PlayerPrefs에 저장합니다.</summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(_currentStageId)) return;
            Save(_currentStageId);
        }

        /// <summary>특정 스테이지의 수집 기록을 PlayerPrefs에 저장합니다.</summary>
        public void Save(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return;

            string data = string.Join("|", _collectedFragmentIds);
            string prefKey = $"hth_frag_{stageId}";
            PlayerPrefs.SetString(prefKey, data);
            PlayerPrefs.Save();
        }

        /// <summary>특정 스테이지의 수집 기록을 PlayerPrefs에서 로드합니다.</summary>
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

                    int charId = ParseCharacterIdFromFragment(fragmentId);
                    if (charId >= 0)
                    {
                        _fragmentCountPerChar.TryGetValue(charId, out int count);
                        _fragmentCountPerChar[charId] = count + 1;
                    }
                }
            }
        }

        /// <summary>특정 스테이지의 모든 수집 기록을 초기화합니다.</summary>
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
        /// 1단계: 최소 조각 수 충족 → 컨셉 카드 해금 가능 이벤트
        /// 2단계: 프로파일 추리는 별도 시스템에서 처리 (추후 연동)
        /// </summary>
        private void CheckRewards(int characterId)
        {
            int count = GetFragmentCount(characterId);

            // 1단계: 컨셉 카드 해금 조건 충족
            if (count >= _conceptCardMinFragments)
            {
                Debug.Log($"[FragmentCollector] 컨셉 카드 해금 가능 — 캐릭터 {characterId} ({count}개)");
                OnConceptCardUnlockable?.Invoke(characterId);
            }
        }

        /// <summary>
        /// 컨셉 카드 해금을 기록합니다.
        /// ProfileInquiryUI에서 일부 정답 시 호출합니다.
        /// </summary>
        public void UnlockConceptCard(int characterId)
        {
            string key = $"conceptcard_{characterId}";
            if (_collectedFragmentIds.Contains(key)) return;

            _collectedFragmentIds.Add(key);
            Save();

            // 로비 보상 열람용 저장
            _rewardSaveData?.SaveConceptCardUnlock(characterId);

            Debug.Log($"[FragmentCollector] 컨셉 카드 해금 — 캐릭터 {characterId}");
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
            Save();

            // 로비 보상 열람용 저장
            _rewardSaveData?.SaveEpilogueUnlock(characterId);

            Debug.Log($"[FragmentCollector] 시점 완결문 해금 — 캐릭터 {characterId}");

            CheckAllCharactersCompleted();
        }

        /// <summary>컨셉 카드 해금 여부를 확인합니다.</summary>
        public bool IsConceptCardUnlocked(int characterId)
            => _collectedFragmentIds.Contains($"conceptcard_{characterId}");

        /// <summary>시점 완결문 해금 여부를 확인합니다.</summary>
        public bool IsEpilogueUnlocked(int characterId)
            => _collectedFragmentIds.Contains($"epilogue_{characterId}");

        /// <summary>
        /// 모든 캐릭터의 시점 완결문이 해금됐는지 체크합니다.
        /// 전부 완료 시 OnAllCharactersCompleted 이벤트를 발생시킵니다.
        /// </summary>
        private void CheckAllCharactersCompleted()
        {
            // 캐릭터 1~7 전부 완료 여부 체크
            for (int i = 1; i <= 7; i++)
            {
                if (!IsEpilogueUnlocked(i)) return;
            }

            Debug.Log("[FragmentCollector] 모든 캐릭터 기록 완수 — 엔딩 조건 달성");
            OnAllCharactersCompleted?.Invoke();
        }

        /// <summary>
        /// FragmentId에서 캐릭터 ID를 파싱합니다.
        /// 명명 규칙: "{stageId}_char{characterId}_frag{index}"
        /// </summary>
        private int ParseCharacterIdFromFragment(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId)) return -1;

            const string marker = "_char";
            int startIdx = fragmentId.IndexOf(marker, System.StringComparison.Ordinal);
            if (startIdx < 0) return -1;

            startIdx += marker.Length;
            int endIdx = fragmentId.IndexOf('_', startIdx);
            if (endIdx < 0) endIdx = fragmentId.Length;

            string idStr = fragmentId.Substring(startIdx, endIdx - startIdx);
            return int.TryParse(idStr, out int id) ? id : -1;
        }
    }
}