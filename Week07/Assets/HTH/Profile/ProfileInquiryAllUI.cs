using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// Phase2 인물 추리 전체 UI입니다.
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   HoldToEnterFinalDecision에서 Phase2 확인 버튼을 클릭하면 이 UI가 열립니다.
    ///   캐릭터 #1~#7의 추리 가능 여부와 완료 여부를 목록으로 보여줍니다.
    ///   캐릭터를 선택하면 ProfileInquiryUI(개별 추리)를 열어줍니다.
    ///
    /// ─── 캐릭터 버튼 상태 ────────────────────────────────────────────────
    ///   잠금 (조각 부족): 버튼 비활성화 + 잠금 아이콘 표시
    ///   추리 가능       : 버튼 활성화
    ///   추리 완료       : 완료 아이콘 표시
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   HoldToEnterFinalDecision.OnFillComplete() (Phase2)
    ///   → ConfirmPanel 확인 클릭
    ///   → ProfileInquiryAllUI.Show() 호출
    ///   → BuildCharacterButtons() → 캐릭터 선택 버튼 생성
    ///   → 캐릭터 버튼 클릭 → OnCharacterSelected()
    ///   → ProfileInquiryUI.Show(characterId)
    ///   → 추리 완료 → FragmentCollector.OnConceptCardUnlockable 이벤트
    ///   → RefreshCharacterButton(characterId) → 완료 아이콘 표시
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///   Canvas/ProfileInquiryAllPanel을 Inspector의 Panel 필드에 연결합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data               → ProfileDataSO 에셋
    ///   Fragment Collector         → _CampaignSystem/FragmentCollector
    ///   Character Record Book      → _CampaignSystem/CharacterRecordBook
    ///   Panel                      → Canvas/ProfileInquiryAllPanel
    ///   Character Select Container → ProfileInquiryAllPanel 하위 빈 GameObject (버튼 목록 부모)
    ///   Select Button Prefab       → CharacterSelectButton.prefab
    ///   Close Button               → 닫기 Button
    ///   Profile Inquiry UI         → _CampaignSystem/ProfileInquiryUI
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileInquiryAllUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("캐릭터별 프로파일 데이터입니다.\n" +
                 "CharacterProfiles 목록의 순서대로 버튼이 생성됩니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Tooltip("대화 조각 수집 관리 컴포넌트입니다.\n" +
                 "조각 수와 해금 여부를 확인할 때 사용됩니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Tooltip("인물 기록장 컴포넌트입니다.\n" +
                 "수집된 캐릭터 이름을 버튼에 표시할 때 사용됩니다.")]
        [SerializeField] private CharacterRecordBook _characterRecordBook;

        [Header("UI")]
        [Tooltip("전체 패널입니다.\nCanvas/ProfileInquiryAllPanel을 연결합니다.")]
        [SerializeField] private GameObject _panel;

        [Tooltip("CharacterSelectButton 프리팹이 생성될 부모 Transform입니다.\n" +
                 "Vertical Layout Group을 추가하면 자동으로 정렬됩니다.")]
        [SerializeField] private Transform _characterSelectContainer;

        [Tooltip("캐릭터 선택 버튼 프리팹입니다.\nCharacterSelectButton.prefab을 연결합니다.")]
        [SerializeField] private CharacterSelectButton _selectButtonPrefab;

        [Tooltip("이 패널을 닫는 버튼입니다.")]
        [SerializeField] private Button _closeButton;

        [Header("개별 추리 UI 참조")]
        [Tooltip("캐릭터 선택 시 열릴 개별 프로파일 추리 UI입니다.\n" +
                 "_CampaignSystem/ProfileInquiryUI를 연결합니다.")]
        [SerializeField] private ProfileInquiryUI _profileInquiryUI;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        // 생성된 캐릭터 선택 버튼 목록입니다.
        // 추리 완료 후 버튼 상태를 갱신할 때 사용됩니다.
        private readonly List<CharacterSelectButton> _selectButtons = new();

        /// <summary>현재 패널이 열려있는지 여부입니다.</summary>
        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            _closeButton?.onClick.AddListener(Hide);
        }

        private void Start()
        {
            // 컨셉 카드 해금 이벤트를 구독합니다.
            // 추리 완료 → FragmentCollector.UnlockConceptCard() → 이벤트 발생 → 버튼 갱신 순서
            if (_fragmentCollector != null)
                _fragmentCollector.OnConceptCardUnlockable += OnConceptCardUnlocked;
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(Hide);

            if (_fragmentCollector != null)
                _fragmentCollector.OnConceptCardUnlockable -= OnConceptCardUnlocked;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 인물 추리 전체 UI를 엽니다.
        /// HoldToEnterFinalDecision에서 Phase2 확인 클릭 시 호출합니다.
        ///
        /// Show() 호출 시마다 버튼을 새로 생성합니다.
        /// (수집 현황이 달라졌을 수 있으므로 항상 최신 상태로 갱신)
        /// </summary>
        public void Show()
        {
            if (_profileData == null)
            {
                Debug.LogError("[ProfileInquiryAllUI] ProfileDataSO가 연결되지 않았습니다.");
                return;
            }

            BuildCharacterButtons();

            if (_panel != null) _panel.SetActive(true);
            IsOpen = true;

            Debug.Log("[ProfileInquiryAllUI] 인물 추리 전체 UI 열림");
        }

        /// <summary>인물 추리 전체 UI를 닫습니다.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            IsOpen = false;
        }

        /// <summary>
        /// 특정 캐릭터의 버튼을 완료 상태로 갱신합니다.
        /// FragmentCollector.OnConceptCardUnlockable 이벤트 수신 시 호출됩니다.
        /// </summary>
        public void RefreshCharacterButton(int characterId)
        {
            foreach (var btn in _selectButtons)
            {
                if (btn == null || btn.CharacterId != characterId) continue;
                bool completed = _fragmentCollector?.IsConceptCardUnlocked(characterId) ?? false;
                btn.SetCompleted(completed);
                break;
            }
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>
        /// 기존 버튼을 제거하고 ProfileDataSO의 캐릭터 목록 기준으로 버튼을 새로 생성합니다.
        /// 각 버튼에 조각 수, 잠금 여부, 완료 여부, 이름을 표시합니다.
        /// </summary>
        private void BuildCharacterButtons()
        {
            // 기존 버튼 제거
            foreach (var btn in _selectButtons)
                if (btn != null) Destroy(btn.gameObject);
            _selectButtons.Clear();

            if (_selectButtonPrefab == null || _characterSelectContainer == null) return;

            foreach (var profile in _profileData.CharacterProfiles)
            {
                if (profile == null) continue;

                int charId = profile.CharacterId;
                int fragmentCount = _fragmentCollector?.GetFragmentCount(charId) ?? 0;
                bool isUnlocked = fragmentCount >= profile.RequiredFragmentCount;
                bool isCompleted = _fragmentCollector?.IsConceptCardUnlocked(charId) ?? false;
                string name = _characterRecordBook?.GetCollectedName(charId);

                var btn = Instantiate(_selectButtonPrefab, _characterSelectContainer);
                btn.Setup(
                    charId,
                    name,
                    fragmentCount,
                    profile.RequiredFragmentCount,
                    isUnlocked,
                    isCompleted,
                    // charId를 클로저로 캡처해 각 버튼이 올바른 ID로 Show()를 호출하도록 합니다.
                    onClicked: () => OnCharacterSelected(charId));

                _selectButtons.Add(btn);
            }
        }

        private void OnCharacterSelected(int characterId)
        {
            if (_profileInquiryUI == null)
            {
                Debug.LogWarning("[ProfileInquiryAllUI] ProfileInquiryUI가 연결되지 않았습니다.");
                return;
            }

            Debug.Log($"[ProfileInquiryAllUI] 캐릭터 선택 — #{characterId}");
            _profileInquiryUI.Show(characterId);
        }

        private void OnConceptCardUnlocked(int characterId)
        {
            RefreshCharacterButton(characterId);
        }
    }
}