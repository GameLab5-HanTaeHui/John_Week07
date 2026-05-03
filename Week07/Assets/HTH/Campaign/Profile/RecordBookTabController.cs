using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 인물 기록장의 탭 시스템입니다.
    ///
    /// ─── 탭 종류 ─────────────────────────────────────────────────────────
    ///   문장 조각   : 항상 활성. 수집된 조각과 힌트 표시.
    ///   컨셉 카드   : 컨셉 카드 해금 시 활성. 미해금 시 취소선.
    ///   시점 완결문 : 에필로그 해금 시 활성. 미해금 시 취소선.
    ///
    /// ─── 탭 상태 ─────────────────────────────────────────────────────────
    ///   잠금 (미해금): 취소선 텍스트, 클릭 무시
    ///   활성 (해금됨): 정상 텍스트, 클릭 가능
    ///   선택됨       : 하이라이트 표시
    ///
    /// ─── 프리팹/씬 구조 ──────────────────────────────────────────────────
    ///   TabBar
    ///   ├── FragmentTabButton     (Button + TMP_Text)
    ///   ├── ConceptCardTabButton  (Button + TMP_Text)
    ///   └── EpilogueTabButton     (Button + TMP_Text)
    /// </summary>
    [DisallowMultipleComponent]
    public class RecordBookTabController : MonoBehaviour
    {
        // ── 탭 종류 열거형 ────────────────────────────────────────────────

        public enum TabType { Fragment, ConceptCard, Epilogue }

        // ── Inspector ────────────────────────────────────────────────────

        [Header("탭 버튼")]
        [SerializeField] private Button _fragmentTabButton;
        [SerializeField] private TMP_Text _fragmentTabText;

        [SerializeField] private Button _conceptCardTabButton;
        [SerializeField] private TMP_Text _conceptCardTabText;

        [SerializeField] private Button _epilogueTabButton;
        [SerializeField] private TMP_Text _epilogueTabText;

        [Header("탭 텍스트")]
        [SerializeField] private string _fragmentTabLabel = "문장 조각";
        [SerializeField] private string _conceptCardTabLabel = "컨셉 카드";
        [SerializeField] private string _epilogueTabLabel = "시점 완결문";

        [Header("탭 색상")]
        [Tooltip("선택된 탭 색상")]
        [SerializeField] private Color _selectedColor = new Color(0.2f, 0.2f, 0.2f);
        [Tooltip("활성화된 탭 색상 (해금됨, 미선택)")]
        [SerializeField] private Color _activeColor = new Color(0.5f, 0.5f, 0.5f);
        [Tooltip("잠긴 탭 색상 (미해금)")]
        [SerializeField] private Color _lockedColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>탭 변경 시 발생합니다. CharacterDetailView에서 구독합니다.</summary>
        public event Action<TabType> OnTabChanged;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private TabType _currentTab = TabType.Fragment;
        private bool _conceptCardUnlocked;
        private bool _epilogueUnlocked;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _fragmentTabButton?.onClick.AddListener(() => OnTabClicked(TabType.Fragment));
            _conceptCardTabButton?.onClick.AddListener(() => OnTabClicked(TabType.ConceptCard));
            _epilogueTabButton?.onClick.AddListener(() => OnTabClicked(TabType.Epilogue));
        }

        private void OnDestroy()
        {
            _fragmentTabButton?.onClick.RemoveAllListeners();
            _conceptCardTabButton?.onClick.RemoveAllListeners();
            _epilogueTabButton?.onClick.RemoveAllListeners();
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 특정 캐릭터의 해금 상태에 맞게 탭을 갱신합니다.
        /// CharacterDetailView.ShowCharacter()에서 호출합니다.
        /// </summary>
        public void Refresh(bool conceptCardUnlocked, bool epilogueUnlocked)
        {
            _conceptCardUnlocked = conceptCardUnlocked;
            _epilogueUnlocked = epilogueUnlocked;

            // 현재 탭이 잠겨있으면 문장 조각 탭으로 초기화
            if (_currentTab == TabType.ConceptCard && !conceptCardUnlocked)
                _currentTab = TabType.Fragment;
            if (_currentTab == TabType.Epilogue && !epilogueUnlocked)
                _currentTab = TabType.Fragment;

            UpdateTabVisuals();
        }

        /// <summary>
        /// 문장 조각 탭으로 초기화합니다.
        /// 캐릭터 선택 변경 시 호출합니다.
        /// </summary>
        public void ResetToFragment()
        {
            _currentTab = TabType.Fragment;
            UpdateTabVisuals();
        }

        /// <summary>현재 선택된 탭입니다.</summary>
        public TabType CurrentTab => _currentTab;

        // ── Private ──────────────────────────────────────────────────────

        private void OnTabClicked(TabType tab)
        {
            // 잠긴 탭 클릭 무시
            if (tab == TabType.ConceptCard && !_conceptCardUnlocked) return;
            if (tab == TabType.Epilogue && !_epilogueUnlocked) return;

            _currentTab = tab;
            UpdateTabVisuals();
            OnTabChanged?.Invoke(tab);
        }

        private void UpdateTabVisuals()
        {
            // 문장 조각 탭 (항상 활성)
            SetTabVisual(_fragmentTabText, _fragmentTabLabel,
                         _currentTab == TabType.Fragment,
                         isLocked: false);

            // 컨셉 카드 탭
            SetTabVisual(_conceptCardTabText, _conceptCardTabLabel,
                         _currentTab == TabType.ConceptCard,
                         isLocked: !_conceptCardUnlocked);

            // 시점 완결문 탭
            SetTabVisual(_epilogueTabText, _epilogueTabLabel,
                         _currentTab == TabType.Epilogue,
                         isLocked: !_epilogueUnlocked);

            // 버튼 인터랙션 설정
            if (_conceptCardTabButton != null)
                _conceptCardTabButton.interactable = _conceptCardUnlocked;
            if (_epilogueTabButton != null)
                _epilogueTabButton.interactable = _epilogueUnlocked;
        }

        private void SetTabVisual(TMP_Text text, string label, bool isSelected, bool isLocked)
        {
            if (text == null) return;

            // 취소선 처리
            text.text = isLocked ? $"<s>{label}</s>" : label;
            text.color = isSelected ? _selectedColor
                       : isLocked ? _lockedColor
                                    : _activeColor;
        }
    }
}