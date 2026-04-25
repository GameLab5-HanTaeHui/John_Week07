using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 캐릭터 개별 인물 기록장 패널입니다.
    /// HistoryPagePanel의 슬라이드업 방식을 참고해 아래에서 위로 등장합니다.
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   Phase2 인게임에서 캐릭터를 클릭하면 해당 캐릭터의
    ///   개별 기록장이 슬라이드업으로 등장합니다.
    ///   표시 내용은 CharacterRecordBook과 동일합니다:
    ///   이름, 문장 조각, 컨셉 카드, 시점 완결문 탭
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   캐릭터 클릭
    ///   → CharacterRecordPanelManager.Open(characterId)
    ///   → SetActive(true) + SpawnIn() 슬라이드업 등장
    ///   → 닫기 버튼 또는 외부 클릭 → 슬라이드다운 후 SetActive(false)
    ///
    /// ─── 씬 구조 ─────────────────────────────────────────────────────────
    ///   CharacterRecordPanel (이 컴포넌트 + RectTransform)
    ///   ├── Header
    ///   │   ├── CharacterIdText     "#1"
    ///   │   ├── CharacterNameText   "엔비 (주인공)"
    ///   │   └── CloseButton
    ///   ├── TabBar (RecordBookTabController)
    ///   │   ├── FragmentTabButton
    ///   │   ├── ConceptCardTabButton
    ///   │   └── EpilogueTabButton
    ///   └── ContentArea
    ///       ├── FragmentContent
    ///       │   ├── FragmentCountText
    ///       │   └── FragmentSlots (TMP_Text × 5)
    ///       ├── ConceptCardContent
    ///       │   └── ConceptCardText
    ///       └── EpilogueContent
    ///           └── EpilogueText
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Profile Data         → ProfileDataSO 에셋
    ///   Hint Data            → FragmentHintDataSO 에셋
    ///   Fragment Collector   → _CampaignSystem/FragmentCollector
    ///   Character Record Book → _CampaignSystem/CharacterRecordBook (이름 참조)
    ///   Dialogue Data        → CampaignDialogueSO 에셋 (조각 텍스트)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class CharacterRecordPanel : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [SerializeField] private ProfileDataSO _profileData;
        [SerializeField] private FragmentHintDataSO _hintData;
        [SerializeField] private CampaignDialogueSO _dialogueData;

        [Header("컴포넌트 참조")]
        [SerializeField] private FragmentCollector _fragmentCollector;
        [SerializeField] private CharacterRecordBook _characterRecordBook;

        [Header("헤더")]
        [SerializeField] private TMP_Text _characterIdText;
        [SerializeField] private TMP_Text _characterNameText;
        [SerializeField] private Button _closeButton;

        [Header("탭")]
        [SerializeField] private RecordBookTabController _tabController;

        [Header("문장 조각 탭")]
        [SerializeField] private GameObject _fragmentContent;
        [SerializeField] private TMP_Text _fragmentCountText;
        [SerializeField] private TMP_Text[] _fragmentSlots;

        [Header("컨셉 카드 탭 (임시)")]
        [SerializeField] private GameObject _conceptCardContent;
        [SerializeField] private TMP_Text _conceptCardText;

        [Header("시점 완결문 탭 (임시)")]
        [SerializeField] private GameObject _epilogueContent;
        [SerializeField] private TMP_Text _epilogueText;

        [Header("슬롯 색상")]
        [SerializeField] private Color _collectedColor = new Color(0.1f, 0.1f, 0.1f);
        [SerializeField] private Color _hintColor = new Color(0.5f, 0.5f, 0.5f);

        [Header("글리치 설정")]
        [SerializeField] private bool _glitchUnknownName = true;
        [SerializeField] private string _glitchChars = "█▓▒░?#@&*";

        [Header("슬라이드 애니메이션")]
        [Tooltip("아래에서 등장할 때 오리진에서 얼마나 아래에서 시작할지 (px)")]
        [SerializeField] private float _spawnBelowOffset = 400f;
        [SerializeField] private float _animDuration = 0.35f;
        [SerializeField] private Ease _expandEase = Ease.OutCubic;
        [SerializeField] private Ease _collapseEase = Ease.InCubic;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private RectTransform _rect;
        private float _originY;
        private Tweener _currentTween;
        private int _currentCharacterId = -1;
        private CharacterProfileData _currentProfile;

        /// <summary>현재 패널이 열려있는지 여부입니다.</summary>
        public bool IsOpen { get; private set; }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _originY = _rect.anchoredPosition.y;

            gameObject.SetActive(false);
            _closeButton?.onClick.AddListener(Close);

            if (_tabController != null)
                _tabController.OnTabChanged += OnTabChanged;
        }

        private void OnDestroy()
        {
            _currentTween?.Kill();
            _closeButton?.onClick.RemoveListener(Close);

            if (_tabController != null)
                _tabController.OnTabChanged -= OnTabChanged;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 특정 캐릭터의 개별 기록장을 슬라이드업으로 엽니다.
        /// Phase2에서 캐릭터 클릭 시 CharacterRecordPanelManager에서 호출합니다.
        /// </summary>
        public void Open(int characterId)
        {
            if (_profileData == null) return;

            var profile = _profileData.FindProfile(characterId);
            if (profile == null) return;

            _currentCharacterId = characterId;
            _currentProfile = profile;

            RefreshAll();

            gameObject.SetActive(true);
            SpawnIn();
            IsOpen = true;
        }

        /// <summary>패널을 슬라이드다운으로 닫습니다.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SlideDown();
        }

        /// <summary>
        /// 조각 수집 시 현재 열려있으면 즉시 갱신합니다.
        /// FragmentCollector 이벤트 수신 시 호출합니다.
        /// </summary>
        public void RefreshIfCurrent(int characterId)
        {
            if (!IsOpen) return;
            if (_currentCharacterId != characterId) return;
            RefreshFragmentContent();
        }

        // ── Private — 전체 갱신 ───────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshHeader();
            RefreshTabState();
            _tabController?.ResetToFragment();
            RefreshCurrentTabContent();
        }

        private void RefreshHeader()
        {
            if (_characterIdText != null)
                _characterIdText.text = $"#{_currentCharacterId}";

            if (_characterNameText != null)
            {
                string collectedName = _characterRecordBook?.GetCollectedName(_currentCharacterId);
                bool hasName = !string.IsNullOrEmpty(collectedName);

                string baseName = _currentProfile?.CharacterFullName ?? "???";
                string displayName = hasName
                    ? collectedName
                    : (_glitchUnknownName ? ApplyGlitch(baseName) : baseName);

                string role = _currentProfile?.CharacterRole ?? "";
                _characterNameText.text = string.IsNullOrEmpty(role)
                    ? displayName
                    : $"{displayName} {role}";
            }
        }

        private void RefreshTabState()
        {
            bool conceptUnlocked = _fragmentCollector?.IsConceptCardUnlocked(_currentCharacterId) ?? false;
            bool epilogueUnlocked = _fragmentCollector?.IsEpilogueUnlocked(_currentCharacterId) ?? false;
            _tabController?.Refresh(conceptUnlocked, epilogueUnlocked);
        }

        // ── Private — 탭 콘텐츠 ──────────────────────────────────────────

        private void OnTabChanged(RecordBookTabController.TabType tab)
        {
            RefreshCurrentTabContent();
        }

        private void RefreshCurrentTabContent()
        {
            HideAllContent();

            if (_tabController == null) return;

            switch (_tabController.CurrentTab)
            {
                case RecordBookTabController.TabType.Fragment:
                    RefreshFragmentContent();
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

        private void RefreshFragmentContent()
        {
            if (_fragmentContent != null)
                _fragmentContent.SetActive(true);

            if (_currentProfile == null) return;

            int requiredCount = _currentProfile.RequiredFragmentCount;
            int collectedCount = _fragmentCollector?.GetFragmentCount(_currentCharacterId) ?? 0;

            if (_fragmentCountText != null)
                _fragmentCountText.text = $"수집한 문장 조각 ({collectedCount}/{requiredCount})";

            BuildFragmentSlots(requiredCount);
        }

        private void BuildFragmentSlots(int slotCount)
        {
            if (_fragmentSlots == null || _fragmentSlots.Length == 0) return;

            var hints = _hintData?.GetHints(_currentCharacterId) ?? new List<string>();

            for (int i = 0; i < _fragmentSlots.Length; i++)
            {
                var slotText = _fragmentSlots[i];
                if (slotText == null) continue;

                if (i >= slotCount)
                {
                    slotText.text = "";
                    slotText.enabled = false;
                    continue;
                }

                slotText.enabled = true;

                string fragmentId = BuildFragmentId(_currentCharacterId, i);
                bool isCollected = _fragmentCollector?.HasFragment(fragmentId) ?? false;

                if (isCollected)
                {
                    slotText.text = GetCollectedFragmentText(_currentCharacterId, i);
                    slotText.color = _collectedColor;
                }
                else
                {
                    slotText.text = i < hints.Count ? hints[i] : $"힌트 {i + 1}";
                    slotText.color = _hintColor;
                }
            }
        }

        private string GetCollectedFragmentText(int characterId, int index)
        {
            if (_dialogueData == null) return $"[대화 조각 {index + 1}]";

            string fragmentId = BuildFragmentId(characterId, index);
            var entry = _dialogueData.FindGroupDialogueByFragment(fragmentId);

            if (entry == null || entry.Lines == null || entry.Lines.Count == 0)
                return $"[대화 조각 {index + 1}]";

            foreach (var line in entry.Lines)
                if (line != null && !string.IsNullOrEmpty(line.Text))
                    return line.Text;

            return $"[대화 조각 {index + 1}]";
        }

        private string BuildFragmentId(int characterId, int index)
        {
            string stageId = CampaignModeManager.Instance?.CurrentPhase2StageId ?? "unknown";
            return $"{stageId}_char{characterId}_frag{index}";
        }

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

        private void ShowEpilogueContent()
        {
            if (_epilogueContent != null)
                _epilogueContent.SetActive(true);

            if (_epilogueText == null || _currentProfile == null) return;

            _epilogueText.text = string.IsNullOrEmpty(_currentProfile.EpilogueText)
                ? "(시점 완결문 없음)"
                : _currentProfile.EpilogueText;
        }

        // ── Private — 슬라이드 애니메이션 ────────────────────────────────

        /// <summary>오리진 아래에서 시작해 슬라이드업으로 등장합니다.</summary>
        private void SpawnIn()
        {
            SnapTo(_originY - _spawnBelowOffset);
            SlideTo(_originY, _expandEase);
        }

        /// <summary>아래로 슬라이드한 후 비활성화합니다.</summary>
        private void SlideDown()
        {
            _currentTween?.Kill();
            _currentTween = _rect
                .DOAnchorPosY(_originY - _spawnBelowOffset, _animDuration)
                .SetEase(_collapseEase)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private void SlideTo(float targetY, Ease ease)
        {
            _currentTween?.Kill();
            _currentTween = _rect
                .DOAnchorPosY(targetY, _animDuration)
                .SetEase(ease);
        }

        private void SnapTo(float targetY)
        {
            _currentTween?.Kill();
            var pos = _rect.anchoredPosition;
            _rect.anchoredPosition = new Vector2(pos.x, targetY);
        }

        // ── Private — 글리치 처리 ─────────────────────────────────────────

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