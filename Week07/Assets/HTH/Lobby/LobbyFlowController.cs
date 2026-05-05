using UnityEngine;
using UnityEngine.SceneManagement;

namespace HTH.Campaign.Lobby
{
    /// <summary>
    /// 로비의 전반적인 흐름(씬 이동, 초기 데이터 세팅, 페이지 전환 애니메이션)을 제어합니다.
    /// 기존 CampaignLobbyNavigator의 역할을 개선하고 통합했습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyFlowController : MonoBehaviour
    {
        [Header("설정 데이터")]
        [Tooltip("씬 이름 / 스테이지 ID / 시드 설정 에셋")]
        [SerializeField] private LobbyConfig _config;

        [Header("애니메이션 제어")]
        [Tooltip("책 페이지를 넘기는 애니메이터")]
        [SerializeField] private TitleBookAnimator _bookAnimator;

        private void Start()
        {
            if (_config == null)
                Debug.LogError("[LobbyFlowController] LobbyConfig가 연결되지 않았습니다.");

            // (선택 사항) 만약 튜토리얼을 강제로 진행해야 하는 시스템이 있다면 여기서 체크 후 StartTutorial() 호출 가능
        }

        /// <summary>기존 세이브 데이터가 존재하는지 확인합니다.</summary>
        public bool HasCampaignSave()
        {
            if (_config == null) return false;
            return CampaignSaveManager.Instance?.HasSave(_config.CampaignStageId) ?? false;
        }

        /// <summary>
        /// 새로운 로비를 시작합니다. 
        /// 기존 세이브를 삭제하고 씬 이동을 위한 초기 데이터를 세팅합니다.
        /// </summary>
        public void StartNewCampaign()
        {
            if (_config == null) return;

            // ★ JSON 파일 삭제 금지 — 초기화는 LobbyPage01Manager에서
            //   CampaignSaveManager.ResetFull / ResetPartial 호출 후 이 메서드가 호출됨
            TurnHistoryRepository.Instance?.ClearAll();

            // 인게임 씬으로 넘길 초기 데이터(시드, 강제 모드 등) 세팅
            PrepareGameSessionData(forcePhase2: false);

            // 씬 이동
            SceneManager.LoadScene(_config.LobbySceneName);
        }

        /// <summary>
        /// 저장된 데이터를 기반으로 캠페인을 이어서 진행합니다.
        /// </summary>
        public void ContinueCampaign()
        {
            if (_config == null) return;

            TurnHistoryRepository.Instance?.ClearAll();

            // 세이브가 있으면 스토리(Phase2)부터, 없으면 기본부터 시작하도록 세팅
            bool hasSave = HasCampaignSave();
            PrepareGameSessionData(forcePhase2: hasSave);

            SceneManager.LoadScene(_config.CampaignSceneName);
        }

        /// <summary>튜토리얼 씬으로 이동합니다.</summary>
        public void StartTutorial()
        {
            if (_config == null) return;

            NewGameConfig.SetTutorial(_config.TutorialFixedSeed);
            SceneManager.LoadScene(_config.TutorialSceneName);
        }

        /// <summary>책의 다음 장(도감 페이지)으로 넘깁니다.</summary>
        public void GoToNextPage()
        {
            if (_bookAnimator != null)
            {
                _bookAnimator.TurnPage();
            }
            else
            {
                Debug.LogWarning("[LobbyFlowController] TitleBookAnimator가 연결되지 않아 페이지를 넘길 수 없습니다.");
            }
        }

        /// <summary>
        /// 스토리 모드(인게임) 씬 진입 전, 데이터를 세팅하는 헬퍼 메서드입니다.
        /// (시작 시 데이터 저장/전달 관리자의 역할)
        /// </summary>
        private void PrepareGameSessionData(bool forcePhase2)
        {
            NewGameConfig.Clear();
            NewGameConfig.SetSeed(_config.DefaultGameSeed, _config.CampaignStageId);
            NewGameConfig.ForceStartAsPhase2 = forcePhase2;
        }
    }
}