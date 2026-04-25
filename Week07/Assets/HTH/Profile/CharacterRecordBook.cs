using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 인물 기록장 UI입니다.
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   플레이어가 수집한 정보를 정리해 보여주는 기록장입니다.
    ///   각 캐릭터별로 수집된 대화 조각 수, 이름, 힌트를 표시합니다.
    ///   조각 수가 충족되면 "추리하기" 버튼이 활성화되어 프로파일 추리로 연결됩니다.
    ///
    /// ─── 이름 수집 ───────────────────────────────────────────────────────
    ///   DialogueTriggerManager.RevealCharacterNamesFromLines()에서 이름이 공개되면
    ///   RegisterCharacterName()이 호출됩니다.
    ///   등록된 이름은 기록장 UI와 ProfileInquiryUI의 이름 텍스트에 표시됩니다.
    ///   RewardSaveData에도 저장되어 로비 보상 열람에서도 표시됩니다.
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   Phase2 진입 → CampaignModeManager.OnPhase2Entered 이벤트 수신
    ///   → BuildEntries() → CharacterEntryView 프리팹을 캐릭터 수만큼 생성
    ///
    ///   조각 수집 → FragmentCollector.OnFragmentCollected 이벤트 수신
    ///   → 해당 캐릭터의 CharacterEntryView.Refresh() 호출 → UI 갱신
    ///
    ///   추리 가능 → FragmentCollector.OnConceptCardUnlockable 이벤트 수신
    ///   → CharacterEntryView.ShowInquiryAvailable() → 추리 버튼 배지 표시
    ///
    ///   이름 공개 → DialogueTriggerManager.RegisterCharacterName() 호출
    ///   → CharacterEntryView.UpdateName() → 이름 텍스트 갱신
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///   Canvas/CharacterRecordBookPanel을 Inspector의 Panel 필드에 연결합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data        → ProfileDataSO 에셋 (캐릭터 목록 기준)
    ///   Hint Data           → FragmentHintDataSO 에셋 (미수집 조각 힌트)
    ///   Fragment Collector  → _CampaignSystem/FragmentCollector
    ///   Profile Inquiry UI  → _CampaignSystem/ProfileInquiryUI (추리 버튼 클릭 시)
    ///   Reward Save Data    → RewardSaveData 에셋 (이름 저장용)
    ///   Panel               → Canvas/CharacterRecordBookPanel
    ///   Open Button         → 기록장 여는 버튼 (HUD 등에 배치)
    ///   Close Button        → 패널 내 닫기 버튼
    ///   Entry Container     → 캐릭터 항목이 생성될 부모 Transform
    ///   Entry Prefab        → CharacterEntryView.prefab
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterRecordBook : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("캐릭터 목록 기준으로 항목을 생성합니다.\n" +
                 "CharacterProfiles에 등록된 캐릭터 수만큼 EntryView가 생성됩니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Tooltip("각 캐릭터별 미수집 조각 힌트 텍스트를 담고 있습니다.\n" +
                 "예: #1 → '새턴과의 철학적 충돌', '리더십이 드러나는 상황'")]
        [SerializeField] private FragmentHintDataSO _hintData;

        [Header("컴포넌트 참조")]
        [Tooltip("대화 조각 수집 관리 컴포넌트입니다.\n" +
                 "조각 수 갱신과 추리 가능 이벤트를 수신합니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Tooltip("프로파일 추리 UI입니다.\n" +
                 "'추리하기' 버튼 클릭 시 Show()를 호출합니다.")]
        [SerializeField] private ProfileInquiryUI _profileInquiryUI;

        [Tooltip("로비 보상 열람용 저장 데이터입니다.\n" +
                 "이름 수집 시 여기에도 저장해 로비에서 이름을 볼 수 있습니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Header("UI")]
        [Tooltip("인물 기록장 전체 패널입니다.\nCanvas/CharacterRecordBookPanel을 연결합니다.")]
        [SerializeField] private GameObject _panel;

        [Tooltip("기록장을 여는 버튼입니다.\nHUD나 별도 버튼에 연결합니다.")]
        [SerializeField] private Button _openButton;

        [Tooltip("패널을 닫는 버튼입니다.")]
        [SerializeField] private Button _closeButton;

        [Tooltip("CharacterEntryView 프리팹이 생성될 부모 Transform입니다.\n" +
                 "Vertical Layout Group을 추가하면 자동으로 정렬됩니다.")]
        [SerializeField] private Transform _entryContainer;

        [Tooltip("캐릭터 항목 1개의 UI 프리팹입니다.\nCharacterEntryView.prefab을 연결합니다.")]
        [SerializeField] private CharacterEntryView _entryPrefab;

        // ── 런타임 데이터 ─────────────────────────────────────────────────

        // 캐릭터 ID별 CharacterEntryView 참조입니다.
        // 조각 수집, 이름 공개 등의 이벤트에서 해당 캐릭터의 뷰를 빠르게 찾을 때 사용합니다.
        private readonly Dictionary<int, CharacterEntryView> _entries = new();

        // 수집된 캐릭터 이름 목록입니다.
        // key = characterId, value = 이름 문자열
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
                // 새 조각이 수집될 때마다 해당 캐릭터의 항목 UI를 갱신합니다.
                _fragmentCollector.OnFragmentCollected += OnFragmentCollected;
                // 추리 가능 조건(최소 조각 수)이 충족되면 추리 버튼에 배지를 표시합니다.
                _fragmentCollector.OnConceptCardUnlockable += OnConceptCardUnlockable;
            }

            // Phase2 진입 이벤트 구독
            // Phase2가 시작되면 ProfileDataSO 기준으로 항목을 생성합니다.
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

        /// <summary>기록장 패널을 열고 현재 수집 현황으로 갱신합니다.</summary>
        public void Open()
        {
            if (_panel != null) _panel.SetActive(true);
            RefreshAll(); // 열 때마다 최신 상태로 갱신합니다.
        }

        /// <summary>기록장 패널을 닫습니다.</summary>
        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>
        /// 캐릭터 이름을 등록합니다.
        /// DialogueTriggerManager.RevealCharacterNamesFromLines()에서 호출합니다.
        ///
        /// 이미 등록된 이름은 덮어쓰지 않습니다 (최초 공개된 이름 유지).
        /// 이름은 RewardSaveData에도 저장되어 로비에서도 확인할 수 있습니다.
        /// </summary>
        public void RegisterCharacterName(int characterId, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (_collectedNames.ContainsKey(characterId)) return; // 이미 수집됨

            _collectedNames[characterId] = name;

            // 기록장 항목 UI의 이름 텍스트를 즉시 갱신합니다.
            if (_entries.TryGetValue(characterId, out var entry))
                entry.UpdateName(name);

            // 로비 보상 열람에서도 이름이 표시되도록 저장합니다.
            _rewardSaveData?.SaveCharacterName(characterId, name);

            Debug.Log($"[CharacterRecordBook] 이름 수집 — #{characterId}: {name}");
        }

        /// <summary>
        /// 특정 캐릭터의 수집된 이름을 반환합니다.
        /// 미수집 시 null을 반환합니다.
        /// ProfileInquiryUI와 ProfileInquiryAllUI에서 이름 표시에 사용됩니다.
        /// </summary>
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

        /// <summary>
        /// ProfileDataSO의 캐릭터 목록 기준으로 CharacterEntryView를 생성합니다.
        /// 기존 항목을 모두 제거하고 새로 생성합니다.
        /// </summary>
        private void BuildEntries()
        {
            // 기존 항목 제거
            foreach (var entry in _entries.Values)
                if (entry != null) Destroy(entry.gameObject);
            _entries.Clear();

            if (_entryPrefab == null || _entryContainer == null || _profileData == null) return;

            foreach (var profile in _profileData.CharacterProfiles)
            {
                if (profile == null) continue;

                // CharacterEntryView 프리팹을 생성하고 초기 설정합니다.
                var entry = Instantiate(_entryPrefab, _entryContainer);
                entry.Setup(
                    profile.CharacterId,
                    profile.RequiredFragmentCount,
                    GetHints(profile.CharacterId),
                    // "추리하기" 버튼 클릭 시 ProfileInquiryUI.Show()를 호출합니다.
                    onInquiryClicked: () => OnInquiryButtonClicked(profile.CharacterId));

                _entries[profile.CharacterId] = entry;
            }

            RefreshAll();
        }

        /// <summary>모든 항목의 UI를 현재 수집 현황으로 갱신합니다.</summary>
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

        /// <summary>FragmentHintDataSO에서 캐릭터의 힌트 목록을 가져옵니다.</summary>
        private List<string> GetHints(int characterId)
        {
            return _hintData?.GetHints(characterId) ?? new List<string>();
        }

        // ── Private — 이벤트 핸들러 ──────────────────────────────────────

        /// <summary>
        /// 새 조각이 수집됐을 때 해당 캐릭터의 항목 UI를 갱신합니다.
        /// FragmentId에서 캐릭터 ID를 파싱해 해당 항목만 갱신합니다.
        /// </summary>
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

        /// <summary>추리 가능 조건이 충족되면 해당 항목에 배지를 표시합니다.</summary>
        private void OnConceptCardUnlockable(int characterId)
        {
            if (_entries.TryGetValue(characterId, out var entry))
                entry.ShowInquiryAvailable();
        }

        /// <summary>"추리하기" 버튼 클릭 시 ProfileInquiryUI를 엽니다.</summary>
        private void OnInquiryButtonClicked(int characterId)
        {
            _profileInquiryUI?.Show(characterId);
        }

        // ── Private — 유틸 ───────────────────────────────────────────────

        /// <summary>
        /// FragmentId에서 캐릭터 ID를 파싱합니다.
        /// FragmentCollector와 동일한 파싱 로직을 사용합니다.
        /// 형식: "{stageId}_char{id}_frag{index}" → id 반환
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