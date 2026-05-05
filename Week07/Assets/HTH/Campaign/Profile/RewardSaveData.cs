using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 보상 해금 기록을 관리하는 ScriptableObject입니다.
    ///
    /// ─── 저장 방식 변경 ──────────────────────────────────────────────────
    ///   PlayerPrefs → CampaignSaveManager JSON 파일로 통합
    ///   CampaignSaveData.unlockedConceptCards / unlockedEpilogues 사용
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   컨셉 카드 해금 여부 저장/조회
    ///   시점 완결문 해금 여부 저장/조회
    ///   수집된 캐릭터 이름 저장/조회
    ///
    /// ─── 사용처 ──────────────────────────────────────────────────────────
    ///   인게임: FragmentCollector에서 해금 시 Save 호출
    ///   로비:   LobbyPage2Controller, LobbyPage1Controller에서 조회
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

        private bool _isTutorialCleared;

        private bool _isLoaded;

        // ── 공개 API — 해금 기록 ─────────────────────────────────────────

        /// <summary>컨셉 카드 해금을 저장합니다.</summary>
        public void SaveConceptCardUnlock(int characterId, string characterName = null)
        {
            EnsureLoaded();
            _unlockedConceptCards.Add(characterId);

            if (!string.IsNullOrEmpty(characterName))
                _characterNames[characterId] = characterName;

            Flush();
            Debug.Log($"[RewardSaveData] 컨셉 카드 저장 — #{characterId}");
        }

        /// <summary>시점 완결문 해금을 저장합니다.</summary>
        public void SaveEpilogueUnlock(int characterId, string characterName = null)
        {
            EnsureLoaded();
            _unlockedEpilogues.Add(characterId);

            if (!string.IsNullOrEmpty(characterName))
                _characterNames[characterId] = characterName;

            Flush();
            Debug.Log($"[RewardSaveData] 시점 완결문 저장 — #{characterId}");
        }

        /// <summary>수집된 캐릭터 이름을 저장합니다.</summary>
        public void SaveCharacterName(int characterId, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            EnsureLoaded();

            _characterNames[characterId] = name;

            // 이름은 CampaignSaveData.collectedNames에 이미 저장됨
            // 여기서는 캐시만 갱신
            Flush();
        }

        // 튜토리얼 클리어를 저장합니다.
        public void SaveTutorialClear()
        {
            EnsureLoaded();
            _isTutorialCleared = true;
            Flush();
            Debug.Log($"[RewardSaveData] 튜토리얼 클리어 저장");
        }

        // ── 공개 API — 조회 ──────────────────────────────────────────────

        /// <summary>컨셉 카드 해금 여부를 반환합니다.</summary>
        public bool IsConceptCardUnlocked(int characterId)
        {
            EnsureLoaded();
            return _unlockedConceptCards.Contains(characterId);
        }

        /// <summary>시점 완결문 해금 여부를 반환합니다.</summary>
        public bool IsEpilogueUnlocked(int characterId)
        {
            EnsureLoaded();
            return _unlockedEpilogues.Contains(characterId);
        }

        /// <summary>해금된 시점 완결문 수를 반환합니다.</summary>
        public int GetUnlockedEpilogueCount()
        {
            EnsureLoaded();
            return _unlockedEpilogues.Count;
        }

        /// <summary>수집된 캐릭터 이름을 반환합니다. 미수집 시 null.</summary>
        public string GetCharacterName(int characterId)
        {
            EnsureLoaded();
            _characterNames.TryGetValue(characterId, out string name);
            return name;
        }

        // 튜토리얼 클리어 여부를 반환합니다.
        public bool IsTutorialCleared()
        {
            EnsureLoaded();
            return _isTutorialCleared;
        }

        // ── 저장/로드 ─────────────────────────────────────────────────────

        /// <summary>
        /// CampaignSaveData에서 보상 기록을 로드합니다.
        /// CampaignSaveManager.Load() 이후 호출합니다.
        /// </summary>
        public void Load()
        {
            _unlockedConceptCards.Clear();
            _unlockedEpilogues.Clear();
            _characterNames.Clear();
            _isTutorialCleared = false;

            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null)
            {
                // CampaignSaveManager가 없으면 직접 로드
                saveData = CampaignSaveManager.Instance?.Load(_stageId);
            }

            if (saveData != null)
            {
                foreach (int id in saveData.unlockedConceptCards)
                    _unlockedConceptCards.Add(id);

                foreach (int id in saveData.unlockedEpilogues)
                    _unlockedEpilogues.Add(id);

                foreach (var entry in saveData.collectedNames)
                    if (!string.IsNullOrEmpty(entry.name))
                        _characterNames[entry.characterId] = entry.name;

                _isTutorialCleared = saveData.isTutorialCleared;
            }

            _isLoaded = true;
            Debug.Log($"[RewardSaveData] 로드 완료 — {_stageId} " +
                      $"(컨셉 카드 {_unlockedConceptCards.Count}개, " +
                      $"시점 완결문 {_unlockedEpilogues.Count}개)");
        }

        /// <summary>
        /// 메모리 상태만 초기화합니다.
        /// JSON 파일 조작은 CampaignSaveManager.ResetFull / ResetPartial에서 처리합니다.
        /// </summary>
        public void Clear()
        {
            _unlockedConceptCards.Clear();
            _unlockedEpilogues.Clear();
            _characterNames.Clear();
            _isLoaded = false;
            Debug.Log($"[RewardSaveData] 메모리 초기화 — {_stageId}");
        }

        // ── ScriptableObject 재사용 시 런타임 상태 초기화 ─────────────────

        private void OnEnable()
        {
            _isLoaded = false;
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>로드되지 않은 경우 자동으로 로드합니다.</summary>
        private void EnsureLoaded()
        {
            if (!_isLoaded) Load();
        }

        /// <summary>현재 캐시를 CampaignSaveData에 반영하고 저장합니다.</summary>
        private void Flush()
        {
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            // 컨셉 카드 동기화
            saveData.unlockedConceptCards.Clear();
            saveData.unlockedConceptCards.AddRange(_unlockedConceptCards);

            // 시점 완결문 동기화
            saveData.unlockedEpilogues.Clear();
            saveData.unlockedEpilogues.AddRange(_unlockedEpilogues);

            saveData.isTutorialCleared = _isTutorialCleared;

            CampaignSaveManager.Instance.Save(saveData);
        }
    }
}