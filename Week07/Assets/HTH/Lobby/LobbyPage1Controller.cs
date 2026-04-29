using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    [DisallowMultipleComponent]
    public class LobbyPage1Controller : MonoBehaviour
    {
        [Header("버튼")]
        [Tooltip("새로 쓰기 버튼 — 항상 표시, 처음부터 시작")]
        [SerializeField] private Button _newGameButton;

        [Tooltip("이어 쓰기 버튼 — 저장 데이터 있을 때만 표시, 캠페인 모드 진입")]
        [SerializeField] private Button _continueButton;

        [Header("도감 페이지 버튼")]
        [SerializeField] private GameObject _nextPageButton;

        [Header("경고창")]
        [SerializeField] private WarningDialog _warningDialog;

        [Header("데이터")]
        [SerializeField] private RewardSaveData _rewardSaveData;

        [Header("씬/스테이지 설정")]
        [Tooltip("캠페인 스테이지 ID (예: Stage_1_Phase2)")]
        [SerializeField] private string _stageId = "Stage_1_Phase2";

        [Tooltip("캠페인 씬 이름")]
        [SerializeField] private string _campaignSceneName = "Stage_1";

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            Refresh();
            _newGameButton?.onClick.AddListener(OnNewGameClicked);
            _continueButton?.onClick.AddListener(OnContinueClicked);
            _nextPageButton?.GetComponentInChildren<Button>()
                            ?.onClick.AddListener(OnNextPageClicked);
        }

        public void Refresh()
        {
            bool hasSave = CampaignSaveManager.Instance != null &&
                            CampaignSaveManager.Instance.HasSave(_stageId);
            bool hasCodex = CheckCodexUnlocked();

            // 새로 쓰기 — 항상 표시
            if (_newGameButton != null)
                _newGameButton.gameObject.SetActive(true);

            // 이어 쓰기 — 저장 데이터 있을 때만 표시
            if (_continueButton != null)
                _continueButton.gameObject.SetActive(hasSave);

            // 도감 버튼 — 에필로그 1개 이상 해금 시 표시
            if (_nextPageButton != null)
                _nextPageButton.SetActive(hasCodex);
        }

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>새로 쓰기 — 저장 데이터 초기화 경고 후 처음부터 시작</summary>
        private void OnNewGameClicked()
        {
            bool hasSave = CampaignSaveManager.Instance != null &&
                           CampaignSaveManager.Instance.HasSave(_stageId);

            if (hasSave)
            {
                // 저장 데이터 있으면 경고창 표시
                _warningDialog?.Show(
                    message: "이야기 진행 데이터를 초기화하고\n처음부터 시작합니다.\n이 작업은 되돌릴 수 없습니다.",
                    onConfirm: StartNewGame,
                    onCancel: null
                );
            }
            else
            {
                StartNewGame();
            }
        }

        private void StartNewGame()
        {
            CampaignSaveManager.Instance?.Delete(_stageId);
            TurnHistoryRepository.Instance.ClearAll();
            string baseStageId = _stageId.Replace("_Phase2", "");
            NewGameConfig.SetSeed(0, baseStageId);
            NewGameConfig.ForceStartAsPhase2 = false; // ★ 기본모드부터
            UnityEngine.SceneManagement.SceneManager.LoadScene(_campaignSceneName);
        }

        /// <summary>이어 쓰기 — 저장 데이터 있으면 캠페인 모드로 진입</summary>
        private void OnContinueClicked()
        {
            // ★ 저장 데이터 있으면 Phase2부터, 없으면 기본모드부터
            bool hasSave = CampaignSaveManager.Instance != null &&
                           CampaignSaveManager.Instance.HasSave(_stageId);

            TurnHistoryRepository.Instance.ClearAll();
            string baseStageId = _stageId.Replace("_Phase2", "");
            NewGameConfig.SetSeed(0, baseStageId);
            NewGameConfig.ForceStartAsPhase2 = hasSave;
            UnityEngine.SceneManagement.SceneManager.LoadScene(_campaignSceneName);
        }

        private void OnNextPageClicked()
        {
            var bookAnimator = FindFirstObjectByType<TitleBookAnimator>();
            bookAnimator?.TurnPage();
        }

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