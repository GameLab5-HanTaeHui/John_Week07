using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 씬 UI 패널 스왑을 담당합니다.
    /// 버튼 클릭 또는 Tab 키로 두 패널 그룹을 토글합니다.
    ///
    /// ─── UI 상태 ─────────────────────────────────────────────────────────
    ///   CharacterRecord 상태:
    ///     CharacterRecordRoot → 활성 + 제자리
    ///     HistoryPanel        → 비활성 (슬라이드다운)
    ///     MemoPanel           → 비활성 (슬라이드다운)
    ///     버튼 텍스트         → "사건 기록지 보기"
    ///
    ///   HistoryMemo 상태:
    ///     HistoryPanel        → 활성 + 제자리
    ///     MemoPanel           → 활성 + 제자리
    ///     CharacterRecordRoot → 비활성 (슬라이드다운)
    ///     버튼 텍스트         → "인물 파일 보기"
    ///
    /// ─── 클릭 방지 ───────────────────────────────────────────────────────
    ///   스왑 시작 시 버튼 즉시 비활성화.
    ///   애니메이션 완료 후 _clickBlockDuration(기본 1초) 추가 대기 후 재활성화.
    ///   Tab 키도 _isAnimating 플래그로 동일하게 차단됩니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Swap Button            → 스왑 전환 Button
    ///   Swap Button Label      → Button 하위 TMP_Text (텍스트 전환용)
    ///   Label CharacterRecord  → CharacterRecord 활성일 때 버튼 텍스트 (기본: "사건 기록지 보기")
    ///   Label History Memo     → HistoryMemo 활성일 때 버튼 텍스트 (기본: "인물 파일 보기")
    ///   History Panel          → HistoryPanel RectTransform
    ///   Memo Panel             → MemoPanel RectTransform
    ///   Character Record Root  → CharacterRecordPanel 7개의 부모 RectTransform
    ///   Hide Offset            → 슬라이드 거리 (px, 기본 400)
    ///   Anim Duration          → 애니메이션 시간 (기본 0.35초)
    ///   Click Block Duration   → 애니메이션 완료 후 추가 클릭 차단 시간 (기본 1초)
    ///   Use Tab Key            → Tab 키 토글 여부 (기본 true)
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignUISwapper : MonoBehaviour
    {
        // ── UI 상태 ───────────────────────────────────────────────────────

        private enum UIState { CharacterRecord, HistoryMemo }

        // ── Inspector ────────────────────────────────────────────────────

        [Header("토글 버튼")]
        [Tooltip("클릭 시 두 패널 그룹을 교체합니다.")]
        [SerializeField] private Button _swapButton;

        [Tooltip("Button 하위 TMP_Text입니다. 현재 상태에 따라 텍스트가 바뀝니다.")]
        [SerializeField] private TMP_Text _swapButtonLabel;

        [Header("버튼 텍스트")]
        [Tooltip("CharacterRecord가 활성일 때 버튼에 표시할 텍스트.")]
        [SerializeField] private string _labelCharacterRecord = "사건 기록지 보기";

        [Tooltip("HistoryMemo가 활성일 때 버튼에 표시할 텍스트.")]
        [SerializeField] private string _labelHistoryMemo = "캐릭터 파일 보기";

        [Header("History / Memo 패널 그룹")]
        [SerializeField] private RectTransform _historyPanel;
        [SerializeField] private RectTransform _memoPanel;

        [Header("Character Record 패널 그룹")]
        [SerializeField] private RectTransform _characterRecordRoot;

        [Header("애니메이션")]
        [Tooltip("슬라이드 거리 (px). 숨길 때 아래로, 표시할 때 아래에서 올라옵니다.")]
        [SerializeField] private float _hideOffset = 400f;
        [SerializeField] private float _animDuration = 0.35f;
        [SerializeField] private Ease _hideEase = Ease.InCubic;
        [SerializeField] private Ease _showEase = Ease.OutCubic;

        [Header("클릭 방지")]
        [Tooltip("애니메이션 완료 후 버튼을 추가로 차단할 시간(초). 기본 1초.")]
        [SerializeField] private float _clickBlockDuration = 1f;

        [Header("Tab 키 입력")]
        [Tooltip("true이면 Tab 키로도 패널을 전환합니다.")]
        [SerializeField] private bool _useTabKey = true;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private UIState _currentState = UIState.HistoryMemo;
        private bool _isAnimating;

        private float _historyOriginY;
        private float _memoOriginY;
        private float _characterOriginY;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_historyPanel != null) _historyOriginY = _historyPanel.anchoredPosition.y;
            if (_memoPanel != null) _memoOriginY = _memoPanel.anchoredPosition.y;
            if (_characterRecordRoot != null) _characterOriginY = _characterRecordRoot.anchoredPosition.y;

            // 초기 상태 — HistoryMemo 활성, CharacterRecord 비활성
            SetActiveInstant(_characterRecordRoot, true, _characterOriginY - _hideOffset);
            SetActiveInstant(_historyPanel, true, _historyOriginY);
            SetActiveInstant(_memoPanel, true, _memoOriginY);
            _currentState = UIState.HistoryMemo;
        }

        private void Start()
        {
            if (_swapButton != null)
                _swapButton.onClick.AddListener(OnSwapButtonClicked);

            // 초기 텍스트 설정
            RefreshButtonLabel();
        }

        private void OnDestroy()
        {
            if (_swapButton != null)
                _swapButton.onClick.RemoveListener(OnSwapButtonClicked);
        }

        private void Update()
        {
            if (!_useTabKey || _isAnimating) return;
            if (Input.GetKeyDown(KeyCode.Tab))
                TrySwap();
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>외부에서 강제로 특정 상태로 전환합니다.</summary>
        public void ShowCharacterRecord() => TrySwapTo(UIState.CharacterRecord);
        public void ShowHistoryMemo() => TrySwapTo(UIState.HistoryMemo);

        // ── Private ──────────────────────────────────────────────────────

        private void OnSwapButtonClicked() => TrySwap();

        private void TrySwap()
        {
            TrySwapTo(_currentState == UIState.CharacterRecord
                ? UIState.HistoryMemo
                : UIState.CharacterRecord);
        }

        private void TrySwapTo(UIState target)
        {
            if (_isAnimating || _currentState == target) return;
            _currentState = target;
            StartCoroutine(SwapCoroutine(target));
        }

        private IEnumerator SwapCoroutine(UIState target)
        {
            _isAnimating = true;

            // ★ 스왑 시작 즉시 버튼 비활성화
            SetButtonInteractable(false);

            HistoryPageController.Instance?.CollapseExpanded();
            CharacterRecordPanelManager.Instance?.CloseCurrentPanel();

            if (target == UIState.CharacterRecord)
            {
                SlideDownAndHide(_historyPanel, _historyOriginY);
                SlideDownAndHide(_memoPanel, _memoOriginY);
                yield return StartCoroutine(SlideUpAndShow(_characterRecordRoot, _characterOriginY));
            }
            else
            {
                SlideDownAndHide(_characterRecordRoot, _characterOriginY);
                yield return StartCoroutine(SlideUpAndShow(_historyPanel, _historyOriginY));
                yield return StartCoroutine(SlideUpAndShow(_memoPanel, _memoOriginY));
            }

            // ★ 애니메이션 완료 후 텍스트 전환
            RefreshButtonLabel();

            // ★ 추가 클릭 방지 대기
            if (_clickBlockDuration > 0f)
                yield return new WaitForSeconds(_clickBlockDuration);

            SetButtonInteractable(true);
            _isAnimating = false;
        }

        // ── 버튼 관리 ─────────────────────────────────────────────────────

        /// <summary>
        /// 현재 UIState에 맞게 버튼 텍스트를 갱신합니다.
        /// CharacterRecord 활성 → "사건 기록지 보기"
        /// HistoryMemo 활성     → "인물 파일 보기"
        /// </summary>
        private void RefreshButtonLabel()
        {
            if (_swapButtonLabel == null) return;
            _swapButtonLabel.text = _currentState == UIState.CharacterRecord
                ? _labelCharacterRecord
                : _labelHistoryMemo;
        }

        private void SetButtonInteractable(bool interactable)
        {
            if (_swapButton != null)
                _swapButton.gameObject.SetActive(interactable);
        }

        // ── 애니메이션 헬퍼 ──────────────────────────────────────────────

        /// <summary>슬라이드다운 후 SetActive(false).</summary>
        private void SlideDownAndHide(RectTransform rect, float originY)
        {
            if (rect == null) return;
            rect.DOAnchorPosY(originY - _hideOffset, _animDuration)
                .SetEase(_hideEase);

            // 오브젝트 비활성화 코드
            //.SetEase(_hideEase)
            //.OnComplete(() => rect.gameObject.SetActive(false));
        }

        /// <summary>SetActive(true) 후 아래에서 슬라이드업. 완료까지 대기.</summary>
        private IEnumerator SlideUpAndShow(RectTransform rect, float originY)
        {
            if (rect == null) yield break;

            // 오브젝트 활성화
            //rect.gameObject.SetActive(true);
            //yield return null;

            SetY(rect, originY - _hideOffset);
            yield return null;

            bool done = false;
            rect.DOAnchorPosY(originY, _animDuration)
                .SetEase(_showEase)
                .OnComplete(() => done = true);

            yield return new WaitUntil(() => done);
        }

        /// <summary>애니메이션 없이 즉시 위치/활성 설정. Awake 초기화에 사용.</summary>
        private void SetActiveInstant(RectTransform rect, bool active, float posY)
        {
            if (rect == null) return;
            SetY(rect, posY);
            rect.gameObject.SetActive(active);
        }

        private void SetY(RectTransform rect, float y)
        {
            if (rect == null) return;
            var pos = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(pos.x, y);
        }
    }
}