using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 개별 캐릭터의 프로파일 추리 UI입니다.
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   특정 캐릭터의 4개 프로파일 항목을 표시하고,
    ///   플레이어가 각 항목의 선택지를 골라 제출하면 정답을 판정합니다.
    ///   결과에 따라 컨셉 카드와 시점 완결문을 표시하고 해금 처리합니다.
    ///
    /// ─── 열리는 조건 ─────────────────────────────────────────────────────
    ///   CharacterRecordBook의 "추리하기" 버튼 클릭 (기록장에서 직접)
    ///   또는 ProfileInquiryAllUI에서 캐릭터 선택 시
    ///   → Show(characterId) 호출
    ///   → FragmentCollector.GetFragmentCount(characterId) >= RequiredFragmentCount 확인
    ///   → 조건 충족 시 패널 활성화
    ///   → 조건 미충족 시 _insufficientFragmentText로 피드백 표시
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   Show(characterId) 호출
    ///   → 대화 조각 수 체크
    ///       부족 → "대화 조각이 부족합니다. (1/3)" 텍스트 2초 표시 후 자동 숨김
    ///       충족 → 패널 활성화
    ///   → 캐릭터 정보 표시 (#번호, 이름, 조각 수)
    ///   → ProfileItemView 프리팹을 동적 생성 (각 프로파일 항목)
    ///   → 각 ProfileItemView에 질문과 선택지 버튼 설정
    ///   → 모든 항목이 선택되면 제출 버튼 활성화
    ///   → 제출 버튼 클릭 → 정답 판정
    ///   → 결과 패널 표시 (컨셉 카드, 시점 완결문)
    ///   → FragmentCollector에 해금 기록 요청
    ///   → 결과 닫기 버튼 클릭 → 패널 닫힘
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///   Canvas/ProfileInquiryPanel을 Inspector에서 Panel 필드에 연결합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data               → ProfileDataSO 에셋
    ///   Fragment Collector         → _CampaignSystem/FragmentCollector
    ///   Character Record Book      → _CampaignSystem/CharacterRecordBook
    ///   Panel                      → Canvas/ProfileInquiryPanel
    ///   Profile Item Container     → ProfileInquiryPanel 하위 빈 GameObject
    ///   Profile Item View Prefab   → ProfileItemView.prefab
    ///   Submit Button              → 제출 Button
    ///   Close Button               → 닫기 Button
    ///   Result Panel               → 결과 패널 (기본 비활성)
    ///   Result Close Button        → 결과 패널 닫기 Button
    ///   Insufficient Fragment Text → 조각 부족 피드백 TMP_Text (기본 비활성)
    ///   Feedback Duration          → 피드백 표시 시간(초, 기본값 2)
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileInquiryUI : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("캐릭터별 프로파일 항목, 선택지, 정답 데이터입니다.\n" +
                 "Project → Create → HTH → Campaign → ProfileData로 생성합니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Tooltip("대화 조각 수집 관리 컴포넌트입니다.\n" +
                 "조각 수 체크와 보상 해금에 사용됩니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Tooltip("인물 기록장 컴포넌트입니다.\n" +
                 "수집된 캐릭터 이름을 가져와 표시할 때 사용됩니다.")]
        [SerializeField] private CharacterRecordBook _characterRecordBook;

        [Header("UI 루트")]
        [Tooltip("프로파일 추리 전체 패널입니다.\nCanvas/ProfileInquiryPanel을 연결합니다.")]
        [SerializeField] private GameObject _panel;

        [Header("캐릭터 정보")]
        [Tooltip("'#1', '#2' 등 캐릭터 번호를 표시합니다.")]
        [SerializeField] private TMP_Text _characterIdText;

        [Tooltip("캐릭터 이름을 표시합니다. 미수집 시 '???'로 표시됩니다.")]
        [SerializeField] private TMP_Text _characterNameText;

        [Tooltip("'대화 조각 3/3' 형식으로 수집 현황을 표시합니다.")]
        [SerializeField] private TMP_Text _fragmentCountText;

        [Header("프로파일 항목")]
        [Tooltip("ProfileItemView 프리팹이 생성될 부모 Transform입니다.\n" +
                 "Vertical Layout Group 컴포넌트를 추가하면 자동으로 정렬됩니다.")]
        [SerializeField] private Transform _profileItemContainer;

        [Tooltip("프로파일 항목 1개의 UI 프리팹입니다.\n" +
                 "ProfileItemView.prefab을 연결합니다.")]
        [SerializeField] private ProfileItemView _profileItemViewPrefab;

        [Header("버튼")]
        [Tooltip("모든 항목 선택 완료 시 활성화됩니다. 클릭 시 정답 판정이 시작됩니다.")]
        [SerializeField] private Button _submitButton;

        [Tooltip("패널을 닫습니다. 선택을 저장하지 않고 닫힙니다.")]
        [SerializeField] private Button _closeButton;

        [Header("결과 패널")]
        [Tooltip("제출 후 결과를 표시하는 패널입니다. 기본적으로 비활성화되어 있습니다.")]
        [SerializeField] private GameObject _resultPanel;

        [Tooltip("'3/4 정답' 또는 '전부 정답!' 형식으로 결과를 표시합니다.")]
        [SerializeField] private TMP_Text _resultText;

        [Tooltip("컨셉 카드 내용을 표시하는 패널입니다. 일부 정답 이상 시 활성화됩니다.")]
        [SerializeField] private GameObject _conceptCardPanel;

        [Tooltip("컨셉 카드 내용(캐치프레이즈, 배경, 성격)을 표시합니다.")]
        [SerializeField] private TMP_Text _conceptCardText;

        [Tooltip("시점 완결문을 표시하는 패널입니다. 전부 정답 시에만 활성화됩니다.")]
        [SerializeField] private GameObject _epiloguePanel;

        [Tooltip("캐릭터의 시점 독백 텍스트를 표시합니다.")]
        [SerializeField] private TMP_Text _epilogueText;

        [Tooltip("결과 패널을 닫고 프로파일 추리 패널 전체를 닫습니다.")]
        [SerializeField] private Button _resultCloseButton;

        [Header("조각 부족 피드백")]
        [Tooltip("대화 조각이 부족할 때 표시할 TMP_Text입니다.\n" +
                 "ProfileInquiryPanel 하위에 배치하고 기본 비활성화 상태로 둡니다.\n" +
                 "예: '대화 조각이 부족합니다. (1/3)'\n" +
                 "패널이 닫힌 상태에서도 표시되므로 Canvas 직속에 배치해도 됩니다.")]
        [SerializeField] private TMP_Text _insufficientFragmentText;

        [Tooltip("조각 부족 피드백 텍스트가 표시되는 시간(초)입니다.")]
        [SerializeField] private float _feedbackDuration = 2f;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CharacterProfileData _currentProfile;
        private int _currentCharacterId;
        private List<ProfileItemView> _itemViews = new();
        private Coroutine _feedbackCoroutine;

        /// <summary>현재 패널이 열려있는지 여부입니다.</summary>
        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_resultPanel != null) _resultPanel.SetActive(false);
            if (_insufficientFragmentText != null) _insufficientFragmentText.gameObject.SetActive(false);

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
        /// 특정 캐릭터의 프로파일 추리 패널을 엽니다.
        /// 대화 조각이 RequiredFragmentCount 미만이면 피드백 텍스트를 표시하고 열리지 않습니다.
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

            int fragmentCount = _fragmentCollector != null
                ? _fragmentCollector.GetFragmentCount(characterId)
                : 0;

            // 조각이 부족하면 피드백을 표시하고 패널을 열지 않습니다.
            if (fragmentCount < profile.RequiredFragmentCount)
            {
                Debug.Log($"[ProfileInquiryUI] 조각 부족 — {fragmentCount}/{profile.RequiredFragmentCount}");
                ShowInsufficientFeedback(fragmentCount, profile.RequiredFragmentCount);
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

        /// <summary>프로파일 추리 패널을 닫습니다.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            IsOpen = false;
            ClearProfileItems();
        }

        /// <summary>ProfileItemView에서 선택지가 변경됐을 때 호출됩니다.</summary>
        public void OnItemSelectionChanged()
        {
            RefreshSubmitButton();
        }

        // ── Private — UI 구성 ─────────────────────────────────────────────

        private void RefreshCharacterInfo(int characterId, int fragmentCount, int requiredCount)
        {
            if (_characterIdText != null)
                _characterIdText.text = $"#{characterId}";

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

        // ── Private — 제출 및 판정 ────────────────────────────────────────

        private void OnSubmitClicked()
        {
            if (_currentProfile == null) return;

            var answers = new int[_currentProfile.ProfileItems.Count];
            for (int i = 0; i < _itemViews.Count; i++)
            {
                answers[i] = (_itemViews[i] == null || _itemViews[i].IsLocked)
                    ? -1
                    : _itemViews[i].SelectedIndex;
            }

            int correctCount = _currentProfile.CountCorrect(answers);
            bool allCorrect = _currentProfile.IsAllCorrect(answers);
            int total = _currentProfile.ProfileItems.Count;

            Debug.Log($"[ProfileInquiryUI] 제출 — {correctCount}/{_currentProfile.ProfileItems.Count} 정답");

            // 프로파일 추리 제출 로그
            GameLogger.Instance?.LogEvent("profile_submitted",
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "character_id",   _currentCharacterId },
                    { "correct_count",  correctCount },
                    { "total_count",    total },
                    { "all_correct",    allCorrect },
                    { "concept_card",   correctCount > 0 },
                    { "epilogue",       allCorrect },
                });

            ShowResult(correctCount, allCorrect);
        }

        private void ShowResult(int correctCount, bool allCorrect)
        {
            if (_resultPanel == null) return;

            int total = _currentProfile?.ProfileItems.Count ?? 4;

            if (_resultText != null)
                _resultText.text = allCorrect
                    ? $"전부 정답! ({correctCount}/{total})\n모든 보상이 해금됩니다."
                    : $"{correctCount}/{total} 정답\n일부 보상이 해금됩니다.";

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

            if (_epiloguePanel != null)
            {
                _epiloguePanel.SetActive(allCorrect);
                if (allCorrect && _epilogueText != null)
                    _epilogueText.text = _currentProfile?.EpilogueText ?? string.Empty;
            }

            _resultPanel.SetActive(true);

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

        // ── Private — 조각 부족 피드백 ───────────────────────────────────

        /// <summary>
        /// 조각 부족 피드백 텍스트를 일정 시간 표시 후 숨깁니다.
        /// Show()에서 조각 수가 RequiredFragmentCount 미만일 때 호출됩니다.
        /// 패널이 열리지 않은 상태에서도 텍스트만 표시됩니다.
        /// </summary>
        private void ShowInsufficientFeedback(int current, int required)
        {
            if (_insufficientFragmentText == null) return;

            if (_feedbackCoroutine != null)
            {
                StopCoroutine(_feedbackCoroutine);
                _feedbackCoroutine = null;
            }

            _feedbackCoroutine = StartCoroutine(FeedbackCoroutine(current, required));
        }

        private IEnumerator FeedbackCoroutine(int current, int required)
        {
            _insufficientFragmentText.text = $"대화 조각이 부족합니다. ({current}/{required})";
            _insufficientFragmentText.gameObject.SetActive(true);

            yield return new WaitForSeconds(_feedbackDuration);

            _insufficientFragmentText.gameObject.SetActive(false);
            _feedbackCoroutine = null;
        }
    }
}