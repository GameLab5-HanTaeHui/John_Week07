using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// Phase2 프로파일 추리 진입 시 캐릭터를 선택하는 패널입니다.
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   HoldToEnterFinalDecision → "인물 추리를 시작하시겠습니까?" 확인창
    ///   → [확인] 클릭 → Show() 호출 → 이 패널 열림
    ///   → CharacterIconButton 클릭 → ProfileInquiryUI.Show(characterId)
    ///   → [닫기] 클릭 → 패널 닫힘
    ///
    /// ─── 버튼 구성 ───────────────────────────────────────────────────────
    ///   CharacterIconButton 프리팹을 동적 생성합니다.
    ///   조각 수 RequiredFragmentCount 이상 → 버튼 활성화
    ///   미만 → 버튼 비활성화 (interactable = false)
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Panel               → 전체 패널 GameObject (기본 비활성)
    ///   Icon Button Prefab  → CharacterIconButton 프리팹
    ///   Icon Grid           → 버튼 배치할 부모 Transform (1열)
    ///   Close Button        → 닫기 버튼
    ///   Profile Inquiry UI  → ProfileInquiryUI 컴포넌트
    ///   Fragment Collector  → FragmentCollector 컴포넌트
    ///   Profile Data        → ProfileDataSO 에셋
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfileInquirySelectPanel : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("UI")]
        [Tooltip("전체 패널 GameObject입니다. (기본 비활성)")]
        [SerializeField] private GameObject _panel;

        [Tooltip("CharacterIconButton 프리팹입니다.")]
        [SerializeField] private CharacterIconButton _iconButtonPrefab;

        [Tooltip("버튼을 배치할 부모 Transform입니다. (1열 배치)")]
        [SerializeField] private Transform _iconGrid;

        [Tooltip("닫기 버튼입니다.")]
        [SerializeField] private Button _closeButton;

        [Header("연결")]
        [Tooltip("캐릭터 선택 후 열릴 ProfileInquiryUI입니다.")]
        [SerializeField] private ProfileInquiryUI _profileInquiryUI;

        [Tooltip("대화 조각 수집 관리 컴포넌트입니다.")]
        [SerializeField] private FragmentCollector _fragmentCollector;

        [Tooltip("캐릭터 아이콘/이름/프로파일 데이터입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CharacterIconButton[] _buttons;
        private bool _isBuilt;

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
        /// 캐릭터 선택 패널을 엽니다.
        /// HoldToEnterFinalDecision에서 호출합니다.
        /// </summary>
        public void Show()
        {
            if (!_isBuilt) BuildGrid();
            else RefreshButtonStates();

            if (_panel != null) _panel.SetActive(true);
        }

        /// <summary>패널을 닫습니다.</summary>
        public void Hide()
        {
            // 선택 상태 초기화
            if (_buttons != null)
                foreach (var btn in _buttons)
                    btn?.SetSelected(false);

            if (_panel != null) _panel.SetActive(false);
        }

        // ── Private — 그리드 생성 ─────────────────────────────────────────

        /// <summary>
        /// CharacterIconButton 프리팹으로 캐릭터 버튼 7개를 생성합니다.
        /// 최초 Show() 호출 시 1회만 실행됩니다.
        /// </summary>
        private void BuildGrid()
        {
            if (_iconButtonPrefab == null || _iconGrid == null) return;

            _buttons = new CharacterIconButton[7];
            _isBuilt = true;

            for (int i = 0; i < 7; i++)
            {
                // 클로저 캡처 버그 방지
                int capturedId = i + 1;

                var profile = _profileData?.FindProfile(capturedId);
                Sprite icon = profile?.CharacterIcon;
                string name = profile?.CharacterFullName ?? $"#{capturedId}";
                // FragmentCollector.CanInquire() 기준 (10개)으로 통일
                bool canInquire = _fragmentCollector?.CanInquire(capturedId) ?? false;

                var btn = Instantiate(_iconButtonPrefab, _iconGrid);
                btn.Setup(
                    characterId: capturedId,
                    icon: icon,
                    collectedName: name,
                    onClicked: () => OnCharacterSelected(capturedId)
                );

                // 조각 10개 미달 시 비활성화
                var button = btn.GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                    button.interactable = canInquire;

                _buttons[i] = btn;
            }
        }

        /// <summary>
        /// 이미 생성된 버튼의 활성화 상태를 갱신합니다.
        /// Show() 재호출 시 사용합니다.
        /// </summary>
        private void RefreshButtonStates()
        {
            if (_buttons == null) return;

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;

                int capturedId = i + 1;
                // FragmentCollector.CanInquire() 기준 (10개)으로 통일
                bool canInquire = _fragmentCollector?.CanInquire(capturedId) ?? false;

                var button = _buttons[i].GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                    button.interactable = canInquire;
            }
        }

        // ── Private — 이벤트 ─────────────────────────────────────────────

        private void OnCharacterSelected(int characterId)
        {
            // 선택 상태 표시
            if (_buttons != null)
                for (int i = 0; i < _buttons.Length; i++)
                    _buttons[i]?.SetSelected(i + 1 == characterId);

            Hide();
            _profileInquiryUI?.Show(characterId);
        }
    }
}