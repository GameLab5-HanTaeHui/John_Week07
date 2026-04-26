using DG.Tweening;
using UnityEngine;

namespace HTH.Campaign
{
    /// <summary>
    /// Phase2 진입 시 UI 전환을 처리합니다.
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   Phase1:
    ///     HistoryPanel → 활성화
    ///     MemoPanel    → 활성화
    ///     CharacterRecordRoot → 비활성화
    ///
    ///   Phase2 진입:
    ///     HistoryPanel → 슬라이드다운 → SetActive(false)
    ///     MemoPanel    → 슬라이드다운 → SetActive(false)
    ///     CharacterRecordRoot → SetActive(true) → 슬라이드업
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   History Panel          → HistoryPanel RectTransform
    ///   History Hide Offset    → 슬라이드다운 거리 (px, 기본 400)
    ///   Memo Panel             → MemoPanel RectTransform
    ///   Memo Hide Offset       → 슬라이드다운 거리 (px, 기본 400)
    ///   Character Record Root  → CharacterRecordPanel 7개의 부모 RectTransform
    ///   Character Show Offset  → 슬라이드업 시작 거리 (px, 기본 400)
    ///   Anim Duration          → 애니메이션 시간 (기본 0.35)
    /// </summary>
    [DisallowMultipleComponent]
    public class Phase2UITransition : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("Phase1 UI (Phase2 진입 시 숨김)")]
        [Tooltip("Phase1 히스토리 패널 RectTransform")]
        [SerializeField] private RectTransform _historyPanel;

        [Tooltip("히스토리 패널 슬라이드다운 거리 (px)\n" +
                 "현재 Y 위치에서 이 값만큼 아래로 내려갑니다.")]
        [SerializeField] private float _historyHideOffset = 400f;

        [Tooltip("Phase1 메모 패널 RectTransform")]
        [SerializeField] private RectTransform _memoPanel;

        [Tooltip("메모 패널 슬라이드다운 거리 (px)\n" +
                 "현재 Y 위치에서 이 값만큼 아래로 내려갑니다.")]
        [SerializeField] private float _memoHideOffset = 400f;

        [Header("Phase2 UI (Phase2 진입 시 표시)")]
        [Tooltip("CharacterRecordPanel 7개가 속한 부모 RectTransform")]
        [SerializeField] private RectTransform _characterRecordRoot;

        [Tooltip("캐릭터 기록장 슬라이드업 시작 거리 (px)\n" +
                 "원래 Y 위치에서 이 값만큼 아래에서 시작해 올라옵니다.")]
        [SerializeField] private float _characterShowOffset = 400f;

        [Header("애니메이션")]
        [SerializeField] private float _animDuration = 0.35f;
        [SerializeField] private Ease _hideEase = Ease.InCubic;
        [SerializeField] private Ease _showEase = Ease.OutCubic;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private float _historyPanelOriginY;
        private float _memoPanelOriginY;
        private float _characterRootOriginY;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_historyPanel != null) _historyPanelOriginY = _historyPanel.anchoredPosition.y;
            if (_memoPanel != null) _memoPanelOriginY = _memoPanel.anchoredPosition.y;
            if (_characterRecordRoot != null) _characterRootOriginY = _characterRecordRoot.anchoredPosition.y;

            // Phase2 UI는 처음에 비활성화
            if (_characterRecordRoot != null)
                _characterRecordRoot.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered += OnPhase2Entered;
        }

        private void OnDestroy()
        {
            if (CampaignModeManager.Instance != null)
                CampaignModeManager.Instance.OnPhase2Entered -= OnPhase2Entered;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────────

        private void OnPhase2Entered(string stageId)
        {
            HidePhase1UI();
            ShowPhase2UI();
        }

        // ── Private ──────────────────────────────────────────────────────

        private void HidePhase1UI()
        {
            SlideDown(_historyPanel, _historyPanelOriginY, _historyHideOffset);
            SlideDown(_memoPanel, _memoPanelOriginY, _memoHideOffset);
        }

        private void ShowPhase2UI()
        {
            if (_characterRecordRoot == null) return;
            StartCoroutine(ShowPhase2UICoroutine());
        }
        private System.Collections.IEnumerator ShowPhase2UICoroutine()
        {
            // 활성화
            _characterRecordRoot.gameObject.SetActive(true);

            // 한 프레임 대기 — SetActive 직후 DOTween이 정상 동작하도록
            yield return null;

            // 활성화 후 현재 위치를 오리진으로 측정
            float originY = _characterRecordRoot.anchoredPosition.y;

            // 아래에서 시작
            SetY(_characterRecordRoot, originY - _characterShowOffset);

            // 한 프레임 더 대기 — SetY 반영 후 애니메이션 시작
            yield return null;

            // 오리진으로 슬라이드업
            _characterRecordRoot
                .DOAnchorPosY(originY, _animDuration)
                .SetEase(_showEase);
        }

        private void SlideDown(RectTransform rect, float originY, float offset)
        {
            if (rect == null) return;

            rect.DOAnchorPosY(originY - offset, _animDuration)
                .SetEase(_hideEase)
                .OnComplete(() => rect.gameObject.SetActive(false));
        }

        private void SetY(RectTransform rect, float y)
        {
            if (rect == null) return;
            var pos = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(pos.x, y);
        }
    }
}