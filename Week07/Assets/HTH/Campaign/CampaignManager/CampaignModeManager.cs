using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 전체 상태를 관리하는 싱글톤입니다.
    /// 캠페인 씬에만 배치합니다.
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   씬 로드 후 저장 데이터 로드 및 스테이지 로깅 시작.
    ///   FragmentCollector 완수 이벤트 구독 → 엔딩 씬 전환.
    ///
    /// ─── 진입 흐름 ───────────────────────────────────────────────────────
    ///   [로비에서 이어 쓰기 선택]
    ///   → NewGameConfig.ForceStartAsPhase2 = true
    ///   → 캠페인 씬 로드
    ///   → Start() → InitializeCampaign()
    ///   → OnCampaignInitialized 이벤트 발생
    ///
    ///   [에디터 직접 실행 폴백]
    ///   → NewGameConfig 미설정이면 _editorStageId로 강제 초기화
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Lobby Scene Name   → "LobbyScene"
    ///   Ending Scene Name  → 엔딩 씬 이름 (비우면 로비로 이동)
    ///   Editor Stage Id    → 에디터 직접 실행 시 사용할 StageId (기본 "CampaignMode")
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignModeManager : SingletonMonobehaviour<CampaignModeManager>
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("씬 이름")]
        [Tooltip("로비 씬 이름입니다.")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Tooltip("엔딩 씬 이름입니다. 비워두면 로비로 이동합니다.")]
        [SerializeField] private string _endingSceneName = "";

        [Header("에디터 직접 실행 설정")]
        [Tooltip("NewGameConfig가 설정되지 않은 상태(에디터 직접 실행)일 때 사용할 StageId입니다.\n" +
                 "로비를 통해 진입하면 NewGameConfig.StageId가 우선됩니다.")]
        [SerializeField] private string _editorStageId = "CampaignMode";

        // ── 상태 ─────────────────────────────────────────────────────────

        /// <summary>현재 캠페인 스테이지 ID입니다.</summary>
        public string CurrentStageId { get; private set; }

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>캠페인 초기화 완료 시. 인자: stageId</summary>
        public event Action<string> OnCampaignInitialized;

        /// <summary>캠페인 엔딩 조건 달성 시</summary>
        public event Action OnCampaignEnding;

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            if (NewGameConfig.IsTutorial) return;

            if (NewGameConfig.ForceStartAsPhase2)
            {
                // 정상 진입 — 로비에서 ForceStartAsPhase2 설정
                NewGameConfig.ForceStartAsPhase2 = false;
                StartCoroutine(InitializeCampaign(NewGameConfig.StageId));
            }
            else
            {
                // ★ 에디터 직접 실행 폴백 — NewGameConfig 미설정 시 _editorStageId 사용
                Debug.LogWarning("[CampaignModeManager] NewGameConfig 미설정 — " +
                                 $"에디터 직접 실행으로 간주, StageId: {_editorStageId}");
                StartCoroutine(InitializeCampaign(_editorStageId));
            }
        }

        // ── Private ──────────────────────────────────────────────────────

        private IEnumerator InitializeCampaign(string stageId)
        {
            yield return null; // 씬 초기화 대기

            CurrentStageId = stageId;

            GameLogger.Instance?.StartStageLogging(stageId);
            CampaignSaveManager.GetOrCreate().Load(stageId);
            SubscribeFragmentCollectorEvents();

            Debug.Log($"[CampaignModeManager] 캠페인 초기화 완료 — {stageId}");
            OnCampaignInitialized?.Invoke(stageId);
        }

        private void SubscribeFragmentCollectorEvents()
        {
            var collector = FindFirstObjectByType<FragmentCollector>();
            if (collector == null)
            {
                Debug.LogWarning("[CampaignModeManager] FragmentCollector를 찾을 수 없습니다.");
                return;
            }
            // 미포함 OnAllCharacterCompleted가 없음
            //collector.OnAllCharactersCompleted += OnAllCharactersCompleted;
            Debug.Log("[CampaignModeManager] FragmentCollector 엔딩 이벤트 구독 완료");
        }

        private void OnAllCharactersCompleted()
        {
            Debug.Log("[CampaignModeManager] 캠페인 엔딩 조건 달성 — 모든 캐릭터 기록 완수");
            OnCampaignEnding?.Invoke();

            string targetScene = string.IsNullOrEmpty(_endingSceneName)
                ? _lobbySceneName
                : _endingSceneName;

            StartCoroutine(EndingTransitionCoroutine(targetScene));
        }

        private IEnumerator EndingTransitionCoroutine(string sceneName)
        {
            yield return new WaitForSeconds(1f);
            SceneManager.LoadScene(sceneName);
        }
    }
}