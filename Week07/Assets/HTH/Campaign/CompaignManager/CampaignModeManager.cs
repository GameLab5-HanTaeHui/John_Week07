using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 전체 상태를 관리하는 싱글톤입니다.
    ///
    /// ─── 씬 구조 (4번 작업 이후 기준) ────────────────────────────────────
    ///   기본모드 씬과 캠페인 씬이 완전 분리됩니다.
    ///   이 매니저는 캠페인 씬에만 배치합니다.
    ///   Phase1→Phase2 전환 연출은 제거되었습니다.
    ///
    /// ─── Phase2 진입 흐름 ────────────────────────────────────────────────
    ///   [로비에서 이어 쓰기 선택]
    ///   → NewGameConfig.ForceStartAsPhase2 = true
    ///   → 캠페인 씬 로드
    ///   → Start() → EnterPhase2Direct()
    ///   → OnPhase2Entered 이벤트 발생
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Lobby Scene Name      → "LobbyScene"
    ///   Ending Scene Name     → 엔딩 씬 이름 (비우면 로비로 이동)
    ///   Phase2 Suffix         → "_Phase2"
    ///   Phase2 Role Config    → StageRoleConfig_2 에셋
    ///   Zone                  → 구역 SpriteRenderer (선택)
    ///   Zone Text             → 구역 TextMeshPro (선택)
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

        [Header("Phase2 스테이지 ID 접미사")]
        [Tooltip("예: 'Stage_1' + '_Phase2' = 'Stage_1_Phase2'")]
        [SerializeField] private string _phase2Suffix = "_Phase2";

        [Header("Phase2 역할 배정")]
        [Tooltip("Phase2에서 사용할 StageRoleConfig 에셋입니다.")]
        [SerializeField] private StageRoleConfig _phase2RoleConfig;

        [Header("Zone 설정")]
        [SerializeField] private SpriteRenderer _zone;
        [SerializeField] private TextMeshPro _zoneText;

        // ── 상태 ─────────────────────────────────────────────────────────

        public CampaignPhase CurrentPhase { get; private set; } = CampaignPhase.None;
        public string CurrentPhase2StageId { get; private set; }

        public static bool IsPhase2Active
            => Instance != null && Instance.CurrentPhase == CampaignPhase.Phase2;

        // ── 이벤트 ───────────────────────────────────────────────────────

        /// <summary>Phase2 진입 완료 시. 인자: phase2StageId</summary>
        public event Action<string> OnPhase2Entered;

        /// <summary>캠페인 엔딩 조건 달성 시</summary>
        public event Action OnCampaignEnding;

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            if (NewGameConfig.IsTutorial) return;

            if (NewGameConfig.ForceStartAsPhase2)
            {
                NewGameConfig.ForceStartAsPhase2 = false;

                string phase2StageId = NewGameConfig.StageId + _phase2Suffix;
                StartCoroutine(EnterPhase2Direct(phase2StageId));
            }
        }

        // ── Private — Phase2 직접 진입 ────────────────────────────────────

        /// <summary>
        /// 캠페인 씬 로드 후 한 프레임 대기 → Phase2 상태 설정 → 이벤트 발생.
        /// Phase1→Phase2 전환 연출 없음 (씬 분리로 불필요).
        /// </summary>
        private IEnumerator EnterPhase2Direct(string phase2StageId)
        {
            yield return null; // 씬 초기화 대기

            CurrentPhase = CampaignPhase.Phase2;
            CurrentPhase2StageId = phase2StageId;

            // 스테이지 로깅 시작
            GameLogger.Instance?.StartStageLogging(phase2StageId);

            // 저장 데이터 로드
            CampaignSaveManager.GetOrCreate().Load(phase2StageId);

            // Phase2 역할 재배정
            if (_phase2RoleConfig != null)
                //GameFlowController.Instance?.ReassignRolesForPhase2(_phase2RoleConfig);

            // 능력 무효화 구역 비활성
            //GameFlowController.Instance?.GetCharacterSpawner()?.DisableAllAbilityZones(false);
            //var gameState = GameFlowController.Instance?.GameState as GameState;
            //if (gameState != null)
            //    GameFlowController.Instance.GetCharacterSpawner()?.ApplyZoneRulesToGameState(gameState);

            // Zone UI
            if (_zone != null) { _zone.color = Color.green; }
            if (_zoneText != null)
            {
                _zoneText.color = Color.green;
                _zoneText.text = "조사 지정 구역";
            }

            // FragmentCollector 엔딩 이벤트 구독
            SubscribeFragmentCollectorEvents();

            Debug.Log($"[CampaignModeManager] Phase2 진입 완료 — {phase2StageId}");
            OnPhase2Entered?.Invoke(phase2StageId);
        }

        // ── Private — 엔딩 처리 ───────────────────────────────────────────

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

            Debug.Log($"[CampaignModeManager] 씬 전환 → {targetScene}");
            StartCoroutine(EndingTransitionCoroutine(targetScene));
        }

        private IEnumerator EndingTransitionCoroutine(string sceneName)
        {
            yield return new WaitForSeconds(1f);
            SceneManager.LoadScene(sceneName);
        }
    }
}