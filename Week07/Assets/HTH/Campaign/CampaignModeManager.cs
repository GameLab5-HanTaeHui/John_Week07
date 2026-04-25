using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 전체 상태를 관리하는 싱글톤입니다.
    ///
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   캠페인 모드의 시작과 끝을 총괄합니다.
    ///   1회차(역할 추리)와 2회차(인물 추리) 사이의 전환을 담당하고,
    ///   Phase2가 끝났을 때 엔딩 씬으로 이동하는 역할을 합니다.
    ///
    /// ─── 1회차 → 2회차 진입 흐름 ────────────────────────────────────────
    ///   FinalDecisionUI에서 역할을 전부 정답으로 맞춤
    ///   → FinalDecisionUI.OnSubmitClicked()에서 OnFirstRunCleared() 호출
    ///   → Phase2 StageId 생성 (예: "Stage_1" → "Stage_1_Phase2")
    ///   → _useSceneTransition 값에 따라:
    ///       false(현재): EnterPhase2SameScene() → 같은 씬에서 컨텐츠 교체
    ///       true(추후): EnterPhase2ViaLobby() → 로비 경유 후 2회차 진입
    ///   → OnPhase2Entered 이벤트 발생
    ///   → DialogueTriggerManager, FragmentCollector 등이 이벤트를 수신해 초기화
    ///
    /// ─── 엔딩 흐름 ───────────────────────────────────────────────────────
    ///   모든 캐릭터(#1~#7)의 시점 완결문이 해금됨
    ///   → FragmentCollector.OnAllCharactersCompleted 이벤트 발생
    ///   → CampaignModeManager가 이벤트 수신
    ///   → OnCampaignEnding 이벤트 발생
    ///   → _endingSceneName이 비어있으면 로비로, 있으면 엔딩 씬으로 이동
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///   싱글톤이므로 씬에 1개만 배치합니다.
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   Use Scene Transition → false (현재 A방식 유지)
    ///   Lobby Scene Name     → "LobbyScene"
    ///   Ending Scene Name    → "" (비워두면 로비로 이동, 추후 엔딩 씬 이름 입력)
    ///   Phase2 Suffix        → "_Phase2"
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignModeManager : SingletonMonobehaviour<CampaignModeManager>
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("씬 전환 방식")]
        [Tooltip("false = A방식: 같은 씬에서 컨텐츠 교체 (현재 사용)\n" +
                 "true  = B방식: 로비를 거쳐서 2회차 씬으로 진입 (추후 구현)")]
        [SerializeField] private bool _useSceneTransition = false;

        [Tooltip("로비 씬 이름입니다.\n" +
                 "B방식 전환 시 이동할 씬 이름과 엔딩 씬이 없을 때 이동할 씬 이름에 사용됩니다.")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Header("엔딩")]
        [Tooltip("엔딩 씬 이름입니다.\n" +
                 "비워두면 모든 캐릭터 완수 시 로비로 이동합니다.\n" +
                 "추후 엔딩 씬을 만들면 여기에 씬 이름을 입력합니다.")]
        [SerializeField] private string _endingSceneName = "";

        [Header("Phase2 스테이지 ID 접미사")]
        [Tooltip("1회차 StageId 뒤에 붙는 접미사입니다.\n" +
                 "예: 'Stage_1' + '_Phase2' = 'Stage_1_Phase2'")]
        [SerializeField] private string _phase2Suffix = "_Phase2";

        [Header("2페이즈 Zone 설정 변경")]
        [SerializeField] private SpriteRenderer _zone;
        [SerializeField] private TextMeshPro _zoneText;

        // ── 상태 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 현재 진행 중인 캠페인 회차입니다.
        /// None = 일반 플레이 / Phase1 = 1회차 / Phase2 = 2회차
        /// </summary>
        public CampaignPhase CurrentPhase { get; private set; } = CampaignPhase.None;

        /// <summary>
        /// 현재 진행 중인 Phase2 StageId입니다.
        /// Phase2가 아니면 null입니다.
        /// 예: "Stage_1_Phase2"
        /// </summary>
        public string CurrentPhase2StageId { get; private set; }

        /// <summary>
        /// Phase2가 현재 활성화됐는지 여부입니다.
        /// 다른 스크립트에서 Phase2 여부를 확인할 때 사용합니다.
        /// 예: if (CampaignModeManager.IsPhase2Active) { ... }
        /// </summary>
        public static bool IsPhase2Active
            => Instance != null && Instance.CurrentPhase == CampaignPhase.Phase2;

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>
        /// Phase2 진입이 완료되면 발생합니다.
        /// string 파라미터: Phase2 StageId (예: "Stage_1_Phase2")
        ///
        /// 구독하는 곳:
        ///   DialogueTriggerManager → 초기화 및 활성화
        ///   CharacterRecordBook    → 인물 기록장 초기화
        ///   FragmentCollector      → 수집 기록 로드
        /// </summary>
        public event Action<string> OnPhase2Entered;

        /// <summary>
        /// Phase2에서 다시 Phase1으로 돌아갈 때 발생합니다.
        /// 현재 미사용 — 추후 구현 예정입니다.
        /// </summary>
        public event Action OnPhase2Exited;

        /// <summary>
        /// 캠페인 엔딩 조건 달성 시 발생합니다.
        /// 모든 캐릭터(#1~#7)의 시점 완결문이 해금됐을 때 발생합니다.
        /// </summary>
        public event Action OnCampaignEnding;

        // ── 외부 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 1회차에서 역할을 전부 정답으로 맞췄을 때 호출합니다.
        /// FinalDecisionUI.OnSubmitClicked()에서 isWin = true일 때 호출합니다.
        ///
        /// Phase2 StageId를 생성하고 _useSceneTransition 값에 따라
        /// A방식(같은 씬 교체) 또는 B방식(로비 경유)으로 2회차에 진입합니다.
        /// </summary>
        /// <param name="phase1StageId">1회차 스테이지 ID (예: "Stage_1")</param>
        public void OnFirstRunCleared(string phase1StageId)
        {
            if (string.IsNullOrEmpty(phase1StageId))
            {
                Debug.LogWarning("[CampaignModeManager] OnFirstRunCleared — StageId가 비어있습니다.");
                return;
            }

            string phase2StageId = phase1StageId + _phase2Suffix;
            Debug.Log($"[CampaignModeManager] 1회차 클리어 — {phase1StageId} → {phase2StageId}");

            if (_useSceneTransition)
                EnterPhase2ViaLobby(phase2StageId);
            else
                EnterPhase2SameScene(phase2StageId);
        }

        /// <summary>
        /// 로비에서 2회차 스테이지를 선택했을 때 호출합니다. (B방식 전용)
        /// _useSceneTransition = true로 설정된 경우에 사용됩니다.
        /// 로비의 스테이지 선택 버튼에서 Phase2 StageId가 설정된 경우 호출합니다.
        /// </summary>
        /// <param name="phase2StageId">Phase2 StageId (예: "Stage_1_Phase2")</param>
        public void EnterPhase2FromLobby(string phase2StageId)
        {
            if (string.IsNullOrEmpty(phase2StageId))
            {
                Debug.LogWarning("[CampaignModeManager] EnterPhase2FromLobby — StageId가 비어있습니다.");
                return;
            }

            CurrentPhase = CampaignPhase.Phase2;
            CurrentPhase2StageId = phase2StageId;

            Debug.Log($"[CampaignModeManager] Phase2 진입 (로비 경유) — {phase2StageId}");
            OnPhase2Entered?.Invoke(phase2StageId);
        }

        // ── Private — Phase2 진입 ─────────────────────────────────────────

        /// <summary>
        /// A방식: 같은 씬에서 컨텐츠를 교체해 2회차로 진입합니다.
        /// 씬 이동 없이 CurrentPhase를 Phase2로 변경하고 OnPhase2Entered 이벤트를 발생시킵니다.
        ///
        /// 처리 순서:
        ///   1. CurrentPhase = Phase2 설정
        ///   2. GameLogger에 Phase2 로깅 시작
        ///   3. FragmentCollector 엔딩 이벤트 구독
        ///   4. OnPhase2Entered 이벤트 발생
        ///      → DialogueTriggerManager, CharacterRecordBook 등이 이를 수신해 초기화
        /// </summary>
        private void EnterPhase2SameScene(string phase2StageId)
        {
            CurrentPhase = CampaignPhase.Phase2;
            CurrentPhase2StageId = phase2StageId;

            GameLogger.Instance?.StartStageLogging(phase2StageId);

            _zone.color = Color.white;
            _zoneText.gameObject.SetActive(false);

            // Phase2가 시작된 시점에 FragmentCollector의 엔딩 이벤트를 구독합니다.
            // Start()에서 구독하면 Phase2가 아직 시작 안 됐을 때도 구독되므로 여기서 처리합니다.
            SubscribeFragmentCollectorEvents();

            Debug.Log($"[CampaignModeManager] Phase2 진입 (같은 씬) — {phase2StageId}");
            OnPhase2Entered?.Invoke(phase2StageId);
        }

        /// <summary>
        /// B방식: 로비 씬을 거쳐서 2회차로 전환합니다.
        /// NewGameConfig에 Phase2 StageId를 저장하고 로비 씬으로 이동합니다.
        /// 로비에서 PendingPhase2StageId를 읽어 Phase2 스테이지를 자동으로 시작합니다.
        /// </summary>
        private void EnterPhase2ViaLobby(string phase2StageId)
        {
            // 로비에서 이 값을 읽어 Phase2 스테이지로 자동 진입합니다.
            NewGameConfig.PendingPhase2StageId = phase2StageId;

            Debug.Log($"[CampaignModeManager] Phase2 진입 (로비 경유) — {phase2StageId}");
            SceneManager.LoadScene(_lobbySceneName);
        }

        // ── Private — 엔딩 처리 ───────────────────────────────────────────

        /// <summary>
        /// FragmentCollector의 OnAllCharactersCompleted 이벤트를 구독합니다.
        /// Phase2 진입 시점에 호출됩니다.
        /// FindObjectOfType을 사용해 씬에 배치된 FragmentCollector를 찾습니다.
        /// </summary>
        private void SubscribeFragmentCollectorEvents()
        {
            var collector = GameObject.FindObjectOfType<FragmentCollector>();
            if (collector == null)
            {
                Debug.LogWarning("[CampaignModeManager] FragmentCollector를 찾을 수 없습니다.\n" +
                                 "씬에 FragmentCollector 컴포넌트가 있는지 확인해주세요.");
                return;
            }

            collector.OnAllCharactersCompleted += OnAllCharactersCompleted;
            Debug.Log("[CampaignModeManager] FragmentCollector 엔딩 이벤트 구독 완료");
        }

        /// <summary>
        /// 모든 캐릭터의 시점 완결문이 해금됐을 때 호출됩니다.
        /// OnCampaignEnding 이벤트를 발생시키고 목표 씬으로 전환합니다.
        ///
        /// _endingSceneName이 비어있으면 로비로, 설정되어 있으면 엔딩 씬으로 이동합니다.
        /// 1초 딜레이는 결과 연출을 위한 것으로 추후 수정할 수 있습니다.
        /// </summary>
        private void OnAllCharactersCompleted()
        {
            Debug.Log("[CampaignModeManager] 캠페인 엔딩 조건 달성 — 모든 캐릭터 기록 완수");
            OnCampaignEnding?.Invoke();

            // Phase2 완료 로그
            GameLogger.Instance?.LogEvent("game_end", new System.Collections.Generic.Dictionary<string, object>
            {
                { "result",  "win" },
                { "mode",    "phase2_campaign" },
            });

            string targetScene = string.IsNullOrEmpty(_endingSceneName)
                ? _lobbySceneName
                : _endingSceneName;

            Debug.Log($"[CampaignModeManager] 씬 전환 → {targetScene}");
            StartCoroutine(EndingTransitionCoroutine(targetScene));
        }

        /// <summary>
        /// 1초 딜레이 후 목표 씬으로 전환합니다.
        /// 추후 엔딩 연출(페이드 아웃 등)을 여기에 추가할 수 있습니다.
        /// </summary>
        private IEnumerator EndingTransitionCoroutine(string sceneName)
        {
            yield return new WaitForSeconds(1f);

            // Phase2 세션 로그 업로드
            var logger = GameLogger.Instance;
            if (logger != null)
            {
                string fileName = logger.BuildUploadFileName();
                string stageId = logger.CurrentStageId;
                logger.StopStageLogging();
                byte[] bytes = logger.ExtractCurrentSessionBytes();

                if (bytes != null && LogUploader.Instance != null)
                {
                    // Phase2 클리어는 항상 isWin = true
                    LogUploader.Instance.UploadSessionBytes(
                        bytes, fileName, isWin: true, stageId: stageId,
                        onComplete: () => SceneManager.LoadScene(sceneName));
                    yield break; // 업로드 완료 후 씬 전환
                }
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}