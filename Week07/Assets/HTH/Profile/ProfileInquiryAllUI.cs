using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// Phase2 인물 추리 전체 UI입니다.
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   HoldToEnterFinalDecision에서 Phase2 확인 시 열립니다.
    ///   수집된 조각 수가 충족된 캐릭터 목록을 표시합니다.
    ///   플레이어가 캐릭터를 선택해 개별 ProfileInquiryUI를 엽니다.
    ///   모든 캐릭터 추리 완료 시 결과를 집계합니다.
    ///
    /// ─── Canvas 구조 ─────────────────────────────────────────────────────
    ///   ProfileInquiryAllUI
    ///   ├── Panel
    ///   │   ├── TitleText
    ///   │   ├── CharacterSelectContainer
    ///   │   │   └── CharacterSelectButton (x7)
    ///   │   │       ├── CharacterIdText   (#1)
    ///   │   │       ├── CharacterNameText (??? or 이름)
    ///   │   │       ├── FragmentCountText (3/3)
    ///   │   │       ├── StatusIcon        (미완료/완료)
    ///   │   │       └── LockedIcon        (조각 부족)
    ///   │   └── CloseButton
    ///   └── ProfileInquiryUI (개별 추리)
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileInquiryAllUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [SerializeField] private ProfileDataSO _profileData;
        [SerializeField] private FragmentCollector _fragmentCollector;
        [SerializeField] private CharacterRecordBook _characterRecordBook;

        [Header("UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _characterSelectContainer;
        [SerializeField] private CharacterSelectButton _selectButtonPrefab;
        [SerializeField] private Button _closeButton;

        [Header("개별 추리 UI")]
        [SerializeField] private ProfileInquiryUI _profileInquiryUI;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private readonly List<CharacterSelectButton> _selectButtons = new();
        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            _closeButton?.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(Hide);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 인물 추리 전체 UI를 엽니다.
        /// HoldToEnterFinalDecision에서 Phase2 확인 시 호출합니다.
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
        /// 개별 추리 완료 후 버튼 상태를 갱신합니다.
        /// ProfileInquiryUI에서 제출 완료 후 호출합니다.
        /// </summary>
        public void RefreshCharacterButton(int characterId)
        {
            foreach (var btn in _selectButtons)
            {
                if (btn != null && btn.CharacterId == characterId)
                {
                    bool completed = _fragmentCollector?.IsConceptCardUnlocked(characterId) ?? false;
                    btn.SetCompleted(completed);
                    break;
                }
            }
        }

        // ── Private ──────────────────────────────────────────────────────

        private void BuildCharacterButtons()
        {
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
                    onClicked: () => OnCharacterSelected(charId));

                _selectButtons.Add(btn);
            }
        }

        private void OnCharacterSelected(int characterId)
        {
            if (_profileInquiryUI == null) return;
            _profileInquiryUI.Show(characterId);
        }
    }
}