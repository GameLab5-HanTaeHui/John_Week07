using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign.Lobby
{
    /// <summary>
    /// 로비 페이지 1 (표지/시작 페이지)의 UI 요소(버튼, 경고창 등)를 관리합니다.
    /// 실제 로직(씬 이동, 데이터 삭제 등)은 LobbyFlowController에 위임합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyPage01Manager : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button _tutorialButton; // 튜토리얼 버튼 추가
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _continueButton;

        [Header("다음 장 (페이지 2)")]
        [SerializeField] private GameObject _nextPageButtonObj;
        private Button _nextPageButton;

        [Header("경고창")]
        [SerializeField] private WarningDialog _warningDialog;

        [Header("의존성")]
        [SerializeField] private LobbyFlowController _flowController; // 로직 위임 대상
        [SerializeField] private RewardSaveData _rewardSaveData;

        private void Awake()
        {
            if (_nextPageButtonObj != null)
                _nextPageButton = _nextPageButtonObj.GetComponentInChildren<Button>();

            SetupButtons();
        }

        private void OnEnable()
        {
            RefreshUIState();
        }

        private void SetupButtons()
        {
            _tutorialButton?.onClick.AddListener(() => _flowController?.StartTutorial());
            _newGameButton?.onClick.AddListener(OnNewGameClicked);
            _continueButton?.onClick.AddListener(() => _flowController?.ContinueCampaign());
            _nextPageButton?.onClick.AddListener(() => _flowController?.GoToNextPage());
        }

        /// <summary>
        /// 세이브 유무 및 도감 해금 상태에 따라 버튼 표시 여부를 갱신합니다.
        /// </summary>
        public void RefreshUIState()
        {
            if (_flowController == null) return;

            bool hasSave = _flowController.HasCampaignSave();
            bool hasCodex = CheckCodexUnlocked();

            // 이어하기 버튼은 세이브가 있을 때만 활성화
            _continueButton?.gameObject.SetActive(hasSave);

            // 다음장(도감) 버튼은 에필로그 해금 시 활성화
            _nextPageButtonObj?.SetActive(hasCodex);
        }

        private void OnNewGameClicked()
        {
            if (_flowController == null) return;

            // 이미 진행 중인 데이터가 있다면 경고창 표시
            if (_flowController.HasCampaignSave())
            {
                _warningDialog?.Show(
                    message: "기존의 이야기 진행 상황이 모두 초기화됩니다.\n새로 시작하시겠습니까?",
                    onConfirm: () => _flowController.StartNewCampaign(),
                    onCancel: null
                );
            }
            else
            {
                _flowController.StartNewCampaign();
            }
        }

        private bool CheckCodexUnlocked()
        {
            if (_rewardSaveData == null) return false;
            for (int i = 1; i <= 7; i++)
            {
                if (_rewardSaveData.IsEpilogueUnlocked(i)) return true;
            }
            return false;
        }
    }
}