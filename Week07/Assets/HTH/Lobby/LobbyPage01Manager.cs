using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign.Lobby
{
    /// <summary>
    /// 로비 페이지 1 UI를 관리합니다.
    ///
    /// ─── 버튼 목록 ──────────────────────────────────────────────────────
    ///   Tutorial Button      → 튜토리얼 씬 이동
    ///   Full Reset Button    → 완전 초기화 (조각·내역 포함 전체 삭제)
    ///   Partial Reset Button → 일부 초기화 (조각·내역 유지, 나머지만 삭제)
    ///   Continue Button      → 이어하기 (세이브 있을 때만 표시)
    ///
    /// ─── 경고 패널 흐름 ─────────────────────────────────────────────────
    ///   완전 초기화 버튼 클릭
    ///     → "대화 조각과 대화 내역이 모두 초기화 됩니다! 정말로 초기화 하시겠습니까?"
    ///     → [확인] ResetFull() / [취소] 닫기
    ///
    ///   일부 초기화 버튼 클릭
    ///     → "대화 조각과 대화 내역은 유지됩니다. 정말로 초기화 하겠습니까?"
    ///     → [확인] ResetPartial() / [취소] 닫기
    ///
    /// ─── Inspector 연결 ─────────────────────────────────────────────────
    ///   버튼 4개, 경고 패널 (루트 / TitleText / ConfirmButton / CancelButton),
    ///   LobbyFlowController, RewardSaveData
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyPage01Manager : MonoBehaviour
    {
        private const string StageId = "CampaignMode";

        // ── Inspector — 버튼 ─────────────────────────────────────────────

        [Header("버튼")]
        [SerializeField] private Button _tutorialButton;

        [Tooltip("완전 초기화 버튼 — 조각·내역 포함 전체 삭제")]
        [SerializeField] private Button _fullResetButton;

        [Tooltip("일부 초기화 버튼 — 조각·내역 유지, 나머지만 삭제")]
        [SerializeField] private Button _partialResetButton;

        [SerializeField] private Button _continueButton;

        [Header("다음 장 (페이지 2)")]
        [SerializeField] private GameObject _nextPageButtonObj;
        private Button _nextPageButton;

        // ── Inspector — 경고 패널 ────────────────────────────────────────

        [Header("경고 패널")]
        [Tooltip("경고 패널 루트 GameObject")]
        [SerializeField] private GameObject _warningPanel;

        [Tooltip("경고 패널 메시지 TMP")]
        [SerializeField] private TMPro.TMP_Text _warningTitleText;

        [Tooltip("확인 버튼")]
        [SerializeField] private Button _warningConfirmButton;

        [Tooltip("취소 버튼")]
        [SerializeField] private Button _warningCancelButton;

        // ── Inspector — 의존성 ───────────────────────────────────────────

        [Header("의존성")]
        [SerializeField] private LobbyFlowController _flowController;
        [SerializeField] private RewardSaveData _rewardSaveData;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private System.Action _pendingConfirmAction;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_nextPageButtonObj != null)
                _nextPageButton = _nextPageButtonObj.GetComponentInChildren<Button>();

            if (_warningPanel != null)
                _warningPanel.SetActive(false);

            SetupButtons();
        }

        private void OnEnable()
        {
            RefreshUIState();
        }

        private void OnDestroy()
        {
            _warningConfirmButton?.onClick.RemoveListener(OnWarningConfirmed);
            _warningCancelButton?.onClick.RemoveListener(OnWarningCancelled);
        }

        // ── 버튼 설정 ────────────────────────────────────────────────────

        private void SetupButtons()
        {
            _tutorialButton?.onClick.AddListener(() => _flowController?.StartTutorial());
            _continueButton?.onClick.AddListener(() => _flowController?.ContinueCampaign());
            _nextPageButton?.onClick.AddListener(() => _flowController?.GoToNextPage());

            _fullResetButton?.onClick.AddListener(OnFullResetClicked);
            _partialResetButton?.onClick.AddListener(OnPartialResetClicked);

            // 경고 패널 버튼은 Awake에서 1회만 구독 — _pendingConfirmAction으로 동작 위임
            _warningConfirmButton?.onClick.AddListener(OnWarningConfirmed);
            _warningCancelButton?.onClick.AddListener(OnWarningCancelled);
        }

        // ── UI 상태 갱신 ─────────────────────────────────────────────────

        public void RefreshUIState()
        {
            if (_flowController == null) return;

            bool hasSave = _flowController.HasCampaignSave();
            bool hasCodex = CheckCodexUnlocked();

            _continueButton?.gameObject.SetActive(hasSave);
            _nextPageButtonObj?.SetActive(hasCodex);
        }

        // ── 완전 초기화 버튼 ─────────────────────────────────────────────

        private void OnFullResetClicked()
        {
            ShowWarningPanel(
                message: "대화 조각과 대화 내역이 모두 초기화 됩니다!\n정말로 초기화 하시겠습니까?",
                onConfirm: () =>
                {
                    CampaignSaveManager.Instance?.ResetFull(StageId);
                    _flowController?.StartNewCampaign();
                    RefreshUIState();
                }
            );
        }

        // ── 일부 초기화 버튼 ─────────────────────────────────────────────

        private void OnPartialResetClicked()
        {
            ShowWarningPanel(
                message: "대화 조각과 대화 내역은 유지됩니다.\n정말로 초기화 하겠습니까?",
                onConfirm: () =>
                {
                    CampaignSaveManager.Instance?.ResetPartial(StageId);
                    _flowController?.StartNewCampaign();
                    RefreshUIState();
                }
            );
        }

        // ── 경고 패널 ────────────────────────────────────────────────────

        private void ShowWarningPanel(string message, System.Action onConfirm)
        {
            _pendingConfirmAction = onConfirm;

            if (_warningTitleText != null)
                _warningTitleText.text = message;

            if (_warningPanel != null)
                _warningPanel.SetActive(true);
        }

        private void OnWarningConfirmed()
        {
            var action = _pendingConfirmAction;
            HideWarningPanel();
            action?.Invoke();
        }

        private void OnWarningCancelled()
        {
            HideWarningPanel();
        }

        private void HideWarningPanel()
        {
            if (_warningPanel != null)
                _warningPanel.SetActive(false);

            _pendingConfirmAction = null;
        }

        // ── 도감 해금 확인 ───────────────────────────────────────────────

        private bool CheckCodexUnlocked()
        {
            if (_rewardSaveData == null) return false;
            for (int i = 1; i <= 7; i++)
                if (_rewardSaveData.IsEpilogueUnlocked(i)) return true;
            return false;
        }
    }
}