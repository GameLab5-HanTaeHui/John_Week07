using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 인물 기록장 UI 메인 컨트롤러입니다. (완전 재설계)
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   좌측 캐릭터 그리드와 우측 상세 뷰를 연결하는 메인 컨트롤러입니다.
    ///   ProfileDataSO 기준으로 캐릭터 아이콘 버튼을 동적 생성합니다.
    ///   FragmentCollector 이벤트를 수신해 UI를 실시간 갱신합니다.
    ///
    /// ─── UI 구조 ─────────────────────────────────────────────────────────
    ///   CharacterRecordBookPanel
    ///   ├── LeftPanel
    ///   │   └── CharacterGridContainer  ← 아이콘 버튼 동적 생성
    ///   ├── RightPanel                  ← CharacterDetailView
    ///   └── CloseButton
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   Open() 호출 → 그리드 생성 → 첫 캐릭터 자동 선택
    ///   캐릭터 클릭 → CharacterDetailView 갱신
    ///   조각 수집   → 아이콘 상태 갱신 + 현재 상세 뷰 갱신
    ///   이름 공개   → 아이콘 상태 갱신 + 헤더 갱신
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 추가합니다.
    ///   Canvas/CharacterRecordBookPanel을 Panel 필드에 연결합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data            → ProfileDataSO 에셋
    ///   Hint Data               → FragmentHintDataSO 에셋
    ///   Fragment Collector      → _CampaignSystem/FragmentCollector
    ///   Panel                   → Canvas/CharacterRecordBookPanel
    ///   Grid Container          → LeftPanel/CharacterGridContainer
    ///   Character Icon Prefab   → CharacterIconButton.prefab
    ///   Detail View             → RightPanel (CharacterDetailView 컴포넌트)
    ///   Open Button             → 기록장 열기 버튼 (HUD 등)
    ///   Close Button            → 패널 닫기 버튼
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterRecordBook : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("캐릭터 목록 기준으로 그리드를 생성합니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Tooltip("미수집 조각 힌트 텍스트 데이터입니다.")]
        [SerializeField] private FragmentHintDataSO _hintData;

        [Header("컴포넌트 참조")]
        [Tooltip("대화 조각 수집 관리 컴포넌트입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Tooltip("보상 저장 데이터입니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Header("UI")]
        [Tooltip("인물 기록장 전체 패널입니다.\nCanvas/CharacterRecordBookPanel을 연결합니다.")]
        [SerializeField] private GameObject _panel;

        [Tooltip("캐릭터 아이콘 버튼이 생성될 부모 Transform입니다.\n" +
                 "Grid Layout Group을 추가하면 자동으로 정렬됩니다.")]
        [SerializeField] private Transform _gridContainer;

        [Tooltip("캐릭터 아이콘 버튼 프리팹입니다.\nCharacterIconButton.prefab을 연결합니다.")]
        [SerializeField] private CharacterIconButton _iconButtonPrefab;

        [Tooltip("우측 상세 뷰 컴포넌트입니다.\nRightPanel GameObject에 CharacterDetailView를 추가하고 연결합니다.")]
        [SerializeField] private CharacterDetailView _detailView;

        [Header("버튼")]
        [Tooltip("기록장을 여는 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button _openButton;

        [Tooltip("패널을 닫는 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button _closeButton;

        // ── 런타임 데이터 ─────────────────────────────────────────────────

        // 생성된 아이콘 버튼 목록 (characterId → 버튼)
        private readonly Dictionary<int, CharacterIconButton> _iconButtons = new();

        // 수집된 캐릭터 이름 (characterId → 이름)
        private readonly Dictionary<int, string> _collectedNames = new();

        // 현재 선택된 캐릭터 ID
        private int _selectedCharacterId = -1;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);

            _openButton?.onClick.AddListener(Open);
            _closeButton?.onClick.AddListener(Close);

            // CharacterDetailView 의존성 주입
            _detailView?.Initialize(_fragmentCollector, _hintData, this);
        }

        private void Start()
        {
            // FragmentCollector 이벤트 구독
            if (_fragmentCollector != null)
            {
                _fragmentCollector.OnFragmentCollected += OnFragmentCollected;
                _fragmentCollector.OnConceptCardUnlockable += OnConceptCardUnlockable;
            }

            // Phase2 진입 시 그리드 초기화
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

        /// <summary>인물 기록장을 엽니다.</summary>
        public void Open()
        {
            if (_panel != null) _panel.SetActive(true);

            // 그리드가 비어있으면 빌드
            if (_iconButtons.Count == 0)
                BuildGrid();

            // 선택 복원 또는 첫 캐릭터 선택
            if (_selectedCharacterId >= 0)
                SelectCharacter(_selectedCharacterId);
            else if (_profileData != null && _profileData.CharacterProfiles.Count > 0)
                SelectCharacter(_profileData.CharacterProfiles[0].CharacterId);
        }

        /// <summary>인물 기록장을 닫습니다.</summary>
        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>
        /// 캐릭터 이름을 등록합니다.
        /// DialogueTriggerManager.RevealCharacterNamesFromLines()에서 호출합니다.
        /// </summary>
        public void RegisterCharacterName(int characterId, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (_collectedNames.ContainsKey(characterId)) return;

            _collectedNames[characterId] = name;

            // 아이콘 버튼 이름 갱신
            if (_iconButtons.TryGetValue(characterId, out var btn))
                btn.UpdateName(name);

            // 현재 선택된 캐릭터라면 상세 뷰 헤더 갱신
            _detailView?.RefreshIfCurrent(characterId);

            // 보상 저장
            _rewardSaveData?.SaveCharacterName(characterId, name);

            Debug.Log($"[CharacterRecordBook] 이름 수집 — #{characterId}: {name}");

            // 이름 공개 로그
            GameLogger.Instance?.LogEvent("name_revealed",
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "character_id",      characterId },
                    { "revealed_name",     name },
                    { "total_names_known", _collectedNames.Count },
                });
        }

        /// <summary>특정 캐릭터의 수집된 이름을 반환합니다. 미수집 시 null.</summary>
        public string GetCollectedName(int characterId)
        {
            _collectedNames.TryGetValue(characterId, out string name);
            return name;
        }

        // ── Private — 그리드 빌드 ─────────────────────────────────────────

        private void OnPhase2Entered(string stageId)
        {
            BuildGrid();
        }

        /// <summary>
        /// ProfileDataSO 기준으로 캐릭터 아이콘 버튼을 동적 생성합니다.
        /// </summary>
        private void BuildGrid()
        {
            // 기존 버튼 제거
            foreach (var btn in _iconButtons.Values)
                if (btn != null) Destroy(btn.gameObject);
            _iconButtons.Clear();

            if (_iconButtonPrefab == null || _gridContainer == null) return;
            if (_profileData == null) return;

            foreach (var profile in _profileData.CharacterProfiles)
            {
                if (profile == null) continue;

                int charId = profile.CharacterId;

                var btn = Instantiate(_iconButtonPrefab, _gridContainer);

                // 수집된 이름이 있으면 실제 이름, 없으면 CharacterFullName 표시
                string displayName = _collectedNames.TryGetValue(charId, out var n)
                    ? n
                    : profile.CharacterFullName;

                btn.Setup(
                    charId,
                    profile.CharacterIcon,
                    collectedName: displayName,
                    onClicked: () => SelectCharacter(charId));

                _iconButtons[charId] = btn;
            }
        }

        /// <summary>
        /// 캐릭터를 선택하고 상세 뷰를 갱신합니다.
        /// </summary>
        private void SelectCharacter(int characterId)
        {
            _selectedCharacterId = characterId;

            // 모든 버튼 선택 해제
            foreach (var btn in _iconButtons.Values)
                btn?.SetSelected(false);

            // 선택된 버튼 활성화
            if (_iconButtons.TryGetValue(characterId, out var selectedBtn))
                selectedBtn.SetSelected(true);

            // 상세 뷰 갱신
            var profile = _profileData?.FindProfile(characterId);
            if (profile != null)
                _detailView?.ShowCharacter(characterId, profile);
        }

        // ── Private — 이벤트 핸들러 ──────────────────────────────────────

        private void OnFragmentCollected(string fragmentId)
        {
            int charId = ParseCharacterIdFromFragment(fragmentId);

            Debug.Log($"[CharacterRecordBook] OnFragmentCollected — " +
                      $"fragmentId={fragmentId}, " +
                      $"charId={charId}, " +
                      $"currentId={_selectedCharacterId}, " +
                      $"panelActive={_panel?.activeSelf}");

            if (charId < 0) return;

            _detailView?.RefreshIfCurrent(charId);
        }

        private void OnConceptCardUnlockable(int characterId)
        {
            // 현재 선택된 캐릭터라면 탭 상태 갱신
            _detailView?.RefreshIfCurrent(characterId);
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