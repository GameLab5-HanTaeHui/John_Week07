using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 인물 기록장 우측 상세 정보 뷰입니다.
    /// 선택된 캐릭터의 정보를 탭에 따라 표시합니다.
    ///
    /// ─── 표시 내용 ───────────────────────────────────────────────────────
    ///   상단: "#1 $%& (주인공)" or "#1 엔비 (주인공)" (이름 수집 여부)
    ///
    ///   문장 조각 탭:
    ///     "수집한 문장 조각 (X/5)"
    ///     슬롯 1~N: 미수집=회색 힌트, 수집=검은 실제 대사
    ///
    ///   컨셉 카드 탭 (임시):
    ///     해금된 컨셉 카드 내용 표시 (추후 디자인 확정 후 교체)
    ///
    ///   시점 완결문 탭 (임시):
    ///     해금된 에필로그 텍스트 표시 (추후 디자인 확정 후 교체)
    ///
    /// ─── 씬 구조 ─────────────────────────────────────────────────────────
    ///   RightPanel (이 컴포넌트)
    ///   ├── CharacterIdText        "#1"
    ///   ├── CharacterNameText      "엔비 (주인공)"
    ///   ├── TabController          RecordBookTabController
    ///   └── ContentArea
    ///       ├── FragmentContent
    ///       │   ├── FragmentCountText   "수집한 문장 조각 (X/5)"
    ///       │   └── FragmentSlotContainer
    ///       ├── ConceptCardContent  (임시)
    ///       │   └── ConceptCardText
    ///       └── EpilogueContent    (임시)
    ///           └── EpilogueText
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterDetailView : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("캐릭터 정보")]
        [SerializeField] private TMP_Text _characterIdText;
        [SerializeField] private TMP_Text _characterNameText;

        [Header("탭 컨트롤러")]
        [SerializeField] private RecordBookTabController _tabController;

        [Header("문장 조각 탭")]
        [SerializeField] private GameObject _fragmentContent;
        [SerializeField] private TMP_Text _fragmentCountText;

        [Tooltip("실제 대사 텍스트를 가져올 CampaignDialogueSO입니다.")]
        [SerializeField] private CampaignDialogueSO _dialogueData;

        [Tooltip("미리 배치된 힌트/대사 텍스트 슬롯 목록입니다.\n" +
                 "Inspector에서 순서대로 TMP_Text를 연결합니다.\n" +
                 "슬롯 수 = RequiredFragmentCount와 일치해야 합니다.")]
        [SerializeField] private TMP_Text[] _fragmentSlots;

        [Header("컨셉 카드 탭 (임시)")]
        [SerializeField] private GameObject _conceptCardContent;
        [SerializeField] private TMP_Text _conceptCardText;

        [Header("시점 완결문 탭 (임시)")]
        [SerializeField] private GameObject _epilogueContent;
        [SerializeField] private TMP_Text _epilogueText;

        [Header("슬롯 색상")]
        [Tooltip("수집된 조각 텍스트 색상")]
        [SerializeField] private Color _collectedColor = new Color(0.1f, 0.1f, 0.1f);

        [Tooltip("미수집 힌트 텍스트 색상")]
        [SerializeField] private Color _hintColor = new Color(0.5f, 0.5f, 0.5f);

        [Header("글리치 설정")]
        [Tooltip("이름 미수집 시 글리치 처리 여부")]
        [SerializeField] private bool _glitchUnknownName = true;
        [SerializeField] private string _glitchChars = "█▓▒░?#@&*";

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private int _currentCharacterId = -1;
        private CharacterProfileData _currentProfile;
        private FragmentCollector _fragmentCollector;
        private FragmentHintDataSO _hintData;
        private CharacterRecordBook _recordBook;

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 의존성을 주입합니다.
        /// CharacterRecordBook.Awake()에서 호출합니다.
        /// </summary>
        public void Initialize(FragmentCollector collector,
                               FragmentHintDataSO hintData,
                               CharacterRecordBook recordBook)
        {
            _fragmentCollector = collector;
            _hintData = hintData;
            _recordBook = recordBook;

            // 탭 변경 이벤트 구독
            if (_tabController != null)
                _tabController.OnTabChanged += OnTabChanged;

            HideAllContent();
        }

        private void OnDestroy()
        {
            if (_tabController != null)
                _tabController.OnTabChanged -= OnTabChanged;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 선택된 캐릭터의 정보를 표시합니다.
        /// CharacterIconButton 클릭 시 CharacterRecordBook에서 호출합니다.
        /// </summary>
        public void ShowCharacter(int characterId, CharacterProfileData profile)
        {
            _currentCharacterId = characterId;
            _currentProfile = profile;

            RefreshCharacterHeader(characterId, profile);
            RefreshTabState(characterId);
            RefreshCurrentTabContent();
        }

        /// <summary>조각 수집 시 현재 표시 중인 캐릭터 정보를 갱신합니다.</summary>
        public void RefreshIfCurrent(int characterId)
        {
            Debug.Log($"[CharacterDetailView] RefreshIfCurrent — " +
                      $"요청={characterId}, " +
                      $"현재={_currentCharacterId}, " +
                      $"active={gameObject.activeInHierarchy}");

            if (_currentCharacterId != characterId) return;
            if (!gameObject.activeInHierarchy) return;

            RefreshCharacterHeader(characterId, _currentProfile);
            RefreshTabState(characterId);
            RefreshCurrentTabContent();
        }

        // ── Private — 헤더 ────────────────────────────────────────────────

        private void RefreshCharacterHeader(int characterId, CharacterProfileData profile)
        {
            if (_characterIdText != null)
                _characterIdText.text = $"#{characterId}";

            if (_characterNameText != null)
            {
                string collectedName = _recordBook?.GetCollectedName(characterId);
                bool hasName = !string.IsNullOrEmpty(collectedName);

                // 이름 수집됨 → 실제 이름
                // 이름 미수집 + 글리치 모드 → CharacterFullName을 글리치 처리
                // 이름 미수집 + 글리치 없음 → CharacterFullName 그대로
                string baseName = profile?.CharacterFullName ?? "???";
                string displayName = hasName
                    ? collectedName
                    : (_glitchUnknownName ? ApplyGlitch(baseName) : baseName);

                string role = profile?.CharacterRole ?? "";
                _characterNameText.text = string.IsNullOrEmpty(role)
                    ? displayName
                    : $"{displayName} {role}";
            }
        }


        // ── Private — 탭 상태 ─────────────────────────────────────────────

        private void RefreshTabState(int characterId)
        {
            bool conceptUnlocked = _fragmentCollector?.IsConceptCardUnlocked(characterId) ?? false;
            bool epilogueUnlocked = _fragmentCollector?.IsEpilogueUnlocked(characterId) ?? false;

            _tabController?.Refresh(conceptUnlocked, epilogueUnlocked);
        }

        // ── Private — 탭 콘텐츠 ──────────────────────────────────────────

        private void OnTabChanged(RecordBookTabController.TabType tab)
        {
            RefreshCurrentTabContent();
        }

        private void RefreshCurrentTabContent()
        {
            if (_tabController == null) return;

            HideAllContent();

            switch (_tabController.CurrentTab)
            {
                case RecordBookTabController.TabType.Fragment:
                    ShowFragmentContent();
                    break;
                case RecordBookTabController.TabType.ConceptCard:
                    ShowConceptCardContent();
                    break;
                case RecordBookTabController.TabType.Epilogue:
                    ShowEpilogueContent();
                    break;
            }
        }

        private void HideAllContent()
        {
            if (_fragmentContent != null) _fragmentContent.SetActive(false);
            if (_conceptCardContent != null) _conceptCardContent.SetActive(false);
            if (_epilogueContent != null) _epilogueContent.SetActive(false);
        }

        // ── 문장 조각 탭 ─────────────────────────────────────────────────

        private void ShowFragmentContent()
        {
            if (_fragmentContent != null)
                _fragmentContent.SetActive(true);

            if (_currentProfile == null) return;

            int characterId = _currentCharacterId;
            int requiredCount = _currentProfile.RequiredFragmentCount;
            int collectedCount = _fragmentCollector?.GetFragmentCount(characterId) ?? 0;

            // 카운트 텍스트
            if (_fragmentCountText != null)
                _fragmentCountText.text = $"수집한 문장 조각 ({collectedCount}/{requiredCount})";

            BuildFragmentSlots(characterId, requiredCount);
        }

        /// <summary>
        /// Inspector에 미리 배치된 TMP_Text 슬롯에 직접 텍스트를 설정합니다.
        /// 슬롯 수가 RequiredFragmentCount와 다르면 초과 슬롯은 비워둡니다.
        /// </summary>
        private void BuildFragmentSlots(int characterId, int slotCount)
        {
            if (_fragmentSlots == null || _fragmentSlots.Length == 0) return;

            var hints = _hintData?.GetHints(characterId) ?? new List<string>();

            for (int i = 0; i < _fragmentSlots.Length; i++)
            {
                var slotText = _fragmentSlots[i];
                if (slotText == null) continue;

                if (i >= slotCount)
                {
                    // 이 캐릭터에 필요한 슬롯 수 초과 → 빈칸
                    slotText.text = "";
                    slotText.enabled = false;
                    continue;
                }

                slotText.enabled = true;

                string fragmentId = BuildFragmentId(characterId, i);
                bool isCollected = _fragmentCollector?.HasFragment(fragmentId) ?? false;

                if (isCollected)
                {
                    // 수집된 조각 → 검은색 실제 대사
                    slotText.text = GetCollectedFragmentText(characterId, i);
                    slotText.color = _collectedColor;
                }
                else
                {
                    // 미수집 → 회색 힌트
                    slotText.text = i < hints.Count ? hints[i] : $"힌트 {i + 1}";
                    slotText.color = _hintColor;
                }
            }
        }

        /// <summary>
        /// 수집된 조각의 실제 대사 텍스트를 반환합니다.
        /// CampaignDialogueSO에서 FragmentId로 해당 대사를 검색합니다.
        /// </summary>
        private string GetCollectedFragmentText(int characterId, int index)
        {
            if (_dialogueData == null)
                return $"[대화 조각 {index + 1}]";

            string fragmentId = BuildFragmentId(characterId, index);

            return $"[대화 조각 {index + 1}]";
        }

        private string BuildFragmentId(int characterId, int index)
        {
            // FragmentId 명명 규칙: {stageId}_char{characterId}_frag{index}
            string stageId = HTH.Campaign.CampaignModeManager.Instance?.CurrentPhase2StageId
                             ?? "unknown";
            return $"{stageId}_char{characterId}_frag{index}";
        }

        // ── 컨셉 카드 탭 (임시) ───────────────────────────────────────────

        private void ShowConceptCardContent()
        {
            if (_conceptCardContent != null)
                _conceptCardContent.SetActive(true);

            if (_conceptCardText == null || _currentProfile?.ConceptCard == null) return;

            var card = _currentProfile.ConceptCard;
            _conceptCardText.text =
                $"[{card.Catchphrase}]\n\n" +
                $"{card.NarrativeBackground}\n\n" +
                $"{card.Personality}";
        }

        // ── 시점 완결문 탭 (임시) ─────────────────────────────────────────

        private void ShowEpilogueContent()
        {
            if (_epilogueContent != null)
                _epilogueContent.SetActive(true);

            if (_epilogueText == null || _currentProfile == null) return;

            _epilogueText.text = string.IsNullOrEmpty(_currentProfile.EpilogueText)
                ? "(시점 완결문 없음)"
                : _currentProfile.EpilogueText;
        }

        // ── 글리치 처리 ───────────────────────────────────────────────────

        private string ApplyGlitch(string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_glitchChars))
                return text;

            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == ' ' || UnityEngine.Random.value > 0.7f)
                    sb.Append(c);
                else
                    sb.Append(_glitchChars[UnityEngine.Random.Range(0, _glitchChars.Length)]);
            }
            return sb.ToString();
        }
    }
}