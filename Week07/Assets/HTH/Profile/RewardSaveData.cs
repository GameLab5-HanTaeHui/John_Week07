using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 보상 해금 기록을 저장/로드하는 클래스입니다.
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   컨셉 카드 해금 여부 저장
    ///   시점 완결문 해금 여부 저장
    ///   수집된 캐릭터 이름 저장
    ///   씬 간 데이터 유지 (PlayerPrefs 기반)
    ///
    /// ─── 사용처 ──────────────────────────────────────────────────────────
    ///   인게임: FragmentCollector에서 해금 시 Save() 호출
    ///   로비:   RewardGalleryUI에서 Load() 후 열람
    ///
    /// ─── PlayerPrefs 키 규칙 ─────────────────────────────────────────────
    ///   컨셉 카드: "hth_reward_{stageId}_conceptcard_{characterId}"
    ///   시점 완결문: "hth_reward_{stageId}_epilogue_{characterId}"
    ///   캐릭터 이름: "hth_reward_{stageId}_name_{characterId}"
    /// </summary>
    [CreateAssetMenu(fileName = "RewardSaveData",
                     menuName = "HTH/Campaign/RewardSaveData")]
    public class RewardSaveData : ScriptableObject
    {
        [Header("스테이지 ID")]
        [SerializeField] private string _stageId;

        // ── 런타임 캐시 ───────────────────────────────────────────────────

        private readonly HashSet<int> _unlockedConceptCards = new();
        private readonly HashSet<int> _unlockedEpilogues = new();
        private readonly Dictionary<int, string> _characterNames = new();

        private bool _isLoaded;

        // ── 공개 API — 해금 기록 ─────────────────────────────────────────

        /// <summary>컨셉 카드 해금을 저장합니다.</summary>
        public void SaveConceptCardUnlock(int characterId, string characterName = null)
        {
            _unlockedConceptCards.Add(characterId);

            string key = $"hth_reward_{_stageId}_conceptcard_{characterId}";
            PlayerPrefs.SetInt(key, 1);

            if (!string.IsNullOrEmpty(characterName))
                SaveCharacterName(characterId, characterName);

            PlayerPrefs.Save();
            Debug.Log($"[RewardSaveData] 컨셉 카드 저장 — #{characterId}");
        }

        /// <summary>시점 완결문 해금을 저장합니다.</summary>
        public void SaveEpilogueUnlock(int characterId, string characterName = null)
        {
            _unlockedEpilogues.Add(characterId);

            string key = $"hth_reward_{_stageId}_epilogue_{characterId}";
            PlayerPrefs.SetInt(key, 1);

            if (!string.IsNullOrEmpty(characterName))
                SaveCharacterName(characterId, characterName);

            PlayerPrefs.Save();
            Debug.Log($"[RewardSaveData] 시점 완결문 저장 — #{characterId}");
        }

        /// <summary>수집된 캐릭터 이름을 저장합니다.</summary>
        public void SaveCharacterName(int characterId, string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            _characterNames[characterId] = name;

            string key = $"hth_reward_{_stageId}_name_{characterId}";
            PlayerPrefs.SetString(key, name);
            PlayerPrefs.Save();
        }

        // ── 공개 API — 조회 ──────────────────────────────────────────────

        /// <summary>컨셉 카드 해금 여부를 반환합니다.</summary>
        public bool IsConceptCardUnlocked(int characterId)
        {
            if (!_isLoaded) Load();
            return _unlockedConceptCards.Contains(characterId);
        }

        /// <summary>시점 완결문 해금 여부를 반환합니다.</summary>
        public bool IsEpilogueUnlocked(int characterId)
        {
            if (!_isLoaded) Load();
            return _unlockedEpilogues.Contains(characterId);
        }

        /// <summary>수집된 캐릭터 이름을 반환합니다. 미수집 시 null.</summary>
        public string GetCharacterName(int characterId)
        {
            if (!_isLoaded) Load();
            _characterNames.TryGetValue(characterId, out string name);
            return name;
        }

        // ── 저장/로드 ─────────────────────────────────────────────────────

        /// <summary>PlayerPrefs에서 해금 기록을 로드합니다.</summary>
        public void Load()
        {
            _unlockedConceptCards.Clear();
            _unlockedEpilogues.Clear();
            _characterNames.Clear();

            // 캐릭터 1~7 기준으로 로드
            for (int i = 1; i <= 7; i++)
            {
                string conceptKey = $"hth_reward_{_stageId}_conceptcard_{i}";
                if (PlayerPrefs.GetInt(conceptKey, 0) == 1)
                    _unlockedConceptCards.Add(i);

                string epilogueKey = $"hth_reward_{_stageId}_epilogue_{i}";
                if (PlayerPrefs.GetInt(epilogueKey, 0) == 1)
                    _unlockedEpilogues.Add(i);

                string nameKey = $"hth_reward_{_stageId}_name_{i}";
                string name = PlayerPrefs.GetString(nameKey, string.Empty);
                if (!string.IsNullOrEmpty(name))
                    _characterNames[i] = name;
            }

            _isLoaded = true;
            Debug.Log($"[RewardSaveData] 로드 완료 — {_stageId} " +
                      $"(컨셉 카드 {_unlockedConceptCards.Count}개, " +
                      $"시점 완결문 {_unlockedEpilogues.Count}개)");
        }

        /// <summary>특정 스테이지의 모든 보상 기록을 초기화합니다.</summary>
        public void Clear()
        {
            for (int i = 1; i <= 7; i++)
            {
                PlayerPrefs.DeleteKey($"hth_reward_{_stageId}_conceptcard_{i}");
                PlayerPrefs.DeleteKey($"hth_reward_{_stageId}_epilogue_{i}");
                PlayerPrefs.DeleteKey($"hth_reward_{_stageId}_name_{i}");
            }

            PlayerPrefs.Save();

            _unlockedConceptCards.Clear();
            _unlockedEpilogues.Clear();
            _characterNames.Clear();
            _isLoaded = false;

            Debug.Log($"[RewardSaveData] 보상 기록 초기화 — {_stageId}");
        }

        // ── ScriptableObject 재사용 시 런타임 상태 초기화 ─────────────────

        private void OnEnable()
        {
            _isLoaded = false;
        }
    }
}