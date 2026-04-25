using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 인물 기록장 UI입니다.
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   수집된 대화 조각 목록 표시
    ///   수집된 캐릭터 이름 표시 (미수집 시 #번호로 표시)
    ///   미수집 조각 힌트 표시
    ///   프로파일 추리 버튼 (조각 수 충족 시 활성화)
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   FragmentCollector.OnFragmentCollected 이벤트 수신
    ///   → RefreshCharacterEntry(characterId) 호출
    ///   → 수집 현황 갱신
    ///   → 조각 수 충족 시 프로파일 추리 버튼 활성화
    ///
    /// ─── Canvas 구조 ─────────────────────────────────────────────────────
    ///   CharacterRecordBook
    ///   ├── Panel (전체 패널)
    ///   │   ├── OpenButton          ← 기록장 열기 버튼
    ///   │   └── CharacterEntryContainer
    ///   │       └── CharacterEntry (x7)  ← 캐릭터별 항목
    ///   │           ├── CharacterIdText  (#1)
    ///   │           ├── CharacterNameText (??? or 이름)
    ///   │           ├── FragmentCountText (0/7)
    ///   │           ├── FragmentHintList  (미수집 힌트)
    ///   │           └── InquiryButton     (프로파일 추리)
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   ProfileData        → ProfileDataSO (프로파일 데이터)
    ///   FragmentCollector  → 대화 조각 수집 관리자
    ///   ProfileInquiryUI   → 프로파일 추리 UI
    ///   FragmentHintDataSO → 미수집 조각 힌트 데이터
    ///   EntryPrefab        → 캐릭터 항목 UI 프리팹
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterRecordBook : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [SerializeField] private ProfileDataSO _profileData;
        [SerializeField] private FragmentHintDataSO _hintData;

        [Header("컴포넌트 참조")]
        [SerializeField] private FragmentCollector _fragmentCollector;
        [SerializeField] private ProfileInquiryUI _profileInquiryUI;
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Header("UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Transform _entryContainer;
        [SerializeField] private CharacterEntryView _entryPrefab;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private readonly Dictionary<int, CharacterEntryView> _entries = new();
        private readonly Dictionary<int, string> _collectedNames = new();

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);

            _openButton?.onClick.AddListener(Open);
            _closeButton?.onClick.AddListener(Close);
        }

        private void Start()
        {
            // FragmentCollector 이벤트 구독
            if (_fragmentCollector != null)
            {
                _fragmentCollector.OnFragmentCollected += OnFragmentCollected;
                _fragmentCollector.OnConceptCardUnlockable += OnConceptCardUnlockable;
            }

            // Phase2 진입 시 초기화
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered += OnPhase2Entered;
        }

        private void OnDestroy()
        {
            if (_fragmentCollector != null)
            {
                _fragmentCollector.OnFragmentCollected -= OnFragmentCollected;
                _fragmentCollector.OnConceptCardUnlockable -= OnConceptCardUnlockable;
            }

            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered -= OnPhase2Entered;

            _openButton?.onClick.RemoveListener(Open);
            _closeButton?.onClick.RemoveListener(Close);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>기록장을 엽니다.</summary>
        public void Open()
        {
            if (_panel != null) _panel.SetActive(true);
            RefreshAll();
        }

        /// <summary>기록장을 닫습니다.</summary>
        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>
        /// 캐릭터 이름을 등록합니다.
        /// 대화 조각에 이름이 포함된 경우 DialogueTriggerManager에서 호출합니다.
        /// </summary>
        public void RegisterCharacterName(int characterId, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (_collectedNames.ContainsKey(characterId)) return; // 이미 수집됨

            _collectedNames[characterId] = name;

            if (_entries.TryGetValue(characterId, out var entry))
                entry.UpdateName(name);

            // 로비 보상 열람용 이름 저장
            _rewardSaveData?.SaveCharacterName(characterId, name);

            Debug.Log($"[CharacterRecordBook] 이름 수집 — #{characterId}: {name}");
        }

        /// <summary>특정 캐릭터의 수집된 이름을 반환합니다. 미수집 시 null.</summary>
        public string GetCollectedName(int characterId)
        {
            _collectedNames.TryGetValue(characterId, out string name);
            return name;
        }

        // ── Private — 초기화 ─────────────────────────────────────────────

        private void OnPhase2Entered(string stageId)
        {
            BuildEntries();
        }

        private void BuildEntries()
        {
            // 기존 항목 제거
            foreach (var entry in _entries.Values)
                if (entry != null) Destroy(entry.gameObject);
            _entries.Clear();

            if (_entryPrefab == null || _entryContainer == null) return;
            if (_profileData == null) return;

            // 캐릭터별 항목 생성 (#1~#7)
            foreach (var profile in _profileData.CharacterProfiles)
            {
                if (profile == null) continue;

                var entry = Instantiate(_entryPrefab, _entryContainer);
                entry.Setup(
                    profile.CharacterId,
                    profile.RequiredFragmentCount,
                    GetHints(profile.CharacterId),
                    onInquiryClicked: () => OnInquiryButtonClicked(profile.CharacterId));

                _entries[profile.CharacterId] = entry;
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            foreach (var kv in _entries)
            {
                int charId = kv.Key;
                var entry = kv.Value;
                int fragmentCount = _fragmentCollector?.GetFragmentCount(charId) ?? 0;
                int required = _profileData?.FindProfile(charId)?.RequiredFragmentCount ?? 3;
                string name = GetCollectedName(charId);

                entry.Refresh(fragmentCount, required, name);
            }
        }

        private List<string> GetHints(int characterId)
        {
            return _hintData?.GetHints(characterId) ?? new List<string>();
        }

        // ── Private — 이벤트 핸들러 ──────────────────────────────────────

        private void OnFragmentCollected(string fragmentId)
        {
            int charId = ParseCharacterIdFromFragment(fragmentId);
            if (charId < 0) return;

            if (_entries.TryGetValue(charId, out var entry))
            {
                int fragmentCount = _fragmentCollector?.GetFragmentCount(charId) ?? 0;
                int required = _profileData?.FindProfile(charId)?.RequiredFragmentCount ?? 3;
                string name = GetCollectedName(charId);
                entry.Refresh(fragmentCount, required, name);
            }
        }

        private void OnConceptCardUnlockable(int characterId)
        {
            if (_entries.TryGetValue(characterId, out var entry))
                entry.ShowInquiryAvailable();
        }

        private void OnInquiryButtonClicked(int characterId)
        {
            _profileInquiryUI?.Show(characterId);
        }

        // ── Private — 유틸 ───────────────────────────────────────────────

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