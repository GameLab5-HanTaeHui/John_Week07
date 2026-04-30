using HTH.Campaign.Lobby;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 로비 1페이지 UI를 관리합니다.
    ///
    /// ─── 책임 ────────────────────────────────────────────────────────────
    ///   버튼 표시/비활성화 상태 관리
    ///   WarningDialog 표시 (새로 쓰기 시 경고)
    ///   씬 이동은 CampaignLobbyNavigator에 위임합니다.
    ///
    /// ─── 의존성 ──────────────────────────────────────────────────────────
    ///   CampaignLobbyNavigator — 씬 이동 전담
    ///   CampaignSaveManager    — 세이브 존재 확인
    ///   RewardSaveData         — 에필로그 해금 확인
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   New Game Button    → 새로 쓰기 버튼
    ///   Continue Button    → 이어 쓰기 버튼 (세이브 있을 때만 표시)
    ///   Next Page Button   → 도감 페이지 버튼 (에필로그 1개 이상 해금 시 표시)
    ///   Warning Dialog     → 새로 쓰기 경고창
    ///   Reward Save Data   → 에필로그 해금 확인용
    /// </summary>
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

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            Refresh();
            _newGameButton?.onClick.AddListener(OnNewGameClicked);
            _continueButton?.onClick.AddListener(OnContinueClicked);
            _nextPageButton?.GetComponentInChildren<Button>()
                            ?.onClick.AddListener(OnNextPageClicked);
        }

        private void OnEnable() => Refresh();

        /// <summary>버튼 표시 상태를 갱신합니다.</summary>
        public void Refresh()
        {
            bool hasSave = CampaignLobbyNavigator.Instance?.HasCampaignSave() ?? false;
            bool hasCodex = CheckCodexUnlocked();

            // 새로 쓰기 — 항상 표시
            _newGameButton?.gameObject.SetActive(true);

            // 이어 쓰기 — 저장 데이터 있을 때만 표시
            _continueButton?.gameObject.SetActive(hasSave);

            // 도감 버튼 — 에필로그 1개 이상 해금 시 표시
            _nextPageButton?.SetActive(hasCodex);
        }

        // ── 버튼 콜백 ────────────────────────────────────────────────────

        /// <summary>새로 쓰기 — 세이브 있으면 경고창, 없으면 바로 시작</summary>
        private void OnNewGameClicked()
        {
            bool hasSave = CampaignLobbyNavigator.Instance?.HasCampaignSave() ?? false;

            if (hasSave)
            {
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
            => CampaignLobbyNavigator.Instance?.StartNewCampaign();

        /// <summary>이어 쓰기 — 세이브 있으면 캠페인 모드, 없으면 기본모드</summary>
        private void OnContinueClicked()
            => CampaignLobbyNavigator.Instance?.ContinueCampaign();

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