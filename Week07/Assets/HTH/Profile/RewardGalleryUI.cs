using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 로비에서 해금된 보상을 열람하는 갤러리 UI입니다.
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   LobbyUI의 보상 열람 버튼 클릭 시 열립니다.
    ///   캐릭터별 보상 해금 여부를 표시합니다.
    ///   캐릭터 선택 → 해금된 컨셉 카드 / 시점 완결문 표시
    ///   미해금 항목은 잠금 상태로 표시합니다.
    ///
    /// ─── 보상 단계 ───────────────────────────────────────────────────────
    ///   1단계: 컨셉 카드 (프로파일 일부 정답 시 해금)
    ///       캐치프레이즈, 외형, 서사적 배경, 성격, 기믹 연관성
    ///   2단계: 시점 완결문 (프로파일 전부 정답 시 해금)
    ///       캐릭터 시점의 마지막 독백
    ///
    /// ─── Canvas 구조 ─────────────────────────────────────────────────────
    ///   RewardGalleryUI
    ///   ├── Panel
    ///   │   ├── CharacterListPanel          ← 캐릭터 목록
    ///   │   │   ├── TitleText
    ///   │   │   ├── CharacterButtonContainer
    ///   │   │   │   └── RewardCharacterButton (x7)
    ///   │   │   └── CloseButton
    ///   │   └── RewardDetailPanel           ← 선택된 캐릭터 보상 상세
    ///       ├── CharacterIdText
    ///       ├── CharacterNameText
    ///       ├── ConceptCardSection
    ///       │   ├── ConceptCardLockedOverlay
    ///       │   ├── CatchphraseText
    ///       │   ├── AppearanceText
    ///       │   ├── NarrativeText
    ///       │   ├── PersonalityText
    ///       │   ├── GimmickText
    ///       │   └── CardIllustration
    ///       ├── EpilogueSection
    ///       │   ├── EpilogueLockedOverlay
    ///       │   └── EpilogueText
    ///       └── BackButton
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   ProfileData       → ProfileDataSO (보상 데이터)
    ///   RewardSaveData    → RewardSaveDataSO (해금 기록)
    ///   CharacterButtonPrefab → RewardCharacterButton 프리팹
    /// </summary>
    [DisallowMultipleComponent]
    public class RewardGalleryUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [SerializeField] private ProfileDataSO _profileData;
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Header("UI 루트")]
        [SerializeField] private GameObject _panel;

        [Header("캐릭터 목록 패널")]
        [SerializeField] private GameObject _characterListPanel;
        [SerializeField] private Transform _characterButtonContainer;
        [SerializeField] private RewardCharacterButton _characterButtonPrefab;
        [SerializeField] private Button _closeButton;

        [Header("보상 상세 패널")]
        [SerializeField] private GameObject _rewardDetailPanel;
        [SerializeField] private TMP_Text _characterIdText;
        [SerializeField] private TMP_Text _characterNameText;

        [Header("컨셉 카드 섹션")]
        [SerializeField] private GameObject _conceptCardSection;
        [SerializeField] private GameObject _conceptCardLockedOverlay;
        [SerializeField] private TMP_Text _catchphraseText;
        [SerializeField] private TMP_Text _appearanceText;
        [SerializeField] private TMP_Text _narrativeText;
        [SerializeField] private TMP_Text _personalityText;
        [SerializeField] private TMP_Text _gimmickText;
        [SerializeField] private Image _cardIllustration;

        [Header("시점 완결문 섹션")]
        [SerializeField] private GameObject _epilogueSection;
        [SerializeField] private GameObject _epilogueLockedOverlay;
        [SerializeField] private TMP_Text _epilogueText;

        [Header("뒤로가기 버튼")]
        [SerializeField] private Button _backButton;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private readonly List<RewardCharacterButton> _characterButtons = new();

        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_rewardDetailPanel != null) _rewardDetailPanel.SetActive(false);

            _closeButton?.onClick.AddListener(Hide);
            _backButton?.onClick.AddListener(ShowCharacterList);
        }

        private void OnDestroy()
        {
            _closeButton?.onClick.RemoveListener(Hide);
            _backButton?.onClick.RemoveListener(ShowCharacterList);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 보상 갤러리를 엽니다.
        /// LobbyUI의 보상 열람 버튼에서 호출합니다.
        /// </summary>
        public void Show()
        {
            if (_profileData == null)
            {
                Debug.LogWarning("[RewardGalleryUI] ProfileDataSO가 연결되지 않았습니다.");
                return;
            }

            _rewardSaveData?.Load();
            BuildCharacterButtons();
            ShowCharacterList();

            if (_panel != null) _panel.SetActive(true);
            IsOpen = true;

            Debug.Log("[RewardGalleryUI] 보상 갤러리 열림");
        }

        /// <summary>보상 갤러리를 닫습니다.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            IsOpen = false;
        }

        // ── Private — 캐릭터 목록 ─────────────────────────────────────────

        private void BuildCharacterButtons()
        {
            foreach (var btn in _characterButtons)
                if (btn != null) Destroy(btn.gameObject);
            _characterButtons.Clear();

            if (_characterButtonPrefab == null || _characterButtonContainer == null) return;

            foreach (var profile in _profileData.CharacterProfiles)
            {
                if (profile == null) continue;

                int charId = profile.CharacterId;
                bool conceptCardUnlocked = _rewardSaveData?.IsConceptCardUnlocked(charId) ?? false;
                bool epilogueUnlocked = _rewardSaveData?.IsEpilogueUnlocked(charId) ?? false;
                string name = _rewardSaveData?.GetCharacterName(charId);

                var btn = Instantiate(_characterButtonPrefab, _characterButtonContainer);
                btn.Setup(
                    charId,
                    name,
                    conceptCardUnlocked,
                    epilogueUnlocked,
                    onClicked: () => ShowRewardDetail(charId));

                _characterButtons.Add(btn);
            }
        }

        private void ShowCharacterList()
        {
            if (_characterListPanel != null) _characterListPanel.SetActive(true);
            if (_rewardDetailPanel != null) _rewardDetailPanel.SetActive(false);
        }

        // ── Private — 보상 상세 ───────────────────────────────────────────

        private void ShowRewardDetail(int characterId)
        {
            var profile = _profileData.FindProfile(characterId);
            if (profile == null) return;

            bool conceptCardUnlocked = _rewardSaveData?.IsConceptCardUnlocked(characterId) ?? false;
            bool epilogueUnlocked = _rewardSaveData?.IsEpilogueUnlocked(characterId) ?? false;
            string name = _rewardSaveData?.GetCharacterName(characterId);

            // 캐릭터 정보
            if (_characterIdText != null)
                _characterIdText.text = $"#{characterId}";
            if (_characterNameText != null)
                _characterNameText.text = string.IsNullOrEmpty(name) ? "???" : name;

            // 컨셉 카드 섹션
            RefreshConceptCard(profile.ConceptCard, conceptCardUnlocked);

            // 시점 완결문 섹션
            RefreshEpilogue(profile.EpilogueText, epilogueUnlocked);

            if (_characterListPanel != null) _characterListPanel.SetActive(false);
            if (_rewardDetailPanel != null) _rewardDetailPanel.SetActive(true);

            Debug.Log($"[RewardGalleryUI] 보상 상세 열람 — #{characterId}");
        }

        private void RefreshConceptCard(ConceptCardData card, bool isUnlocked)
        {
            if (_conceptCardSection == null) return;

            if (_conceptCardLockedOverlay != null)
                _conceptCardLockedOverlay.SetActive(!isUnlocked);

            if (!isUnlocked) return;

            if (card == null) return;

            if (_catchphraseText != null) _catchphraseText.text = card.Catchphrase;
            if (_appearanceText != null) _appearanceText.text = card.Appearance;
            if (_narrativeText != null) _narrativeText.text = card.NarrativeBackground;
            if (_personalityText != null) _personalityText.text = card.Personality;
            if (_gimmickText != null) _gimmickText.text = card.GimmickRelevance;

            if (_cardIllustration != null)
            {
                _cardIllustration.sprite = card.CardIllustration;
                _cardIllustration.enabled = card.CardIllustration != null;
            }
        }

        private void RefreshEpilogue(string epilogueContent, bool isUnlocked)
        {
            if (_epilogueSection == null) return;

            if (_epilogueLockedOverlay != null)
                _epilogueLockedOverlay.SetActive(!isUnlocked);

            if (_epilogueText != null)
                _epilogueText.text = isUnlocked ? epilogueContent : string.Empty;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RewardCharacterButton — 캐릭터 선택 버튼 1개
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// RewardGalleryUI의 캐릭터 선택 버튼 1개입니다.
    ///
    /// ─── 상태 표시 ───────────────────────────────────────────────────────
    ///   미해금       : 잠금 아이콘
    ///   컨셉 카드 해금: 1단계 아이콘
    ///   시점 완결문 해금: 2단계 아이콘 (전체 완료)
    /// </summary>
    [DisallowMultipleComponent]
    public class RewardCharacterButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text _characterIdText;
        [SerializeField] private TMP_Text _characterNameText;
        [SerializeField] private GameObject _lockedIcon;
        [SerializeField] private GameObject _conceptCardIcon;
        [SerializeField] private GameObject _epilogueIcon;
        [SerializeField] private Button _button;

        public int CharacterId { get; private set; }

        private System.Action _onClicked;

        public void Setup(int characterId,
                          string name,
                          bool conceptCardUnlocked,
                          bool epilogueUnlocked,
                          System.Action onClicked)
        {
            CharacterId = characterId;
            _onClicked = onClicked;

            if (_characterIdText != null)
                _characterIdText.text = $"#{characterId}";

            if (_characterNameText != null)
                _characterNameText.text = string.IsNullOrEmpty(name) ? "???" : name;

            bool anyUnlocked = conceptCardUnlocked || epilogueUnlocked;

            if (_lockedIcon != null) _lockedIcon.SetActive(!anyUnlocked);
            if (_conceptCardIcon != null) _conceptCardIcon.SetActive(conceptCardUnlocked);
            if (_epilogueIcon != null) _epilogueIcon.SetActive(epilogueUnlocked);

            if (_button != null)
                _button.onClick.AddListener(() => _onClicked?.Invoke());
        }

        private void OnDestroy()
        {
            _button?.onClick.RemoveAllListeners();
        }
    }
}