using System;
using System.Collections.Generic;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// 대화 조각 수집 및 획득 조건 판별을 관리합니다.
    ///
    /// ─── 변경 이력 ───────────────────────────────────────────────────────
    ///   ProfileClueDataSO → FragmentDataSO 단일 SO로 교체.
    ///   CanInquire / UnlockConceptCard / UnlockEpilogue 제거.
    ///
    /// ─── FragmentId 명명 규칙 ────────────────────────────────────────────
    ///   형식: "P{characterId:00}_{clueIndex:00}"  예: "P02_01"
    ///
    /// ─── 특수 규칙 ───────────────────────────────────────────────────────
    ///   동시획득   : TryCollectFragment 시 SimultaneousIds 자동 지급
    ///   강제퇴고   : IsForcedExitFragment = true 조각은 퇴고 후 지급
    /// </summary>
    [DisallowMultipleComponent]
    public class FragmentCollector : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("FragmentDataSO 에셋입니다.")]
        [SerializeField] private FragmentDataSO _fragmentData;

        // ── 런타임 ───────────────────────────────────────────────────────

        private readonly HashSet<string> _collectedIds = new();
        private readonly Dictionary<int, int> _countPerChar = new();
        private string _currentStageId;

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>새 조각이 수집될 때 발생합니다. (string = ProfileClueId)</summary>
        public event Action<string> OnFragmentCollected;

        /// <summary>모든 조각(#2~#7)이 수집됐을 때 발생합니다.</summary>
        public event Action OnAllFragmentsCollected;

        // ── 초기화 ───────────────────────────────────────────────────────

        public void Initialize(string stageId)
        {
            _currentStageId = stageId;
            _collectedIds.Clear();
            _countPerChar.Clear();

            var saveData = CampaignSaveManager.Instance?.Load(stageId);
            if (saveData == null) return;

            foreach (var id in saveData.collectedFragmentIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                _collectedIds.Add(id);
                int charId = ParseCharId(id);
                if (charId >= 0)
                {
                    _countPerChar.TryGetValue(charId, out int cnt);
                    _countPerChar[charId] = cnt + 1;
                }
            }

            Debug.Log($"[FragmentCollector] 초기화 완료 — {stageId} ({_collectedIds.Count}개)");
        }

        // ── 조건 판별 ─────────────────────────────────────────────────────

        /// <summary>현재 상태에서 획득 가능한 미수집 조각을 반환합니다.</summary>
        public List<FragmentEntry> GetAvailableClues(
            int anchorZoneId,
            IReadOnlyList<int> charactersInZone,
            HashSet<int> deadCharacters)
        {
            var result = new List<FragmentEntry>();
            if (_fragmentData == null) return result;

            foreach (var f in _fragmentData.Fragments)
            {
                if (f == null) continue;
                if (_collectedIds.Contains(f.ProfileClueId)) continue;
                if (f.IsForcedExitFragment) continue;
                if (CheckConditions(f, anchorZoneId, charactersInZone, deadCharacters))
                    result.Add(f);
            }
            return result;
        }

        /// <summary>강제퇴고 직전 특수 조각을 반환합니다.</summary>
        public List<FragmentEntry> GetForcedExitClues(
            int anchorZoneId,
            IReadOnlyList<int> charactersInZone,
            HashSet<int> deadCharacters)
        {
            var result = new List<FragmentEntry>();
            if (_fragmentData == null) return result;

            foreach (var f in _fragmentData.Fragments)
            {
                if (f == null || !f.IsForcedExitFragment) continue;
                if (_collectedIds.Contains(f.ProfileClueId)) continue;
                if (CheckConditions(f, anchorZoneId, charactersInZone, deadCharacters))
                    result.Add(f);
            }
            return result;
        }

        /// <summary>단일 조각의 4가지 AND 조건을 모두 충족하는지 확인합니다.</summary>
        public bool CheckConditions(
            FragmentEntry entry,
            int anchorZoneId,
            IReadOnlyList<int> charactersInZone,
            HashSet<int> deadCharacters)
        {
            // 1. Combo — 지정 캐릭터 전원이 anchorZone에 생존
            if (entry.Combo != null && entry.Combo.Count > 0)
            {
                var zoneSet = new HashSet<int>(charactersInZone);
                foreach (int id in entry.Combo)
                    if (!zoneSet.Contains(id)) return false;
            }

            // 2. PlaceName — 장소 일치
            if (!string.IsNullOrEmpty(entry.PlaceName))
            {
                int requiredZone = PlaceNameMapper.ToZoneId(entry.PlaceName);
                if (requiredZone >= 0 && requiredZone != anchorZoneId)
                    return false;
            }

            // 3. DeadRequired — 지정 캐릭터 전원 사망
            if (entry.DeadRequired != null && entry.DeadRequired.Count > 0)
                foreach (int id in entry.DeadRequired)
                    if (deadCharacters == null || !deadCharacters.Contains(id))
                        return false;

            // 4. Prerequisites — 사전 조각 보유
            if (entry.Prerequisites != null && entry.Prerequisites.Count > 0)
                foreach (string prereq in entry.Prerequisites)
                    if (!string.IsNullOrEmpty(prereq) && !_collectedIds.Contains(prereq))
                        return false;

            return true;
        }

        // ── 수집 API ─────────────────────────────────────────────────────

        /// <summary>조각을 수집합니다. SimultaneousIds도 자동 지급합니다.</summary>
        public void TryCollectFragment(string profileClueId)
        {
            if (string.IsNullOrEmpty(profileClueId)) return;
            if (_collectedIds.Contains(profileClueId)) return;

            CollectInternal(profileClueId);

            // 동시 획득 처리
            var entry = _fragmentData?.FindById(profileClueId);
            if (entry?.SimultaneousIds != null)
                foreach (var simId in entry.SimultaneousIds)
                    if (!string.IsNullOrEmpty(simId) && !_collectedIds.Contains(simId))
                    {
                        Debug.Log($"[FragmentCollector] 동시 획득 — {simId} (← {profileClueId})");
                        CollectInternal(simId);
                    }

            CheckAllCollected();
        }

        public bool HasFragment(string profileClueId) => _collectedIds.Contains(profileClueId);

        public int GetFragmentCount(int characterId)
        {
            _countPerChar.TryGetValue(characterId, out int count);
            return count;
        }

        // ── FragmentDataSO 접근 ───────────────────────────────────────────

        /// <summary>캐릭터의 조각 목록을 반환합니다. (CharacterRecordPanel에서 사용)</summary>
        public List<FragmentEntry> GetFragmentsByCharacter(int characterId)
            => _fragmentData?.GetByCharacter(characterId) ?? new List<FragmentEntry>();

        /// <summary>ID로 단일 조각을 반환합니다.</summary>
        public FragmentEntry FindById(string profileClueId)
            => _fragmentData?.FindById(profileClueId);

        // ── 저장/초기화 ───────────────────────────────────────────────────

        public void Save()
        {
            if (string.IsNullOrEmpty(_currentStageId)) return;
            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            if (saveData == null) return;

            saveData.collectedFragmentIds.Clear();
            foreach (var id in _collectedIds)
                if (id.StartsWith("P"))
                    saveData.collectedFragmentIds.Add(id);

            CampaignSaveManager.Instance.Save(saveData);
        }

        public void Clear(string stageId)
        {
            _collectedIds.Clear();
            _countPerChar.Clear();
            CampaignSaveManager.Instance?.Delete(stageId);
        }

        // ── Private ──────────────────────────────────────────────────────

        private void CollectInternal(string profileClueId)
        {
            _collectedIds.Add(profileClueId);
            int charId = ParseCharId(profileClueId);
            if (charId >= 0)
            {
                _countPerChar.TryGetValue(charId, out int cnt);
                _countPerChar[charId] = cnt + 1;
            }

            Save();

            Debug.Log($"[FragmentCollector] 조각 수집 — {profileClueId} (#{charId})");

            GameLogger.Instance?.LogEvent("fragment_collected", new Dictionary<string, object>
            {
                { "fragment_id",  profileClueId },
                { "character_id", charId },
                { "total_count",  _collectedIds.Count },
            });

            OnFragmentCollected?.Invoke(profileClueId);
        }

        private void CheckAllCollected()
        {
            if (_fragmentData == null) return;
            foreach (var f in _fragmentData.Fragments)
            {
                if (f == null || f.CharacterId == 1) continue; // 엔비 제외
                if (!_collectedIds.Contains(f.ProfileClueId)) return;
            }
            Debug.Log("[FragmentCollector] 모든 조각 수집 완료");
            OnAllFragmentsCollected?.Invoke();
        }

        private static int ParseCharId(string fragmentId)
        {
            if (string.IsNullOrEmpty(fragmentId) || fragmentId[0] != 'P') return -1;
            int idx = fragmentId.IndexOf('_');
            if (idx <= 1) return -1;
            return int.TryParse(fragmentId.Substring(1, idx - 1), out int id) ? id : -1;
        }
    }
}