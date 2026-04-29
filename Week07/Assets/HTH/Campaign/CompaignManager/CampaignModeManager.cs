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
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Lobby Scene Name  → "LobbyScene"
    ///   Ending Scene Name → 엔딩 씬 이름 (비우면 로비로 이동)
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
            if (!NewGameConfig.ForceStartAsPhase2) return;

            NewGameConfig.ForceStartAsPhase2 = false;
            StartCoroutine(InitializeCampaign(NewGameConfig.StageId));
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
            collector.OnAllCharactersCompleted += OnAllCharactersCompleted;
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