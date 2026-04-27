using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 로비 1페이지를 관리합니다.
    ///
    /// ─── 버튼 구성 ───────────────────────────────────────────────────────
    ///   캠페인 모드 버튼
    ///     저장 데이터 없음 → "새로 시작" 문구
    ///     저장 데이터 있음 → "이어하기" 문구
    ///     튜토리얼 미클리어 → 비활성화
    ///
    ///   이야기 초기화 버튼
    ///     저장 데이터 있을 때만 표시
    ///     클릭 시 WarningDialog 표시
    ///
    ///   도감 페이지 버튼 (→)
    ///     FragmentCollector에서 시점 완결문 1개 이상 해금 시 활성화
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Campaign Mode Button  → 캠페인 모드 진입 버튼
    ///   Button Label          → 캠페인 모드 버튼 라벨 TMP
    ///   Reset Button          → 이야기 초기화 버튼 (GameObject)
    ///   Next Page Button      → 도감 페이지 이동 버튼 (GameObject)
    ///   Warning Dialog        → WarningDialog 컴포넌트
    ///   Stage Id              → 캠페인 스테이지 ID (예: Stage_1_Phase2)
    ///   Campaign Scene Name   → 캠페인 씬 이름
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyPage1Controller : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("캠페인 모드 버튼")]
        [Tooltip("캠페인 모드 진입 버튼입니다.")]
        [SerializeField] private Button _campaignModeButton;

        [Tooltip("캠페인 모드 버튼 라벨입니다.\n" +
                 "저장 데이터 없음: '새로 시작' / 있음: '이어하기'")]
        [SerializeField] private TMP_Text _buttonLabel;

        [Header("이야기 초기화 버튼")]
        [Tooltip("이야기 초기화 버튼 GameObject입니다.\n" +
                 "저장 데이터 있을 때만 표시됩니다.")]
        [SerializeField] private GameObject _resetButton;

        [Header("도감 페이지 버튼")]
        [Tooltip("도감 페이지(2페이지)로 이동하는 버튼입니다.\n" +
                 "시점 완결문 1개 이상 해금 시 활성화됩니다.")]
        [SerializeField] private GameObject _nextPageButton;

        [Header("경고창")]
        [Tooltip("이야기 초기화 확인 경고창입니다.")]
        [SerializeField] private WarningDialog _warningDialog;

        [Header("데이터")]
        [Tooltip("캠페인 보상 해금 기록 에셋입니다.")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Header("씬/스테이지 설정")]
        [Tooltip("캠페인 스테이지 ID입니다.\n예: Stage_1_Phase2")]
        [SerializeField] private string _stageId = "Stage_1_Phase2";

        [Tooltip("캠페인 씬 이름입니다.")]
        [SerializeField] private string _campaignSceneName = "Stage_1";

        [Tooltip("캠페인 모드 진입에 필요한 클리어 스테이지 ID입니다.\n" +
                 "기본모드 클리어 후 캠페인 진입 가능합니다.")]
        [SerializeField] private string _requiredClearStageId = "Stage_1";

        [Header("버튼 문구")]
        [SerializeField] private string _newGameLabel = "새로 시작";
        [SerializeField] private string _continueLabel = "이어하기";

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            Refresh();

            _campaignModeButton?.onClick.AddListener(OnCampaignModeClicked);
            _resetButton?.GetComponentInChildren<Button>()
                         ?.onClick.AddListener(OnResetClicked);
            _nextPageButton?.GetComponentInChildren<Button>()
                            ?.onClick.AddListener(OnNextPageClicked);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// UI 상태를 갱신합니다.
        /// 로비 진입 시 및 초기화 완료 후 호출합니다.
        /// </summary>
        public void Refresh()
        {
            bool cleared = StageClearRepository.Instance.HasCleared(_requiredClearStageId);
            bool hasSave = CampaignSaveManager.Instance != null &&
                           CampaignSaveManager.Instance.HasSave(_stageId);
            bool hasCodex = CheckCodexUnlocked();

            // 캠페인 모드 버튼
            if (_campaignModeButton != null)
                _campaignModeButton.interactable = cleared;

            if (_buttonLabel != null)
                _buttonLabel.text = hasSave ? _continueLabel : _newGameLabel;

            // 이야기 초기화 버튼: 저장 데이터 있을 때만 표시
            if (_resetButton != null)
                _resetButton.SetActive(hasSave);

            // 도감 페이지 버튼: 시점 완결문 1개 이상 해금 시 활성화
            if (_nextPageButton != null)
                _nextPageButton.SetActive(hasCodex);
        }

        // ── Private ──────────────────────────────────────────────────────

        private void OnCampaignModeClicked()
        {
            TurnHistoryRepository.Instance.ClearAll();
            NewGameConfig.SetSeed(0, _stageId);
            UnityEngine.SceneManagement.SceneManager.LoadScene(_campaignSceneName);
        }

        private void OnResetClicked()
        {
            _warningDialog?.Show(
                message: "이야기 진행 데이터를 초기화합니다.\n이 작업은 되돌릴 수 없습니다.",
                onConfirm: OnResetConfirmed,
                onCancel: null
            );
        }

        private void OnResetConfirmed()
        {
            CampaignSaveManager.Instance?.Delete(_stageId);
            Refresh();
            Debug.Log("[LobbyPage1Controller] 이야기 진행 데이터 초기화 완료");
        }

        private void OnNextPageClicked()
        {
            // LobbyUIManager의 챕터 시스템으로 2페이지로 전환
            var bookAnimator = FindObjectOfType<TitleBookAnimator>();
            bookAnimator?.TurnPage();
        }

        /// <summary>
        /// 시점 완결문(에필로그)이 1개 이상 해금됐는지 확인합니다.
        /// RewardSaveData에서 확인합니다.
        /// </summary>
        private bool CheckCodexUnlocked()
        {
            if (_rewardSaveData == null) return false;
            for (int i = 1; i <= 7; i++)
                if (_rewardSaveData.IsEpilogueUnlocked(i))
                    return true;
            return false;
        }
    }
}