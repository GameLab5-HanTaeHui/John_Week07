using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 로비에서 최종 대화 결과를 열람하는 갤러리 UI입니다.
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   LobbyUI의 보상 열람 버튼 클릭 시 열립니다.
    ///   캐릭터별 최종 대화 완료 여부 / 성공·실패를 표시합니다.
    ///   캐릭터 선택 → 선택한 선택지 텍스트와 성공/실패 결과를 표시합니다.
    ///   미완료 캐릭터는 잠금 상태로 표시합니다.
    ///
    /// ─── ProfileDataSO 구조 변경 반영 ────────────────────────────────────
    ///   ConceptCard / EpilogueLines 제거.
    ///   최종 대화(FinalTalk) 데이터 기반으로 재설계.
    ///
    /// ─── Canvas 구조 ─────────────────────────────────────────────────────
    ///   RewardGalleryUI
    ///   ├── Panel
    ///   │   ├── CharacterListPanel
    ///   │   │   ├── TitleText
    ///   │   │   ├── CharacterButtonContainer
    ///   │   │   │   └── RewardCharacterButton (x6)
    ///   │   │   └── CloseButton
    ///   │   └── RewardDetailPanel
    ///   │       ├── CharacterNameText
    ///   │       ├── StatusText          ← "성공" / "실패" / "미완료"
    ///   │       ├── SelectedChoiceLabel ← "선택한 말:"
    ///   │       ├── SelectedChoiceText  ← 선택지 텍스트
    ///   │       ├── LockedOverlay       ← 미완료 시 표시
    ///   │       └── BackButton
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data             → ProfileDataSO 에셋
    ///   Character Button Prefab  → RewardCharacterButton 프리팹
    /// </summary>
    [DisallowMultipleComponent]
    public class RewardGalleryUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("캐릭터 프로파일 데이터입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("UI 루트")]
        [SerializeField] private GameObject _panel;

        [Header("캐릭터 목록 패널")]
        [SerializeField] private GameObject _characterListPanel;
        [SerializeField] private Transform _characterButtonContainer;
        [SerializeField] private RewardCharacterButton _characterButtonPrefab;
        [SerializeField] private Button _closeButton;

        [Header("보상 상세 패널")]
        [SerializeField] private GameObject _rewardDetailPanel;

        [Tooltip("캐릭터 이름 TMP입니다.")]
        [SerializeField] private TMP_Text _characterNameText;

        [Tooltip("성공 / 실패 / 미완료 상태 TMP입니다.")]
        [SerializeField] private TMP_Text _statusText;

        [Tooltip("선택한 선택지 텍스트 TMP입니다.")]
        [SerializeField] private TMP_Text _selectedChoiceText;

        [Tooltip("미완료 캐릭터 오버레이 GameObject입니다.")]
        [SerializeField] private GameObject _lockedOverlay;

        [Header("뒤로가기")]
        [SerializeField] private Button _backButton;

        [Header("상태 표시 문구")]
        [SerializeField] private string _textSuccess = "✔ 성공";
        [SerializeField] private string _textFailure = "✘ 실패";
        [SerializeField] private string _textIncomplete = "미완료";
        [SerializeField] private string _textNoChoice = "—";

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

        /// <summary>보상 갤러리를 엽니다.</summary>
        public void Show()
        {
            if (_profileData == null)
            {
                Debug.LogWarning("[RewardGalleryUI] ProfileDataSO 미연결");
                return;
            }

            BuildCharacterButtons();
            ShowCharacterList();

            _panel?.SetActive(true);
            IsOpen = true;

            Debug.Log("[RewardGalleryUI] 열림");
        }

        /// <summary>보상 갤러리를 닫습니다.</summary>
        public void Hide()
        {
            _panel?.SetActive(false);
            IsOpen = false;
        }

        // ── Private — 캐릭터 목록 ─────────────────────────────────────────

        private void BuildCharacterButtons()
        {
            foreach (var btn in _characterButtons)
                if (btn != null) Destroy(btn.gameObject);
            _characterButtons.Clear();

            if (_characterButtonPrefab == null || _characterButtonContainer == null) return;

            var saveData = CampaignSaveManager.Instance?.CurrentSave;

            foreach (var profile in _profileData.CharacterProfiles)
            {
                if (profile == null) continue;

                int charId = profile.CharacterId;

                // 수집된 이름 우선 → ProfileDataSO.CharacterFullName
                string name = CharacterRecordPanelManager.Instance?.GetCollectedName(charId);
                if (string.IsNullOrEmpty(name)) name = profile.CharacterFullName;

                var record = saveData?.finalTalkRecords.Find(r => r.characterId == charId);
                bool completed = record?.completed ?? false;
                bool success = record?.success ?? false;

                var btn = Instantiate(_characterButtonPrefab, _characterButtonContainer);
                btn.Setup(charId, name, completed, success,
                          onClicked: () => ShowRewardDetail(charId));

                _characterButtons.Add(btn);
            }
        }

        private void ShowCharacterList()
        {
            _characterListPanel?.SetActive(true);
            _rewardDetailPanel?.SetActive(false);
        }

        // ── Private — 보상 상세 ───────────────────────────────────────────

        private void ShowRewardDetail(int characterId)
        {
            var profile = _profileData.FindProfile(characterId);
            if (profile == null) return;

            var saveData = CampaignSaveManager.Instance?.CurrentSave;
            var record = saveData?.finalTalkRecords.Find(r => r.characterId == characterId);

            bool completed = record?.completed ?? false;
            bool success = record?.success ?? false;

            // 이름
            if (_characterNameText != null)
            {
                string name = CharacterRecordPanelManager.Instance?.GetCollectedName(characterId);
                if (string.IsNullOrEmpty(name)) name = profile.CharacterFullName;
                _characterNameText.text = !string.IsNullOrEmpty(name) ? name : $"#{characterId}";
            }

            // 잠금 오버레이
            _lockedOverlay?.SetActive(!completed);

            // 상태 문구
            if (_statusText != null)
            {
                _statusText.text = !completed ? _textIncomplete
                                 : success ? _textSuccess
                                              : _textFailure;
            }

            // 선택한 선택지 텍스트
            if (_selectedChoiceText != null)
            {
                string choiceText = _textNoChoice;
                if (completed && record != null && profile.FinalTalk?.Choices != null)
                {
                    int idx = record.selectedChoiceIndex;
                    if (idx >= 0 && idx < profile.FinalTalk.Choices.Count)
                        choiceText = profile.FinalTalk.Choices[idx];
                }
                _selectedChoiceText.text = choiceText;
            }

            _characterListPanel?.SetActive(false);
            _rewardDetailPanel?.SetActive(true);

            Debug.Log($"[RewardGalleryUI] 상세 열람 — #{characterId} completed={completed} success={success}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RewardCharacterButton
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// RewardGalleryUI의 캐릭터 선택 버튼 1개입니다.
    ///
    /// ─── 상태 표시 ───────────────────────────────────────────────────────
    ///   미완료 : 잠금 아이콘 표시
    ///   성공   : 성공 아이콘 표시
    ///   실패   : 실패 아이콘 표시
    /// </summary>
    [DisallowMultipleComponent]
    public class RewardCharacterButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text _characterNameText;
        [SerializeField] private GameObject _lockedIcon;
        [SerializeField] private GameObject _successIcon;
        [SerializeField] private GameObject _failureIcon;
        [SerializeField] private Button _button;

        public int CharacterId { get; private set; }

        private System.Action _onClicked;

        public void Setup(
            int characterId,
            string name,
            bool completed,
            bool success,
            System.Action onClicked)
        {
            CharacterId = characterId;
            _onClicked = onClicked;

            if (_characterNameText != null)
                _characterNameText.text = string.IsNullOrEmpty(name) ? $"#{characterId}" : name;

            if (_lockedIcon != null) _lockedIcon.SetActive(!completed);
            if (_successIcon != null) _successIcon.SetActive(completed && success);
            if (_failureIcon != null) _failureIcon.SetActive(completed && !success);

            _button?.onClick.AddListener(() => _onClicked?.Invoke());
        }

        private void OnDestroy()
        {
            _button?.onClick.RemoveAllListeners();
        }
    }
}