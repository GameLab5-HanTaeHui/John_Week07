using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// Phase2 프로파일 추리 UI입니다.
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   플레이어가 특정 캐릭터의 프로파일 추리 버튼 클릭
    ///   → 대화 조각 수 체크 (RequiredFragmentCount 이상인지)
    ///   → 조건 충족 시 Show(characterId) 호출
    ///   → 4개의 프로파일 항목 표시
    ///   → 각 항목마다 선택지 버튼 표시
    ///   → 플레이어가 각 항목의 선택지 선택
    ///   → 제출 버튼 클릭
    ///   → 정답 판정
    ///       일부 정답 → 컨셉 카드 해금
    ///       전부 정답 → 컨셉 카드 + 시점 완결문 해금
    ///   → 결과 표시 후 닫기
    ///
    /// ─── Canvas 구조 ─────────────────────────────────────────────────────
    ///   ProfileInquiryPanel
    ///   ├── CharacterIdText        ← #1, #2 등 번호 표시
    ///   ├── CharacterNameText      ← 수집된 이름 표시 (미수집 시 ???)
    ///   ├── FragmentCountText      ← 수집된 조각 수 표시
    ///   ├── ProfileItemContainer   ← ProfileItemView 4개 배치
    ///   ├── SubmitButton           ← 제출 버튼
    ///   ├── CloseButton            ← 닫기 버튼
    ///   └── ResultPanel            ← 결과 표시 패널
    ///       ├── ResultText
    ///       ├── ConceptCardPanel
    ///       └── EpiloguePanel
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   ProfileData       → 이 스테이지의 ProfileDataSO
    ///   FragmentCollector → 대화 조각 수집 관리자
    ///   ProfileItemViewPrefab → 프로파일 항목 UI 프리팹
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileInquiryUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [SerializeField] private ProfileDataSO _profileData;
        [SerializeField] private FragmentCollector _fragmentCollector;
        [SerializeField] private CharacterRecordBook _characterRecordBook;

        [Header("UI 루트")]
        [SerializeField] private GameObject _panel;

        [Header("캐릭터 정보")]
        [SerializeField] private TMP_Text _characterIdText;
        [SerializeField] private TMP_Text _characterNameText;
        [SerializeField] private TMP_Text _fragmentCountText;

        [Header("프로파일 항목")]
        [SerializeField] private Transform _profileItemContainer;
        [SerializeField] private ProfileItemView _profileItemViewPrefab;

        [Header("버튼")]
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _closeButton;

        [Header("결과 패널")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private GameObject _conceptCardPanel;
        [SerializeField] private TMP_Text _conceptCardText;
        [SerializeField] private GameObject _epiloguePanel;
        [SerializeField] private TMP_Text _epilogueText;
        [SerializeField] private Button _resultCloseButton;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CharacterProfileData _currentProfile;
        private int _currentCharacterId;
        private List<ProfileItemView> _itemViews = new();

        /// <summary>현재 패널이 열려있는지 여부입니다.</summary>
        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_resultPanel != null) _resultPanel.SetActive(false);

            _submitButton?.onClick.AddListener(OnSubmitClicked);
            _closeButton?.onClick.AddListener(Hide);
            _resultCloseButton?.onClick.AddListener(HideResult);
        }

        private void OnDestroy()
        {
            _submitButton?.onClick.RemoveListener(OnSubmitClicked);
            _closeButton?.onClick.RemoveListener(Hide);
            _resultCloseButton?.onClick.RemoveListener(HideResult);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 특정 캐릭터의 프로파일 추리 UI를 표시합니다.
        /// 대화 조각이 부족하면 열리지 않습니다.
        /// CharacterRecordBook 또는 별도 버튼에서 호출합니다.
        /// </summary>
        public void Show(int characterId)
        {
            if (_profileData == null)
            {
                Debug.LogError("[ProfileInquiryUI] ProfileDataSO가 연결되지 않았습니다.");
                return;
            }

            var profile = _profileData.FindProfile(characterId);
            if (profile == null)
            {
                Debug.LogWarning($"[ProfileInquiryUI] CharacterId={characterId}의 프로파일 데이터가 없습니다.");
                return;
            }

            // 대화 조각 수 체크
            int fragmentCount = _fragmentCollector != null
                ? _fragmentCollector.GetFragmentCount(characterId)
                : 0;

            if (fragmentCount < profile.RequiredFragmentCount)
            {
                Debug.Log($"[ProfileInquiryUI] 조각 부족 — {fragmentCount}/{profile.RequiredFragmentCount}");
                // TODO: 조각 부족 피드백 UI 표시
                return;
            }

            _currentCharacterId = characterId;
            _currentProfile = profile;

            RefreshCharacterInfo(characterId, fragmentCount, profile.RequiredFragmentCount);
            BuildProfileItems(profile);

            if (_resultPanel != null) _resultPanel.SetActive(false);
            if (_panel != null) _panel.SetActive(true);
            IsOpen = true;

            Debug.Log($"[ProfileInquiryUI] 프로파일 추리 열림 — CharacterId={characterId}");
        }

        /// <summary>프로파일 추리 UI를 닫습니다.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            IsOpen = false;
            ClearProfileItems();
        }

        // ── Private — UI 구성 ─────────────────────────────────────────────

        private void RefreshCharacterInfo(int characterId,
                                          int fragmentCount,
                                          int requiredCount)
        {
            if (_characterIdText != null)
                _characterIdText.text = $"#{characterId}";

            // 수집된 이름 표시 (미수집 시 ???)
            if (_characterNameText != null)
            {
                string name = _characterRecordBook?.GetCollectedName(characterId);
                _characterNameText.text = string.IsNullOrEmpty(name) ? "???" : name;
            }

            if (_fragmentCountText != null)
                _fragmentCountText.text = $"대화 조각 {fragmentCount}/{requiredCount}";
        }

        private void BuildProfileItems(CharacterProfileData profile)
        {
            ClearProfileItems();

            if (_profileItemViewPrefab == null || _profileItemContainer == null) return;

            for (int i = 0; i < profile.ProfileItems.Count; i++)
            {
                var item = profile.ProfileItems[i];
                if (item == null) continue;

                // 해당 항목 추리 가능 여부 체크
                bool isLocked = !string.IsNullOrEmpty(item.RequiredFragmentId)
                    && (_fragmentCollector == null
                        || !_fragmentCollector.HasFragment(item.RequiredFragmentId));

                var view = Instantiate(_profileItemViewPrefab, _profileItemContainer);
                view.Setup(i, item, isLocked);
                _itemViews.Add(view);
            }

            RefreshSubmitButton();
        }

        private void ClearProfileItems()
        {
            foreach (var view in _itemViews)
                if (view != null) Destroy(view.gameObject);
            _itemViews.Clear();
        }

        private void RefreshSubmitButton()
        {
            if (_submitButton == null) return;

            // 모든 항목에 선택이 완료됐는지 체크
            bool allSelected = true;
            foreach (var view in _itemViews)
            {
                if (view == null || view.IsLocked) continue;
                if (!view.HasSelection)
                {
                    allSelected = false;
                    break;
                }
            }
            _submitButton.interactable = allSelected;
        }

        // ── Private — 제출 판정 ───────────────────────────────────────────

        private void OnSubmitClicked()
        {
            if (_currentProfile == null) return;

            // 선택된 답안 수집
            var answers = new int[_currentProfile.ProfileItems.Count];
            for (int i = 0; i < _itemViews.Count; i++)
            {
                if (_itemViews[i] == null || _itemViews[i].IsLocked)
                    answers[i] = -1;
                else
                    answers[i] = _itemViews[i].SelectedIndex;
            }

            int correctCount = _currentProfile.CountCorrect(answers);
            bool allCorrect = _currentProfile.IsAllCorrect(answers);

            Debug.Log($"[ProfileInquiryUI] 제출 — {correctCount}/{_currentProfile.ProfileItems.Count} 정답");

            ShowResult(correctCount, allCorrect);
        }

        private void ShowResult(int correctCount, bool allCorrect)
        {
            if (_resultPanel == null) return;

            int total = _currentProfile?.ProfileItems.Count ?? 4;

            // 결과 텍스트
            if (_resultText != null)
                _resultText.text = allCorrect
                    ? $"전부 정답! ({correctCount}/{total})\n모든 보상이 해금됩니다."
                    : $"{correctCount}/{total} 정답\n일부 보상이 해금됩니다.";

            // 컨셉 카드 해금 (일부 정답 이상)
            bool conceptCardUnlocked = correctCount > 0;
            if (_conceptCardPanel != null)
            {
                _conceptCardPanel.SetActive(conceptCardUnlocked);
                if (conceptCardUnlocked && _conceptCardText != null
                    && _currentProfile?.ConceptCard != null)
                {
                    var card = _currentProfile.ConceptCard;
                    _conceptCardText.text =
                        $"[{card.Catchphrase}]\n\n" +
                        $"{card.NarrativeBackground}\n\n" +
                        $"{card.Personality}";
                }
            }

            // 시점 완결문 해금 (전부 정답)
            if (_epiloguePanel != null)
            {
                _epiloguePanel.SetActive(allCorrect);
                if (allCorrect && _epilogueText != null)
                    _epilogueText.text = _currentProfile?.EpilogueText ?? string.Empty;
            }

            _resultPanel.SetActive(true);

            // FragmentCollector에 보상 해금 알림
            if (_fragmentCollector != null)
            {
                if (conceptCardUnlocked)
                    _fragmentCollector.UnlockConceptCard(_currentCharacterId);
                if (allCorrect)
                    _fragmentCollector.UnlockEpilogue(_currentCharacterId);
            }
        }

        private void HideResult()
        {
            if (_resultPanel != null) _resultPanel.SetActive(false);
            Hide();
        }

        // ── 외부에서 ProfileItemView 선택 변경 알림 ───────────────────────

        /// <summary>ProfileItemView에서 선택이 변경될 때 호출됩니다.</summary>
        public void OnItemSelectionChanged()
        {
            RefreshSubmitButton();
        }
    }
}