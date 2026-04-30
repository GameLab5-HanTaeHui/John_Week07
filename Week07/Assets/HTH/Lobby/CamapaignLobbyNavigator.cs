using UnityEngine;
using UnityEngine.SceneManagement;

namespace HTH.Campaign
{
    /// <summary>
    /// 로비에서 씬 전환 및 NewGameConfig 설정을 전담합니다.
    ///
    /// ─── 책임 ────────────────────────────────────────────────────────────
    ///   씬 이름 / 스테이지 ID / 시드는 LobbyConfig SO에서만 읽습니다.
    ///   직접 값을 갖지 않으며 NewGameConfig 설정 + 씬 이동만 합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Config → LobbyConfig 에셋 (필수)
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignLobbyNavigator : MonoBehaviour
    {
        public static CampaignLobbyNavigator Instance { get; private set; }

        [Header("설정")]
        [Tooltip("씬 이름 / 스테이지 ID / 시드 설정 에셋입니다.")]
        [SerializeField] private LobbyConfig _config;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            if (_config == null)
                Debug.LogError("[CampaignLobbyNavigator] LobbyConfig가 연결되지 않았습니다.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>세이브 데이터가 존재하는지 확인합니다.</summary>
        public bool HasCampaignSave()
            => _config != null
            && (CampaignSaveManager.Instance?.HasSave(_config.CampaignStageId) ?? false);

        /// <summary>캠페인을 처음부터 시작합니다. 세이브를 삭제하고 기본모드부터 진입합니다.</summary>
        public void StartNewCampaign()
        {
            if (_config == null) return;
            CampaignSaveManager.Instance?.Delete(_config.CampaignStageId);
            TurnHistoryRepository.Instance.ClearAll();
            NewGameConfig.SetSeed(_config.DefaultGameSeed, _config.CampaignStageId);
            NewGameConfig.ForceStartAsPhase2 = false;
            SceneManager.LoadScene(_config.CampaignSceneName);
        }

        /// <summary>캠페인을 이어서 시작합니다. 세이브가 있으면 캠페인 모드, 없으면 기본모드.</summary>
        public void ContinueCampaign()
        {
            if (_config == null) return;
            bool hasSave = HasCampaignSave();
            TurnHistoryRepository.Instance.ClearAll();
            NewGameConfig.SetSeed(_config.DefaultGameSeed, _config.CampaignStageId);
            NewGameConfig.ForceStartAsPhase2 = hasSave;
            SceneManager.LoadScene(_config.CampaignSceneName);
        }

        /// <summary>튜토리얼을 시작합니다.</summary>
        public void StartTutorial()
        {
            if (_config == null) return;
            NewGameConfig.SetTutorial(_config.TutorialFixedSeed);
            SceneManager.LoadScene(_config.TutorialSceneName);
        }

        /// <summary>튜토리얼을 다시 시작합니다.</summary>
        public void RetryTutorial()
        {
            if (_config == null) return;
            NewGameConfig.SetTutorial(_config.TutorialFixedSeed);
            SceneManager.LoadScene(_config.TutorialRetrySceneName);
        }

        /// <summary>기본모드를 지정한 시드로 시작합니다.</summary>
        public void StartWithSeed(int seed)
        {
            if (_config == null) return;
            TurnHistoryRepository.Instance.ClearAll();
            NewGameConfig.SetSeed(seed, _config.DefaultStageId);
            NewGameConfig.ForceStartAsPhase2 = false;
            SceneManager.LoadScene(_config.DefaultGameSceneName);
        }

        /// <summary>기본모드를 랜덤 시드로 시작합니다.</summary>
        public void StartRandom()
        {
            if (_config == null) return;
            TurnHistoryRepository.Instance.ClearAll();
            NewGameConfig.SetRandom(_config.DefaultStageId);
            NewGameConfig.ForceStartAsPhase2 = false;
            SceneManager.LoadScene(_config.DefaultGameSceneName);
        }
    }
}