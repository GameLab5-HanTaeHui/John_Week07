using UnityEngine;
using UnityEngine.SceneManagement;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 전체 상태를 관리하는 싱글톤입니다.
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   1회차 클리어 감지 → 2회차 진입 처리
    ///   현재 회차(CampaignPhase) 상태 유지
    ///   Phase2 StageId 생성 및 관리 (Stage_1 → Stage_1_Phase2)
    ///   씬 전환 방식 2가지 지원
    ///     A. 같은 씬 내 컨텐츠 교체 (현재)
    ///     B. 로비 거쳐서 2회차 씬 진입 (추후)
    ///
    /// ─── 진입 흐름 ───────────────────────────────────────────────────────
    ///   [1회차 클리어]
    ///   FinalDecisionUI → 전부 정답 (isWin = true)
    ///   → OnFirstRunCleared(stageId) 호출
    ///   → Phase2 StageId 생성 (Stage_1 → Stage_1_Phase2)
    ///   → _useSceneTransition 값에 따라 분기
    ///       A방식: EnterPhase2SameScene() → 같은 씬 컨텐츠 교체
    ///       B방식: EnterPhase2ViaLobby()  → 로비 거쳐서 씬 전환
    ///
    ///   [로비에서 2회차 진입] (B방식 전환 후)
    ///   LobbyPresetSeedButton 등에서 Phase2 스테이지 선택
    ///   → EnterPhase2FromLobby(phase2StageId) 호출
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   UseSceneTransition → true: B방식(로비 경유) / false: A방식(같은 씬)
    ///   LobbySceneName    → 로비 씬 이름
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignModeManager : SingletonMonobehaviour<CampaignModeManager>
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("씬 전환 방식")]
        [Tooltip("false = A방식: 같은 씬에서 컨텐츠 교체 (현재)\n" +
                 "true  = B방식: 로비 거쳐서 2회차 씬 진입 (추후)")]
        [SerializeField] private bool _useSceneTransition = false;

        [Tooltip("B방식 전환 시 이동할 로비 씬 이름")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        [Header("엔딩")]
        [Tooltip("엔딩 씬 이름. 비워두면 로비로 이동합니다. (추후 엔딩 씬 생성 시 입력)")]
        [SerializeField] private string _endingSceneName = "";

        [Header("Phase2 스테이지 ID 접미사")]
        [Tooltip("1회차 StageId 뒤에 붙는 접미사. Stage_1 + _Phase2 = Stage_1_Phase2")]
        [SerializeField] private string _phase2Suffix = "_Phase2";

        // ── 상태 ─────────────────────────────────────────────────────────

        /// <summary>현재 진행 중인 캠페인 회차입니다.</summary>
        public CampaignPhase CurrentPhase { get; private set; } = CampaignPhase.None;

        /// <summary>현재 진행 중인 Phase2 StageId입니다. Phase2가 아니면 null.</summary>
        public string CurrentPhase2StageId { get; private set; }

        /// <summary>Phase2가 현재 활성화됐는지 여부입니다.</summary>
        public static bool IsPhase2Active
            => Instance != null && Instance.CurrentPhase == CampaignPhase.Phase2;

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>
        /// Phase2 진입 완료 시 발생합니다.
        /// DialogueTriggerManager 등에서 구독해 초기화하세요.
        /// string 파라미터: Phase2 StageId
        /// </summary>
        public event System.Action<string> OnPhase2Entered;

        /// <summary>
        /// Phase2에서 다시 Phase1으로 돌아갈 때 발생합니다.
        /// (추후 구현 — 현재는 미사용)
        /// </summary>
        public event System.Action OnPhase2Exited;

        /// <summary>
        /// 캠페인 엔딩 조건 달성 시 발생합니다.
        /// 모든 캐릭터의 시점 완결문이 해금됐을 때 발생합니다.
        /// </summary>
        public event System.Action OnCampaignEnding;

        // ── 외부 API ─────────────────────────────────────────────────────

        /// <summary>
        /// FinalDecisionUI에서 1회차 전부 정답 시 호출합니다.
        /// Phase2 StageId를 생성하고 2회차로 진입합니다.
        /// </summary>
        /// <param name="phase1StageId">1회차 StageId (예: Stage_1)</param>
        public void OnFirstRunCleared(string phase1StageId)
        {
            if (string.IsNullOrEmpty(phase1StageId))
            {
                Debug.LogWarning("[CampaignModeManager] OnFirstRunCleared — StageId가 비어있습니다.");
                return;
            }

            // Phase2 StageId 생성 (Stage_1 → Stage_1_Phase2)
            string phase2StageId = phase1StageId + _phase2Suffix;
            Debug.Log($"[CampaignModeManager] 1회차 클리어 — {phase1StageId} → {phase2StageId}");

            // 씬 전환 방식에 따라 분기
            if (_useSceneTransition)
                EnterPhase2ViaLobby(phase2StageId);
            else
                EnterPhase2SameScene(phase2StageId);
        }

        /// <summary>
        /// 로비에서 2회차 스테이지를 선택했을 때 호출합니다. (B방식)
        /// LobbyPresetSeedButton 등에서 Phase2 StageId가 설정된 경우 호출합니다.
        /// </summary>
        /// <param name="phase2StageId">Phase2 StageId (예: Stage_1_Phase2)</param>
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

        /// <summary>현재 Phase2 여부를 확인합니다.</summary>
        public bool IsCurrentlyPhase2() => CurrentPhase == CampaignPhase.Phase2;

        // ── Private ──────────────────────────────────────────────────────

        /// <summary>
        /// A방식: 같은 씬에서 컨텐츠 교체로 2회차 진입합니다.
        ///
        /// 처리 순서:
        ///   1. CurrentPhase = Phase2 설정
        ///   2. GameLogger에 Phase2 로깅 시작
        ///   3. DialogueTriggerManager 활성화 (OnPhase2Entered 이벤트)
        ///   4. 루프 제한 해제는 GameFlowController에서 OnPhase2Entered 구독 후 처리
        /// </summary>
        private void EnterPhase2SameScene(string phase2StageId)
        {
            CurrentPhase = CampaignPhase.Phase2;
            CurrentPhase2StageId = phase2StageId;

            // Phase2 로깅 시작
            GameLogger.Instance?.StartStageLogging(phase2StageId);

            // FragmentCollector 엔딩 이벤트 구독
            SubscribeFragmentCollectorEvents();

            Debug.Log($"[CampaignModeManager] Phase2 진입 (같은 씬) — {phase2StageId}");
            OnPhase2Entered?.Invoke(phase2StageId);
        }

        /// <summary>
        /// B방식: 로비 씬을 거쳐서 2회차로 전환합니다.
        ///
        /// 처리 순서:
        ///   1. NewGameConfig에 Phase2 StageId 설정
        ///   2. 로비 씬으로 이동
        ///   3. 로비에서 Phase2 스테이지로 진입 (EnterPhase2FromLobby 호출)
        /// </summary>
        private void EnterPhase2ViaLobby(string phase2StageId)
        {
            // NewGameConfig에 Phase2 정보 저장
            // 로비에서 이 값을 읽어 Phase2 스테이지로 자동 진입
            NewGameConfig.PendingPhase2StageId = phase2StageId;

            Debug.Log($"[CampaignModeManager] Phase2 진입 (로비 경유) — {phase2StageId}");
            SceneManager.LoadScene(_lobbySceneName);
        }

        // ── 엔딩 처리 ─────────────────────────────────────────────────────

        private void SubscribeFragmentCollectorEvents()
        {
            // Start()가 아닌 Phase2 진입 시점에 구독
            // FragmentCollector는 씬에 배치된 컴포넌트이므로 FindObjectOfType 사용
            var collector = UnityEngine.Object.FindObjectOfType<FragmentCollector>();
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

            // 엔딩 씬이 설정되어 있으면 엔딩 씬으로, 없으면 로비로
            string targetScene = string.IsNullOrEmpty(_endingSceneName)
                ? _lobbySceneName
                : _endingSceneName;

            Debug.Log($"[CampaignModeManager] 씬 전환 → {targetScene}");

            // 약간의 딜레이 후 전환 (결과 연출 시간 확보)
            StartCoroutine(EndingTransitionCoroutine(targetScene));
        }

        private System.Collections.IEnumerator EndingTransitionCoroutine(string sceneName)
        {
            // 결과 표시 대기 (추후 엔딩 연출 추가 시 여기서 처리)
            yield return new UnityEngine.WaitForSeconds(1f);

            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }
}